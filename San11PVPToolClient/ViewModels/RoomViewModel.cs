using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using San11PVPToolClient.Models;
using San11PVPToolClient.Services;
using San11PVPToolShared.Models;

namespace San11PVPToolClient.ViewModels;

public class RoomViewModel : ViewModelBase, IRoutableViewModel
{
    public string UrlPathSegment => "room";
    public IScreen HostScreen { get; }

    public bool IsRoomOwner => UserInfo != null && UserInfo.IsRoomOwner;

    public PlayerInfo? UserInfo
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public RoomInfo? RoomInfo
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public SaveDataSummary? SaveDataSummary
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string InputText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    private bool IsOnline
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;

    public ObservableCollection<MessageItem> Messages { get; } = new();


    private readonly OnlineService _client;
    private readonly UserSettingsService _userSettingsService;

    private CancellationTokenSource _cts;

    private DateTime _saveDataMTime;
    private DispatcherTimer? _saveDataCheckTimer;

    private readonly SemaphoreSlim _autoUploadSemaphore = new SemaphoreSlim(1, 1);

    public ReactiveCommand<Unit, Unit> SettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> LeaveRoomCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseRoomCommand { get; }
    public ReactiveCommand<Unit, Unit> UploadSaveCommand { get; }
    public ReactiveCommand<Unit, Unit> DownloadSaveCommand { get; }
    public ReactiveCommand<Unit, Unit> SendMessageCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearMessagesCommand { get; }
    public ReactiveCommand<Unit, Unit> SetRoomConfigCommand { get; }
    public ReactiveCommand<PlayerInfo, Unit> SetKingNameCommand { get; }
    public ReactiveCommand<PlayerInfo, Unit> SetOwnerCommand { get; }
    public ReactiveCommand<PlayerInfo, Unit> KickPlayerCommand { get; }

    public Interaction<UserSettings, UserSettings?> OpenSettingsInteraction { get; } = new();
    public Interaction<Unit, RoomConfig?> SetRoomConfigInteraction { get; } = new();
    public Interaction<PlayerInfo, string?> SetKingNameInteraction { get; } = new();

    public RoomViewModel(IScreen screen, OnlineService client, UserSettingsService userSettingsService)
    {
        HostScreen = screen;
        _client = client;
        _userSettingsService = userSettingsService;

        var isOnline = this.WhenAnyValue(x => x.IsOnline)
            .Select(x => x);

        var canSend = this.WhenAnyValue(
            x => x.InputText,
            x => x.IsOnline,
            (text, online) => !string.IsNullOrWhiteSpace(text) && online
        );

        SettingsCommand = ReactiveCommand.CreateFromTask(OpenSettings);
        LeaveRoomCommand = ReactiveCommand.CreateFromTask(LeaveRoom);
        CloseRoomCommand = ReactiveCommand.CreateFromTask(CloseRoom, isOnline);
        UploadSaveCommand = ReactiveCommand.CreateFromTask(UploadSave, isOnline);
        DownloadSaveCommand = ReactiveCommand.CreateFromTask(DownloadSave, isOnline);
        SendMessageCommand = ReactiveCommand.CreateFromTask(SendMessage, canSend);
        ClearMessagesCommand = ReactiveCommand.Create(ClearMessage);
        SetRoomConfigCommand = ReactiveCommand.CreateFromTask(SetRoomConfig, isOnline);
        SetKingNameCommand = ReactiveCommand.CreateFromTask<PlayerInfo>(SetKingName, isOnline);
        SetOwnerCommand = ReactiveCommand.CreateFromTask<PlayerInfo>(SetOwner, isOnline);
        KickPlayerCommand = ReactiveCommand.CreateFromTask<PlayerInfo>(KickPlayer, isOnline);
    }

    protected override void DoWhenActivated(CompositeDisposable disposable)
    {
        _client.Events.LoginStateChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x =>
            {
                UserInfo = x.Item1;
                RoomInfo = x.Item2;
                this.RaisePropertyChanged(nameof(IsRoomOwner));
            })
            .DisposeWith(disposable);

        _client.Events.SocketConnectionChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async connected =>
            {
                if (connected == true)
                {
                    AddSystemMessage("成功连接到服务器");
                    IsOnline = true;
                }
                else if (connected == false)
                {
                    AddSystemMessage("连接中断", MessageLevel.Warning);
                    IsOnline = false;
                    if (!_client.IsTerminated) // 没有彻底终止连接，说明是网络问题，尝试重连
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            try
                            {
                                AddSystemMessage($"尝试重连({i + 1}/5)...");
                                await _client.Reconnect(_cts.Token);
                                break;
                            }
                            catch
                            {
                                // ignored
                            }

                            await Task.Delay(3000);
                        }
                    }
                }
            })
            .DisposeWith(disposable);

        _client.Events.PlayerKicked
            .Subscribe(async void (eventData) =>
            {
                var byPlayer = eventData.ByPlayer;
                if (eventData.KickedPlayer.PlayerId == UserInfo?.PlayerId)
                {
                    AddSystemMessage($"你被{eventData.ByPlayer.Name}踢出房间");
                    await _client.TerminateSocket();
                }
                else
                {
                    AddSystemMessage($"{eventData.KickedPlayer.Name}被{eventData.ByPlayer.Name}踢出房间");
                }
            })
            .DisposeWith(disposable);

        _client.Events.RoomClosed
            .Subscribe(async _ =>
            {
                AddSystemMessage("房间已关闭");
                await _client.TerminateSocket();
            })
            .DisposeWith(disposable);

        _client.Events.RoomInfoUpdated
            .Subscribe(eventData =>
            {
                RoomInfo = eventData.RoomInfo;
                if (!string.IsNullOrEmpty(eventData.Message))
                    AddSystemMessage(eventData.Message);
                // 顺便更新自己的信息
                var myInfo = RoomInfo?.Players.FirstOrDefault(p => p.PlayerId == UserInfo?.PlayerId);
                if (myInfo != null)
                    UserInfo = myInfo;
            })
            .DisposeWith(disposable);

        _client.Events.SaveUploaded
            .Subscribe(eventData =>
            {
                var player = eventData.Player;
                SaveDataSummary = eventData.SaveDataSummary;

                if (player.PlayerId == UserInfo?.PlayerId)
                {
                    AddSystemMessage("存档上传成功");
                }
                else
                {
                    AddSystemMessage($"{player.Name}上传了存档");
                    if (_userSettingsService.Settings.AutoDownload && SaveDataSummary != null &&
                        SaveDataSummary.CurrentKingName == UserInfo?.KingName)
                    {
                        AddSystemMessage("自动下载存档");
                        _ = DownloadSave();
                    }
                }
            })
            .DisposeWith(disposable);

        _client.Events.SystemMessageReceived
            .Subscribe(sysMsg =>
            {
                AddSystemMessage(sysMsg.Message);
            })
            .DisposeWith(disposable);

        _client.Events.ChatMessageReceived
            .Subscribe(chatMsg =>
            {
                AddPlayerMessage(chatMsg);
            })
            .DisposeWith(disposable);

        _cts = new CancellationTokenSource();
        // 当 deactivate 时自动取消
        Disposable.Create(() =>
            {
                _cts.Cancel();
                _cts.Dispose();
            })
            .DisposeWith(disposable);

        InitAutoUploadTimer();
    }

    private void InitAutoUploadTimer()
    {
        // 开启了自动上传但timer没有启动
        if (_userSettingsService.Settings.AutoUpload && (_saveDataCheckTimer == null || !_saveDataCheckTimer.IsEnabled))
        {
            _saveDataMTime = DateTime.Now;
            _saveDataCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _saveDataCheckTimer.Tick += SaveDataCheckTimer_TickAsync;
            _saveDataCheckTimer.Start();
            AddSystemMessage("已启动自动上传");
        }
        // 没开启自动上传但timer已经启动
        else if (!_userSettingsService.Settings.AutoUpload && (_saveDataCheckTimer?.IsEnabled ?? false))
        {
            _saveDataCheckTimer.Stop();
            _saveDataCheckTimer.Tick -= SaveDataCheckTimer_TickAsync;
            _saveDataCheckTimer = null;
            AddSystemMessage("已停止自动上传");
        }
    }

    private async Task OpenSettings()
    {
        var userSettings = await OpenSettingsInteraction.Handle(_userSettingsService.Settings);
        if (userSettings is null) return;
        _userSettingsService.Settings = userSettings;
        _userSettingsService.Save();
        InitAutoUploadTimer(); // 自动上传设定可能改变，重新初始化
    }

    private async Task LeaveRoom()
    {
        try
        {
            await _client.LeaveRoom();
            await HostScreen.Router.NavigateBack.Execute();
        }
        catch (Exception ex)
        {
            AddSystemMessage($"连接失败：{ex.Message}", MessageLevel.Error);
        }
    }

    private async Task CloseRoom()
    {
        try
        {
            await _client.CloseRoom();
        }
        catch (Exception ex)
        {
            AddSystemMessage($"连接失败：{ex.Message}", MessageLevel.Error);
        }
    }

    private async Task UploadSave()
    {
        try
        {
            string baseSavePath = Path.Combine(_userSettingsService.Settings.SaveDataDir, "Save031.s11");
            if (!File.Exists(baseSavePath))
            {
                AddSystemMessage($"存档不存在：\"{baseSavePath}\"", MessageLevel.Error);
                return;
            }

            List<string> paths = [baseSavePath];

            foreach (var extension in new[] { ".exsav", ".xml" })
            {
                string exPath = Path.ChangeExtension(baseSavePath, extension);
                if (File.Exists(exPath))
                    paths.Add(exPath);
            }

            await _client.UploadSave(paths);
        }
        catch (Exception ex)
        {
            AddSystemMessage($"上传失败：{ex.Message}", MessageLevel.Error);
        }
    }

    private async Task DownloadSave()
    {
        await _autoUploadSemaphore.WaitAsync();
        try
        {
            await _client.DownloadSave(Path.Combine(_userSettingsService.Settings.SaveDataDir, "Save031.s11"));
            var saveDataPath = Path.Combine(_userSettingsService.Settings.SaveDataDir, "Save031.s11");
            var mtime = File.GetLastWriteTime(saveDataPath);
            _saveDataMTime = mtime; // 避免触发自动上传
        }
        catch (Exception ex)
        {
            AddSystemMessage($"下载失败：{ex.Message}", MessageLevel.Error);
        }
        finally
        {
            _autoUploadSemaphore.Release();
        }
    }

    private async Task SendMessage()
    {
        try
        {
            await _client.SendMessage(InputText.Trim());
            InputText = "";
        }
        catch (Exception ex)
        {
            AddSystemMessage($"连接失败：{ex.Message}", MessageLevel.Error);
        }
    }

    private void ClearMessage()
    {
        Messages.Clear();
    }

    private async Task SetRoomConfig()
    {
        var roomConfig = await SetRoomConfigInteraction.Handle(Unit.Default);
        if (roomConfig == null) return;
        await _client.SetRoomConfig(roomConfig);
    }

    private async Task SetKingName(PlayerInfo player)
    {
        string? kingName = await SetKingNameInteraction.Handle(player);
        if (kingName == null) return;
        await _client.SetKingName(player.PlayerId, kingName);
    }

    private async Task SetOwner(PlayerInfo player)
    {
        await _client.SetOwner(player.PlayerId);
    }

    private async Task KickPlayer(PlayerInfo player)
    {
        await _client.KickPlayer(player.PlayerId);
    }

    public void AddSystemMessage(string msg, MessageLevel level = MessageLevel.Normal)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Messages.Add(new("", "", msg, DateTime.Now, true, level));
        });
    }

    public void AddPlayerMessage(ChatMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Messages.Add(new(message.SenderId, message.SenderName, message.Message, DateTime.Now,
                DisplayAlignment: message.SenderId == UserInfo?.PlayerId ? "Right" : "Left"));
        });
    }

    private async void SaveDataCheckTimer_TickAsync(object? sender, EventArgs e)
    {
        if (!await _autoUploadSemaphore.WaitAsync(0))
            return;

        try
        {
            var saveDataPath = Path.Combine(_userSettingsService.Settings.SaveDataDir, "Save031.s11");
            try
            {
                // 能够独占文件，确保存档不是正在写入中
                using var stream = File.Open(saveDataPath, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch { return; }

            var mtime = File.GetLastWriteTime(saveDataPath);
            if (mtime > _saveDataMTime)
            {
                _saveDataMTime = mtime;
                AddSystemMessage("检测到存档更新");
                await Task.Delay(TimeSpan.FromMilliseconds(200)); // 等待游戏内存档文件的保存操作完成，避免冲突造成文件发送失败
                await UploadSave();
            }
        }
        finally
        {
            _autoUploadSemaphore.Release();
        }
    }
}

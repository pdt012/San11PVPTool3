using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform;
using DynamicData;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using San11PVPToolClient.Services;
using San11PVPToolShared.Models;

namespace San11PVPToolClient.ViewModels;

public class LobbyViewModel : ViewModelBase, IRoutableViewModel
{
    public string UrlPathSegment => "lobby";
    public IScreen HostScreen { get; }

    private readonly OnlineService _client;

    private readonly UserConfigService _userConfigService;

    private CancellationTokenSource _cts;

    public ObservableCollection<RoomInfoSummary> Rooms { get; } = new();

    public ReactiveCommand<Unit, Unit> SettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> AboutCommand { get; }
    public ReactiveCommand<Unit, Unit> GetRoomListCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateRoomCommand { get; }
    public ReactiveCommand<RoomInfoSummary, Unit> JoinRoomCommand { get; }

    public Interaction<Unit, RoomConfig?> SetRoomConfigInteraction { get; } = new();
    public Interaction<Unit, string?> InputPasswordInteraction { get; } = new();
    public Interaction<MessageBoxStandardParams, Unit> ShowMsgBoxInteraction { get; } = new();

    public LobbyViewModel(IScreen screen, OnlineService client, UserConfigService userConfigService)
    {
        HostScreen = screen;
        _client = client;
        _userConfigService = userConfigService;

        SettingsCommand = ReactiveCommand.CreateFromTask(OpenSettings);
        AboutCommand = ReactiveCommand.CreateFromTask(OpenAbout);
        GetRoomListCommand = ReactiveCommand.CreateFromTask(GetRoomList);
        CreateRoomCommand = ReactiveCommand.CreateFromTask(CreateRoom);
        JoinRoomCommand = ReactiveCommand.CreateFromTask<RoomInfoSummary>(JoinRoom);
    }

    protected override void DoWhenActivated(CompositeDisposable disposable)
    {
        _client.ServerUrl = _userConfigService.Config.ServerAddress;

        _cts = new CancellationTokenSource();
        // 当 deactivate 时自动取消
        Disposable.Create(() =>
            {
                _cts.Cancel();
                _cts.Dispose();
            })
            .DisposeWith(disposable);

        GetRoomListCommand
            .Execute()
            .Subscribe()
            .DisposeWith(disposable);
    }

    private async Task OpenSettings()
    {
        await HostScreen.Router.Navigate.Execute(new SettingsViewModel(HostScreen, _userConfigService));
    }

    private async Task OpenAbout()
    {
        await HostScreen.Router.Navigate.Execute(new AboutViewModel(HostScreen));
    }

    private async Task GetRoomList()
    {
        try
        {
            List<RoomInfoSummary> rooms = await _client.GetRoomList(_cts.Token);
            Rooms.Clear();
            Rooms.AddRange(rooms);
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            await ShowMsgBoxAsync("连接失败", $"{ex.Message}");
        }
    }

    private async Task CreateRoom()
    {
        RoomConfig? roomConfig = await SetRoomConfigInteraction.Handle(Unit.Default);
        if (roomConfig == null) return;

        try
        {
            (bool success, string message) =
                await _client.CreateRoom(_userConfigService.Config.UserName, roomConfig, _cts.Token);
            if (success)
            {
                await HostScreen.Router.Navigate.Execute(new RoomViewModel(HostScreen, _client, _userConfigService));
            }
            else
            {
                await ShowMsgBoxAsync("创建失败", message);
            }
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            await ShowMsgBoxAsync("连接失败", $"{ex.Message}");
        }
    }

    private async Task JoinRoom(RoomInfoSummary room)
    {
        string? password;
        if (room.HasPassword)
        {
            password = await InputPasswordInteraction.Handle(Unit.Default);
            if (password == null) return;
        }
        else
        {
            password = null;
        }
        
        try
        {
            (bool success, string message) =
                await _client.JoinRoom(_userConfigService.Config.UserName, room.RoomId, password, _cts.Token);
            if (success)
            {
                await HostScreen.Router.Navigate.Execute(new RoomViewModel(HostScreen, _client, _userConfigService));
            }
            else
            {
                await ShowMsgBoxAsync("加入失败", message);
            }
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            await ShowMsgBoxAsync("连接失败", $"{ex.Message}");
        }
    }

    private async Task ShowMsgBoxAsync(string title, string message, Icon icon = Icon.Error,
        ButtonEnum button = ButtonEnum.Ok)
    {
        await using var stream = AssetLoader.Open(new Uri("avares://San11PVPToolClient/Assets/pvpTool.ico"));
        var boxParams = new MessageBoxStandardParams
        {
            WindowIcon = new WindowIcon(stream),
            ContentTitle = title,
            ContentMessage = message,
            Icon = icon,
            ButtonDefinitions = button,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        await ShowMsgBoxInteraction.Handle(boxParams);
    }
}

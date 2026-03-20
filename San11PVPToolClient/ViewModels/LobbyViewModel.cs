using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using San11PVPToolClient.Services;
using San11PVPToolShared.Models;

namespace San11PVPToolClient.ViewModels;

public class LobbyViewModel : ViewModelBase, IRoutableViewModel
{
    public string UrlPathSegment => "lobby";
    public IScreen HostScreen { get; }

    private readonly MainViewModel _mainViewModel;
    private readonly OnlineService _client;
    private readonly UserSettingsService _userSettingsService;

    private CancellationTokenSource _cts;

    public ObservableCollection<RoomInfoSummary> Rooms { get; } = new();

    public ReactiveCommand<Unit, Unit> SettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> AboutCommand { get; }
    public ReactiveCommand<Unit, Unit> NetworkConfigCommand { get; }
    public ReactiveCommand<Unit, Unit> GetRoomListCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateRoomCommand { get; }
    public ReactiveCommand<RoomInfoSummary, Unit> JoinRoomCommand { get; }

    public Interaction<Unit, RoomConfig?> SetRoomConfigInteraction { get; } = new();
    public Interaction<Unit, string?> InputPasswordInteraction { get; } = new();

    public LobbyViewModel(IScreen screen, MainViewModel mainViewModel, OnlineService client,
        UserSettingsService userSettingsService)
    {
        HostScreen = screen;
        _mainViewModel = mainViewModel;
        _client = client;
        _userSettingsService = userSettingsService;

        SettingsCommand = ReactiveCommand.CreateFromTask(OpenSettings);
        AboutCommand = ReactiveCommand.CreateFromTask(OpenAbout);
        NetworkConfigCommand = ReactiveCommand.CreateFromTask(OpenNetworkConfig);
        GetRoomListCommand = ReactiveCommand.CreateFromTask(GetRoomList);
        CreateRoomCommand = ReactiveCommand.CreateFromTask(CreateRoom);
        JoinRoomCommand = ReactiveCommand.CreateFromTask<RoomInfoSummary>(JoinRoom);
    }

    protected override void DoWhenActivated(CompositeDisposable disposable)
    {
        _client.ServerUrl = _client.NetworkConfig.ServerAddress;

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
        var userSettings = await _mainViewModel.OpenSettingsInteraction.Handle(_userSettingsService.Settings);
        if (userSettings is null) return;
        _userSettingsService.Settings = userSettings;
        _userSettingsService.Save();
    }

    private async Task OpenAbout()
    {
        await HostScreen.Router.Navigate.Execute(new AboutViewModel(HostScreen));
    }

    private async Task OpenNetworkConfig()
    {
        await HostScreen.Router.Navigate.Execute(new NetworkConfigViewModel(HostScreen, _client));
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
            await _mainViewModel.ShowMsgBoxAsync("连接失败", $"{ex.Message}", Icon.Error);
            Rooms.Clear();
        }
    }

    private async Task CreateRoom()
    {
        RoomConfig? roomConfig = await SetRoomConfigInteraction.Handle(Unit.Default);
        if (roomConfig == null) return;

        try
        {
            (bool success, string message) =
                await _client.CreateRoom(_client.NetworkConfig.UserName, roomConfig, _cts.Token);
            if (success)
            {
                await HostScreen.Router.Navigate.Execute(
                    new RoomViewModel(HostScreen, _mainViewModel, _client, _userSettingsService));
            }
            else
            {
                await _mainViewModel.ShowMsgBoxAsync("创建失败", message, Icon.Error);
            }
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            await _mainViewModel.ShowMsgBoxAsync("连接失败", $"{ex.Message}", Icon.Error);
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
                await _client.JoinRoom(_client.NetworkConfig.UserName, room.RoomId, password, _cts.Token);
            if (success)
            {
                await HostScreen.Router.Navigate.Execute(new RoomViewModel(HostScreen, _mainViewModel, _client,
                    _userSettingsService));
            }
            else
            {
                await _mainViewModel.ShowMsgBoxAsync("加入失败", message, Icon.Error);
            }
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            await _mainViewModel.ShowMsgBoxAsync("连接失败", $"{ex.Message}", Icon.Error);
        }
    }
}

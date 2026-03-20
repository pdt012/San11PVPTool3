using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using San11PVPToolClient.Models;
using San11PVPToolClient.Services;

namespace San11PVPToolClient.ViewModels;

public class MainViewModel : ViewModelBase, IScreen
{
    public RoutingState Router { get; } = new();

    public OnlineService Client { get; }

    public UserSettingsService UserSettingsService { get; }

    public Interaction<MessageBoxStandardParams, ButtonResult> ShowMsgBoxInteraction { get; } = new();
    public Interaction<UserSettings, UserSettings?> OpenSettingsInteraction { get; } = new();

    public MainViewModel()
    {
        UserSettingsService = new UserSettingsService();

        Client = new OnlineService();

        Router.Navigate.Execute(new LobbyViewModel(this, this, Client, UserSettingsService)).Subscribe();
    }

    public async Task<bool> CanCloseAsync()
    {
        // 并不在联机状态则直接关闭
        if (Client.IsTerminated) return true;
        // 否则询问
        return await ShowAskBoxAsync("关闭联机工具", "联机中，是否退出？");
    }

    public async Task ShowMsgBoxAsync(string title, string message, Icon icon = Icon.Info,
        ButtonEnum button = ButtonEnum.Ok, WindowStartupLocation location = WindowStartupLocation.CenterOwner)
    {
        await using var stream = AssetLoader.Open(new Uri("avares://San11PVPToolClient/Assets/pvpTool.ico"));
        var boxParams = new MessageBoxStandardParams
        {
            WindowIcon = new WindowIcon(stream),
            ContentTitle = title,
            ContentMessage = message,
            Icon = icon,
            ButtonDefinitions = button,
            WindowStartupLocation = location
        };
        await ShowMsgBoxInteraction.Handle(boxParams);
    }

    public async Task<bool> ShowAskBoxAsync(string title, string message,
        WindowStartupLocation location = WindowStartupLocation.CenterOwner)
    {
        await using var stream = AssetLoader.Open(new Uri("avares://San11PVPToolClient/Assets/pvpTool.ico"));
        var boxParams = new MessageBoxStandardParams
        {
            WindowIcon = new WindowIcon(stream),
            ContentTitle = title,
            ContentMessage = message,
            Icon = Icon.Question,
            ButtonDefinitions = ButtonEnum.OkCancel,
            WindowStartupLocation = location
        };
        var result = await ShowMsgBoxInteraction.Handle(boxParams);
        return result == ButtonResult.Ok;
    }
}

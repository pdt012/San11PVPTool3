using System;
using ReactiveUI;
using San11PVPToolClient.Services;

namespace San11PVPToolClient.ViewModels;

public class MainViewModel : ViewModelBase, IScreen
{
    public RoutingState Router { get; } = new();

    public OnlineService Client { get; }

    public UserSettingsService UserSettingsService { get; }

    public MainViewModel()
    {
        UserSettingsService = new UserSettingsService();

        Client = new OnlineService();

        Router.Navigate.Execute(new LobbyViewModel(this, Client, UserSettingsService)).Subscribe();
    }
}

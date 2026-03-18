using System;
using ReactiveUI;
using San11PVPToolClient.Services;

namespace San11PVPToolClient.ViewModels;

public class MainViewModel : ViewModelBase, IScreen
{
    public RoutingState Router { get; } = new();

    public OnlineService Client { get; }

    public UserConfigService UserConfigService { get; }

    public MainViewModel()
    {
        UserConfigService = new UserConfigService();

        Client = new OnlineService(UserConfigService.Config.ServerAddress);

        Router.Navigate.Execute(new LobbyViewModel(this, Client, UserConfigService)).Subscribe();
    }
}

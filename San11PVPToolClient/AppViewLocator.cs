using System;
using ReactiveUI;
using San11PVPToolClient.ViewModels;
using San11PVPToolClient.Views;

namespace San11PVPToolClient;

public class AppViewLocator : IViewLocator
{
    public IViewFor ResolveView<T>(T viewModel, string contract = null) => viewModel switch
    {
        LobbyViewModel context => new LobbyView { DataContext = context },
        SettingsViewModel context => new SettingsView { DataContext = context },
        AboutViewModel context => new AboutView { DataContext = context },
        RoomViewModel context => new RoomView { DataContext = context },
        _ => throw new ArgumentOutOfRangeException(nameof(viewModel))
    };
}

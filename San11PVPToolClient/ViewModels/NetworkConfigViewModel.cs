using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using San11PVPToolClient.Models;
using San11PVPToolClient.Services;

namespace San11PVPToolClient.ViewModels;

public class NetworkConfigViewModel : ViewModelBase, IRoutableViewModel
{
    public string UrlPathSegment => "lobby";

    public IScreen HostScreen { get; }

    private readonly OnlineService _onlineService;

    public string UserName
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string ServerAddress
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ReactiveCommand<Unit, Unit> BackCommand { get; }

    public NetworkConfigViewModel(IScreen screen, OnlineService onlineService)
    {
        HostScreen = screen;
        _onlineService = onlineService;
        UserName = onlineService.NetworkConfig.UserName;
        ServerAddress = onlineService.NetworkConfig.ServerAddress;

        BackCommand = ReactiveCommand.CreateFromTask(Back);
    }

    private async Task Back()
    {
        _onlineService.NetworkConfig = new NetworkConfig(UserName, ServerAddress);
        _onlineService.Save();
        await HostScreen.Router.NavigateBack.Execute();
    }
}

using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;

namespace San11PVPToolClient.ViewModels;

public class AboutViewModel : ViewModelBase, IRoutableViewModel
{
    public string UrlPathSegment => "about";
    public IScreen HostScreen { get; }

    public ReactiveCommand<Unit, Unit> BackCommand { get; }

    public AboutViewModel(IScreen screen)
    {
        HostScreen = screen;

        BackCommand = ReactiveCommand.CreateFromTask(Back);
    }

    private async Task Back()
    {
        await HostScreen.Router.NavigateBack.Execute();
    }
}

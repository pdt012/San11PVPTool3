using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using San11PVPToolClient.Models;
using San11PVPToolClient.Services;

namespace San11PVPToolClient.ViewModels;

public class SettingsViewModel : ViewModelBase, IRoutableViewModel
{
    public string UrlPathSegment => "lobby";

    public IScreen HostScreen { get; }

    private readonly UserConfigService _userConfigService;

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

    public string SaveDataDir
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool AutoUpload
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool AutoDownload
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ReactiveCommand<Unit, Unit> BackCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectSaveDataDirCommand { get; }


    public Interaction<Unit, string?> OpenFolderInteraction { get; } = new();

    public SettingsViewModel(IScreen screen, UserConfigService userConfigService)
    {
        HostScreen = screen;
        _userConfigService = userConfigService;
        UserName = userConfigService.Config.UserName;
        ServerAddress = userConfigService.Config.ServerAddress;
        SaveDataDir = userConfigService.Config.SaveDataDir ?? "";
        AutoUpload = userConfigService.Config.AutoUpload;
        AutoDownload = userConfigService.Config.AutoDownload;

        BackCommand = ReactiveCommand.CreateFromTask(Back);
        SelectSaveDataDirCommand = ReactiveCommand.CreateFromTask(SelectSaveDataDir);
    }

    private async Task Back()
    {
        _userConfigService.Config = new UserConfig(UserName, ServerAddress, SaveDataDir, AutoUpload, AutoDownload);
        _userConfigService.Save();
        await HostScreen.Router.NavigateBack.Execute();
    }

    private async Task SelectSaveDataDir()
    {
        string? dir = await OpenFolderInteraction.Handle(Unit.Default);
        if (dir == null) return;
        SaveDataDir = dir;
    }
}

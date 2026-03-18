using System.IO;
using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using ReactiveUI;
using San11PVPToolClient.ViewModels;

namespace San11PVPToolClient.Views;

public partial class SettingsView : ReactiveUserControl<SettingsViewModel>
{
    public SettingsView()
    {
        InitializeComponent();
        if (Design.IsDesignMode) return;

        this.WhenActivated(disposables =>
        {
            ViewModel!.OpenFolderInteraction.RegisterHandler(async interaction =>
            {
                var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
                if (storageProvider == null)
                {
                    interaction.SetOutput(null);
                    return;
                }

                var folder = await storageProvider.TryGetFolderFromPathAsync(
                    ViewModel.SaveDataDir.Length > 0 ? ViewModel.SaveDataDir : Path.GetFullPath("."));

                var dirs = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "请选择SaveData文件夹",
                    SuggestedStartLocation = folder,
                    SuggestedFileName = "SaveData",
                    AllowMultiple = false
                });

                if (dirs.Count == 0)
                {
                    interaction.SetOutput(null);
                    return;
                }

                var dir = dirs[0].Path.LocalPath;
                interaction.SetOutput(dir);
            }).DisposeWith(disposables);
        });
    }
}

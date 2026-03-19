using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using ReactiveUI;
using San11PVPToolClient.Models;
using San11PVPToolClient.ViewModels;

namespace San11PVPToolClient.Dialogs;

public partial class UserSettingsDialog : ReactiveWindow<UserSettingsDialogViewModel>
{
    public UserSettingsDialog()
    {
        InitializeComponent();
        if (Design.IsDesignMode) return;

        ViewModel = new UserSettingsDialogViewModel(new UserSettings());
    }

    public UserSettingsDialog(UserSettingsDialogViewModel vm) : this()
    {
        ViewModel = vm;
    }

    private async void ButtonConfirm_Click(object? sender, RoutedEventArgs e)
    {
        Close(ViewModel?.ToModel());
    }

    private void ButtonClose_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private async void SelectSaveDataDir_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var storageProvider = GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null) return;

        var folder = await storageProvider.TryGetFolderFromPathAsync(
            ViewModel.SaveDataDir.Length > 0 ? ViewModel.SaveDataDir : Path.GetFullPath("."));

        var dirs = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "请选择SaveData文件夹",
            SuggestedStartLocation = folder,
            SuggestedFileName = "SaveData",
            AllowMultiple = false
        });

        if (dirs.Count == 0) return;

        var dir = dirs[0].Path.LocalPath;
        ViewModel.SaveDataDir = dir;
    }
}

public class UserSettingsDialogViewModel : ViewModelBase
{
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

    public int XOffset
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    private void FromModel(UserSettings userSettings)
    {
        SaveDataDir = userSettings.SaveDataDir;
        AutoUpload = userSettings.AutoUpload;
        AutoDownload = userSettings.AutoDownload;
        XOffset = userSettings.XOffset;
    }

    public UserSettings ToModel()
    {
        return new UserSettings(SaveDataDir, AutoUpload, AutoDownload, XOffset);    
    }

    public UserSettingsDialogViewModel(UserSettings userSettings)
    {
        FromModel(userSettings);
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using San11PVPToolShared.Models;

namespace San11PVPToolClient.Dialogs;

public partial class RoomConfigDialog : ReactiveWindow<RoomConfigDialogViewModel>
{
    public RoomConfigDialog()
    {
        InitializeComponent();
        if (Design.IsDesignMode) return;

        ViewModel = new RoomConfigDialogViewModel();
    }

    public RoomConfigDialog(RoomConfigDialogViewModel vm) : this()
    {
        ViewModel = vm;
    }

    private async void ButtonConfirm_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        ViewModel.RoomName = ViewModel.RoomName.Trim();
        Close(ViewModel.RoomConfig);
    }

    private void ButtonClose_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}

public class RoomConfigDialogViewModel : ReactiveValidationObject
{
    public RoomConfig RoomConfig
    {
        get => new(RoomName, Password, MaxPlayers);
        set
        {
            RoomName = value.RoomName;
            Password = value.Password ?? "";
            MaxPlayers = value.MaxPlayers;
        }
    }

    public string RoomName
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public string Password
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public int MaxPlayers
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public RoomConfigDialogViewModel(RoomConfig? config = null)
    {
        RoomConfig = config ?? new("", null, 4);

        this.ValidationRule(
            vm => vm.RoomName,
            name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length is >= 1 and <= 16,
            "房间名长度应在1~16字符");

        this.ValidationRule(
            vm => vm.Password,
            pw => string.IsNullOrWhiteSpace(pw) || pw.Length is >= 4 and <= 8,
            "密码只能留空或4-8位");
    }
}

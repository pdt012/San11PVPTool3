using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using ReactiveUI;
using ReactiveUI.Validation.Helpers;

namespace San11PVPToolClient.Dialogs;

public partial class TextBoxDialog : ReactiveWindow<TextBoxDialogViewModel>
{
    public TextBoxDialog()
    {
        InitializeComponent();
        if (Design.IsDesignMode) return;

        ViewModel = new TextBoxDialogViewModel();
    }

    public TextBoxDialog(TextBoxDialogViewModel vm) : this()
    {
        ViewModel = vm;
    }

    private async void ButtonConfirm_Click(object? sender, RoutedEventArgs e)
    {
        Close(ViewModel?.Text ?? "");
    }

    private void ButtonClose_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}

public class TextBoxDialogViewModel : ReactiveValidationObject
{
    public string Message
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public string? Text
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public bool IsPassword
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;

    public char PasswordChar
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
}

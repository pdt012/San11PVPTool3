using System.Diagnostics;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using San11PVPToolClient.ViewModels;

namespace San11PVPToolClient.Views;

public partial class AboutView : ReactiveUserControl<AboutViewModel>
{
    public AboutView()
    {
        InitializeComponent();
    }

    private void GitHub_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://www.github.com/pdt012/San11PVPTool3",
            UseShellExecute = true
        });
    }
}

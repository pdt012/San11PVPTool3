using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ReactiveUI;
using San11PVPToolClient.Dialogs;
using San11PVPToolClient.ViewModels;
using San11PVPToolShared.Models;

namespace San11PVPToolClient.Views;

public partial class LobbyView : ReactiveUserControl<LobbyViewModel>
{
    public LobbyView()
    {
        InitializeComponent();
        if (Design.IsDesignMode) return;

        this.WhenActivated(disposables =>
        {
            ViewModel!.SetRoomConfigInteraction.RegisterHandler(async interaction =>
            {
                var dialog = new RoomConfigDialog(
                    new RoomConfigDialogViewModel(new RoomConfig("联机房间", null, 4)))
                {
                    Title = "房间信息设置", WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                var result = await dialog.ShowDialog<RoomConfig?>(TopLevel.GetTopLevel(this) as Window);
                interaction.SetOutput(result);
            }).DisposeWith(disposables);

            ViewModel!.InputPasswordInteraction.RegisterHandler(async interaction =>
            {
                var dialog =
                    new TextBoxDialog(new TextBoxDialogViewModel { Message = "请输入密码：", IsPassword = true })
                    {
                        Title = "输入密码"
                    };

                var result = await dialog.ShowDialog<string?>(TopLevel.GetTopLevel(this) as Window);
                interaction.SetOutput(result);
            }).DisposeWith(disposables);
        });
    }
}

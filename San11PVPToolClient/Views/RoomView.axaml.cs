using System.Collections.Specialized;
using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using ReactiveUI;
using San11PVPToolClient.Dialogs;
using San11PVPToolClient.ViewModels;
using San11PVPToolShared.Models;

namespace San11PVPToolClient.Views;

public partial class RoomView : ReactiveUserControl<RoomViewModel>
{
    public RoomView()
    {
        InitializeComponent();
        if (Design.IsDesignMode) return;

        this.WhenActivated(disposables =>
        {
            if (ViewModel != null)
            {
                ViewModel.Messages.CollectionChanged += MessagesChanged;

                Disposable.Create(() =>
                {
                    ViewModel.Messages.CollectionChanged -= MessagesChanged;
                }).DisposeWith(disposables);
            }

            ViewModel!.SetRoomConfigInteraction.RegisterHandler(async interaction =>
            {
                var dialog = new RoomConfigDialog
                {
                    Title = "房间信息修改",
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ViewModel = new RoomConfigDialogViewModel(ViewModel.RoomInfo?.Config)
                };

                var result = await dialog.ShowDialog<RoomConfig?>(TopLevel.GetTopLevel(this) as Window);
                interaction.SetOutput(result);
            }).DisposeWith(disposables);

            ViewModel!.SetKingNameInteraction.RegisterHandler(async interaction =>
            {
                var player = interaction.Input;
                var dialog =
                    new TextBoxDialog(new TextBoxDialogViewModel { Message = "输入君主名：", Text = player.KingName })
                    {
                        Title = "重设君主名"
                    };

                var result = await dialog.ShowDialog<string?>(TopLevel.GetTopLevel(this) as Window);
                interaction.SetOutput(result);
            }).DisposeWith(disposables);
        });
    }

    private void MessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            Dispatcher.UIThread.Post(() =>
            {
                // 每次新增消息时自动滚动到最下方
                ChatScrollViewer?.ScrollToEnd();
            });
    }
}

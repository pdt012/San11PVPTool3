using System;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using San11PVPToolClient.Dialogs;
using San11PVPToolClient.Models;
using San11PVPToolClient.ViewModels;

namespace San11PVPToolClient.Views;

public partial class MainWindow : ReactiveWindow<MainViewModel>
{
    private bool _isHidden = false;
    private bool _isHover = false;

    private const int EdgeThreshold = 5; // 判定贴边距离
    private const int PeekSize = 5; // 露出像素

    private CancellationTokenSource? _animationCts;

    private int XOffset => ViewModel?.UserSettingsService.Settings.XOffset ?? 0;

    public MainWindow()
    {
        InitializeComponent();

        Closing += OnClosing;
        PositionChanged += OnPositionChanged;
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;

        this.WhenActivated(disposables =>
        {
            ViewModel!.ShowMsgBoxInteraction.RegisterHandler(async interaction =>
            {
                var boxParams = interaction.Input;
                boxParams.Topmost = true;
                var result = ButtonResult.None;

                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var box = MessageBoxManager.GetMessageBoxStandard(boxParams);
                    result = await box.ShowWindowDialogAsync(this);
                    interaction.SetOutput(result);
                });
            });
            
            ViewModel!.OpenSettingsInteraction.RegisterHandler(async interaction =>
            {
                var dialog = new UserSettingsDialog()
                {
                    Title = "设置",
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ViewModel = new UserSettingsDialogViewModel(interaction.Input)
                };

                var result = await dialog.ShowDialog<UserSettings?>(this);
                interaction.SetOutput(result);
            }).DisposeWith(disposables);
        });
    }
    
    private bool _isCheckingClose;
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isCheckingClose) return;

        e.Cancel = true; // 先阻止关闭
        _isCheckingClose = true;
        
        if (ViewModel == null) return;
        
        _ = CheckCanCloseAsync();
        return;

        async Task CheckCanCloseAsync()
        {
            try
            {
                var canClose = await ViewModel.CanCloseAsync();
                if (canClose)
                    Close();
            }
            finally
            {
                _isCheckingClose = false;
            }
        }
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        // var screen = Screens.ScreenFromWindow(this);
        // if (screen == null) return;
        // var work = screen.WorkingArea;
        //
        // var canHide = CanHideToLeft(work) || CanHideToRight(work);
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        // Console.WriteLine($"Enter hidden={_isHidden}");
        _isHover = true;
        if (!_isHidden) return;

        var screen = Screens.ScreenFromWindow(this);
        if (screen == null) return;

        var work = screen.WorkingArea;

        // 恢复位置
        if (Position.X < work.X)
        {
            // Console.WriteLine($"->, {Position}");
            var showPos = new PixelPoint(work.X + XOffset, Position.Y);
            _isHidden = false;
            AnimateMoveTo(showPos);
        }
        else if (Position.X + WidthPx > work.Right)
        {
            var showPos = new PixelPoint(work.Right - WidthPx + XOffset, Position.Y);
            _isHidden = false;
            AnimateMoveTo(showPos);
        }
    }

    private async void OnPointerExited(object? sender, PointerEventArgs e)
    {
        // Console.WriteLine($"Exit hidden={_isHidden}");
        _isHover = false;
        if (_isHidden) return;

        var screen = Screens.ScreenFromWindow(this);
        if (screen == null) return;
        var work = screen.WorkingArea;

        await Task.Delay(1000);
        if (_isHover) return;

        // 左贴边
        if (CanHideToLeft(work))
        {
            // Console.WriteLine($"<-, {Position}");
            var newPos = new PixelPoint(work.X - WidthPx + PeekSize + XOffset, Position.Y);
            _isHidden = true;
            await AnimateMoveTo(newPos, 1000);
        }
        else if (CanHideToRight(work))
        {
            var newPos = new PixelPoint(work.Right - PeekSize + XOffset, Position.Y);
            _isHidden = true;
            await AnimateMoveTo(newPos, 1000);
        }
    }

    private bool CanHideToLeft(PixelRect work) =>
        WindowState == WindowState.Normal && Position.X - (work.X + XOffset) <= EdgeThreshold;

    private bool CanHideToRight(PixelRect work) =>
        WindowState == WindowState.Normal && work.Right + XOffset - (Position.X + WidthPx) <= EdgeThreshold;

    private int WidthPx => (int)(Width * RenderScaling);

    private Task AnimateMoveTo(PixelPoint target, int durationMs = 500)
    {
        if (_animationCts != null)
        {
            _animationCts.Cancel();
            _animationCts.Dispose();
        }

        var cts = new CancellationTokenSource();
        _animationCts = cts;

        var tcs = new TaskCompletionSource();

        var start = Position;
        var dx = target.X - start.X;
        var dy = target.Y - start.Y;

        var startTime = DateTime.UtcNow;
        var duration = TimeSpan.FromMilliseconds(durationMs);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += OnTick;
        timer.Start();

        return tcs.Task;

        void OnTick(object? sender, EventArgs e)
        {
            if (cts.IsCancellationRequested)
            {
                // Console.WriteLine($"cancelled {Position}");
                timer.Stop();
                timer.Tick -= OnTick;
                return;
            }

            var elapsed = DateTime.UtcNow - startTime;
            double t = elapsed.TotalMilliseconds / duration.TotalMilliseconds;

            if (t >= 1)
            {
                // Console.WriteLine($"finished {Position}");
                Position = target;
                timer.Stop();
                timer.Tick -= OnTick;
                tcs.SetResult();
                return;
            }

            t = EaseOutCubic(t);

            int x = (int)(start.X + dx * t);
            int y = (int)(start.Y + dy * t);

            Position = new PixelPoint(x, y);
        }

        // 缓动函数
        static double EaseOutCubic(double t)
        {
            return 1 - Math.Pow(1 - t, 3);
        }
    }
}

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using NLog;

namespace San11PVPToolClient.Services;

public static class GlobalExceptionHandler
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();
    private static bool s_isHandlingException = false;

    public static void InitUiHandlers()
    {
        // UI线程异常
        Dispatcher.UIThread.UnhandledException += (s, e) =>
        {
            e.Handled = true;
            HandleException(e.Exception);
        };
    }

    public static void InitNonUiHandlers()
    {
        // Task 异常
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            e.SetObserved();
            HandleException(e.Exception);
        };

        // 其他异常
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleException(ex);
            }
            else
            {
                s_logger.Error($"未知异常对象: {e.ExceptionObject}");
            }
        };
    }

    private static void HandleException(Exception? ex)
    {
        if (ex == null) return;
        s_logger.Error(ex);

        if (s_isHandlingException)
            return;

        s_isHandlingException = true;

        // 备份当前日志文件
        try
        {
            var logFile = "logs/pvp-tool.log";
            if (System.IO.File.Exists(logFile))
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFile = $"logs/pvp-tool_{timestamp}.log.bak";
                System.IO.File.Copy(logFile, backupFile, true);
            }
        }
        catch (Exception backupEx)
        {
            s_logger.Error(backupEx, "备份日志文件失败");
        }

        Dispatcher.UIThread.Post(async () =>
        {
            var lifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime;
            if (lifetime == null) return;
            try
            {
                var window = lifetime.MainWindow;
                if (window != null)
                {
                    var boxParams = new MessageBoxStandardParams
                    {
                        ContentTitle = "联机工具发生异常",
                        ContentMessage = $"联机工具发生异常，请查看日志。\n{ex}",
                        Icon = Icon.Error,
                        ButtonDefinitions = ButtonEnum.Ok,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        MaxHeight = 600
                    };
                    var box = MessageBoxManager.GetMessageBoxStandard(boxParams);
                    await box.ShowWindowDialogAsync(window);
                }

                lifetime.Shutdown();
            }
            catch
            {
                lifetime?.Shutdown();
            }
        });
    }
}

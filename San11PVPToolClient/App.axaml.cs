using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NLog;
using NLog.Config;
using NLog.Targets;
using San11PVPToolClient.Services;
using San11PVPToolClient.ViewModels;
using San11PVPToolClient.Views;

namespace San11PVPToolClient;

public partial class App : Application
{
    public override void Initialize()
    {
        InitLogConfig();
        GlobalExceptionHandler.InitNonUiHandlers();
        
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = new MainViewModel(), };
        }

        GlobalExceptionHandler.InitUiHandlers();

        base.OnFrameworkInitializationCompleted();
    }
    
    private static void InitLogConfig()
    {
        var config = new LoggingConfiguration();

        var fileTarget = new FileTarget("file")
        {
            FileName = "logs/pvp-tool.log",
            Layout = "[${longdate}] [${level:uppercase=true}] <${logger}> ${message} ${exception:format=tostring}",
            DeleteOldFileOnStartup = true
        };

        config.AddTarget(fileTarget);
        config.AddRule(LogLevel.Info, LogLevel.Fatal, fileTarget);
        
        #if DEBUG
        var consoleTarget = new ConsoleTarget("console")
        {
            Layout = "[${longdate}] [${level:uppercase=true}] ${message} ${exception:format=tostring}"
        };
        config.AddTarget(consoleTarget);
        config.AddRule(LogLevel.Debug, LogLevel.Fatal, consoleTarget);
        #endif

        LogManager.Configuration = config;
    }
}

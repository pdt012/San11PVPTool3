using System.Text;
using NLog;
using NLog.Config;
using NLog.Targets;
using San11PVPToolServer.ServerWebSocket;
using San11PVPToolServer.Services;
using San11PVPToolShared.Utils;
using LogLevel = NLog.LogLevel;

namespace San11PVPToolServer;

public class Program
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();
    
    public static void Main(string[] args)
    {
        InitLogConfig();

        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseWebSockets();

        app.MapControllers();

        app.Map("/ws", WebSocketHandler.Handle);

        // 加载编码
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            PK22CodeConverter.Init("enc_3.xml");
        }
        catch (Exception ex)
        {
            s_logger.Warn($"PK2.2码表初始化失败. {ex}");
        }

        // 注册循环检查
        RegisterLoops(app);

        app.Run();
    }

    private static void RegisterLoops(WebApplication app)
    {
        var cts = new CancellationTokenSource();
        app.Lifetime.ApplicationStopping.Register(() =>
        {
            cts.Cancel();
        });

        _ = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    await RoomManager.EventActor.Enqueue(() =>
                    {
                        WebSocketHandler.CheckHeartbeat();
                        return Task.CompletedTask;
                    });
                }
                catch (Exception ex)
                {
                    s_logger.Error($"CheckHeartbeat loop: {ex}");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), cts.Token);
            }
        });

        _ = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    await RoomManager.EventActor.Enqueue(() =>
                    {
                        RoomManager.RemoveTimeoutPlayers();
                        return Task.CompletedTask;
                    });
                }
                catch (Exception ex)
                {
                    s_logger.Error($"RemoveTimeoutPlayers loop: {ex}");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), cts.Token);
            }
        });
        
        // _ = Task.Run(async () =>
        // {
        //     while (!cts.Token.IsCancellationRequested)
        //     {
        //         try
        //         {
        //             await RoomManager.EventActor.Enqueue(() =>
        //             {
        //                 RoomManager.CleanupRooms();
        //                 return Task.CompletedTask;
        //             });
        //         }
        //         catch (Exception ex)
        //         {
        //             s_logger.Error($"CleanupRooms loop: {ex}");
        //         }
        //
        //         await Task.Delay(TimeSpan.FromSeconds(60), cts.Token);
        //     }
        // });
    }

    private static void InitLogConfig()
    {
        var config = new LoggingConfiguration();

        // 控制台目标
        var consoleTarget = new ConsoleTarget("console")
        {
            Layout = "[${longdate}] [${level:uppercase=true}] ${message} ${exception:format=tostring}"
        };

        // 文件目标
        var fileTarget = new FileTarget("file")
        {
            FileName = "logs/pvp-server.log",
            Layout = "[${longdate}] [${level:uppercase=true}] <${logger}> ${message} ${exception:format=tostring}",
        };

        // 添加 target
        config.AddTarget(consoleTarget);
        config.AddTarget(fileTarget);

        // 规则：日志同时写入控制台和文件
        config.AddRule(LogLevel.Debug, LogLevel.Fatal, consoleTarget);
        config.AddRule(LogLevel.Info, LogLevel.Fatal, fileTarget);

        // 应用配置
        LogManager.Configuration = config;
    }
}

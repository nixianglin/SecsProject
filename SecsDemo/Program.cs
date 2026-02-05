using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SecsCore;
using SecsCore.Transport;
using Serilog;
using static SecsCore.Sml;

// 1. 加载 appsettings.json 配置
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// 2. 初始化 Serilog (同时输出到 Console 和 File)
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration) // 读取 JSON 中的 Serilog 节点
    .CreateLogger();

// 创建 Microsoft.Extensions.Logging 的工厂，把 Serilog 接进来
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSerilog(); // 桥接
});
var logger = loggerFactory.CreateLogger("SecsHost");

logger.LogInformation("=== SecsCore Production Host Starting ===");

try
{
    // 3. 绑定配置对象 (自动把 JSON 里的 "Secs" 节点映射到 SecsOptions 类)
    var secsOptions = new SecsOptions();
    configuration.GetSection("Secs").Bind(secsOptions);

    logger.LogInformation("Loaded Configuration: Mode={Mode}, Remote={Ip}:{Port}, DeviceId={DeviceId}",
        secsOptions.Mode, secsOptions.IpAddress, secsOptions.Port, secsOptions.DeviceId);

    // 4. 启动驱动
    using var connection = new HsmsConnection(secsOptions, logger);

    // 挂载业务逻辑 (S1F13 自动回复)
    connection.OnPrimaryMessageReceived += async msg =>
    {
        if (msg.Header.Stream == 1 && msg.Header.Function == 13)
        {
            logger.LogInformation("Auto-Replying to S1F13 (Comm Request)");
            var replyBody = L(B(0), L(A("SecsCore"), A("Prod-1.0")));
            await connection.SendReplyAsync(new SecsMessage(1, 14, false, replyBody), msg);
        }
    };

    connection.Start();

    logger.LogInformation("Driver started. Press Ctrl+C to exit.");

    // 5. 阻塞主线程，直到手动退出
    // 在实际 Windows 服务或 Linux Daemon 中，这里会是 await Task.Delay(-1) 或 Semaphore
    var exitEvent = new ManualResetEvent(false);
    Console.CancelKeyPress += (sender, eventArgs) => {
        eventArgs.Cancel = true;
        exitEvent.Set();
    };
    exitEvent.WaitOne();

    logger.LogInformation("Stopping driver...");
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Fatal application error");
}
finally
{
    Log.CloseAndFlush(); // 确保所有日志都写进文件
}
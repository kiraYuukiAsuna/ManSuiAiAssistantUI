using Serilog;

namespace MyElysiaRunner;

public static class Util
{
    public static Serilog.ILogger LoggerBertVits = new LoggerConfiguration().WriteTo.Console()
        .WriteTo.File("LogsBertVits/Log.txt", rollingInterval: RollingInterval.Day)
        .CreateLogger();

    public static Serilog.ILogger LoggerVoiceInputServer = new LoggerConfiguration().WriteTo.Console()
        .WriteTo.File("LogsVoiceInputServer/Log.txt", rollingInterval: RollingInterval.Day)
        .CreateLogger();

    public static Serilog.ILogger LoggerVoiceInputClient = new LoggerConfiguration().WriteTo.Console()
        .WriteTo.File("LogsVoiceInputClient/Log.txt", rollingInterval: RollingInterval.Day)
        .CreateLogger();
}
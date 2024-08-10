using Avalonia;
using ShowMeTheXaml;
using System;
using System.Diagnostics;
using System.Linq;
using Avalonia.Dialogs;
using MyElysiaUI.ControlsLibrary.Dialog;
using Serilog;
using SukiUI.Controls;

namespace MyElysiaUI;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "An unhandled exception occurred." +
                         "Please report this error to the developers.");

            Log.Information("关闭所有Python进程开始...");
            try
            {
                // 获取所有正在运行的进程
                Process[] allProcesses = Process.GetProcesses();

                // 查找所有名为 "python" 的进程
                var pythonProcesses = allProcesses.Where(p => p.ProcessName.ToLower().Contains("python"));

                // 逐个关闭这些进程
                foreach (var process in pythonProcesses)
                {
                    Console.WriteLine($"Killing process {process.ProcessName} with ID {process.Id}");
                    process.Kill();
                    process.WaitForExit(); // 等待进程完全退出
                }

                Log.Information("All Python processes have been terminated.");
            }
            catch (Exception ex)
            {
                SukiHost.ShowDialog(new StandardDialog("关闭Python进程失败! 请重新打开程序并手动检查关闭任务管理器中是否存在未关闭的Python进程！", "确定"));
                Log.Error($"An error occurred: {ex.Message}");
            }

            Log.Information("关闭所有Python进程结束...");
        }
        finally
        {
            Log.CloseAndFlush();
            Environment.Exit(-1);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("Logs/Log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var app = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseXamlDisplay();

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            app.UseManagedSystemDialogs();
        return app;
    }
}
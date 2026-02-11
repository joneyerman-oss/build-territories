using System.IO;
using System.Windows;
using Serilog;

namespace TerritoryBuilder.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        Directory.CreateDirectory("logs");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine("logs", "territorybuilder-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

using System.IO;
using System.Windows;
using PitWall.Contracts;
using Serilog;

namespace PitWall.Host;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var logDir = ProjectPaths.LogsDirectory;
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logDir, "pitwall-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: Constants.LogRetainedDays)
            .CreateLogger();

        Log.Information("PitWall Host starting");
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI exception");
            args.Handled = true;
            MessageBox.Show(args.Exception.Message, "PitWall", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("PitWall Host exiting");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

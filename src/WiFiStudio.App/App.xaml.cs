using Microsoft.UI.Xaml;

namespace WiFiStudio.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        WriteCrashLog($"XAML unhandled: {e.Message}{Environment.NewLine}{e.Exception}");
    }

    private static void OnCurrentDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        WriteCrashLog($"AppDomain unhandled: {e.ExceptionObject}");
    }

    private static void WriteCrashLog(string message)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WiFiStudioPro");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "crash.log"), $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Last-chance logging should never create another startup failure.
        }
    }
}

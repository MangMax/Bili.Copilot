using Microsoft.UI.Xaml;
namespace BiliCopilot.UI;
public partial class App : Application
{
    public static string LoggerFolder => Microsoft.Windows.Storage.ApplicationData.GetDefault().LocalFolder.Path;
    public App() { UnhandledException += (_, e) => { System.IO.File.AppendAllText(System.IO.Path.Combine(LoggerFolder,"startup-errors.txt"),e.Exception.ToString()); e.Handled=true; }; InitializeComponent(); }
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        GlobalDependencies.Initialize();
        Offline48.Start();
    }
}

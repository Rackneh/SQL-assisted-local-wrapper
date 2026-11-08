using System.Windows;
using DoryNative.Services;
using DoryNative.Views;
using DoryNative.XpSim;
using WLMRevivalV2Desktop.Protocol;

namespace DoryNative;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static BackendApi Backend { get; private set; } = null!;
    public static MessengerService Messenger { get; private set; } = null!;
    public static XpBootFlow BootFlow { get; private set; } = null!;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        Backend = new BackendApi();
        Messenger = new MessengerService(Backend);
        Messenger.Start();

        BootFlow = new XpBootFlow(Backend);
        var shell = new StudioShell(Backend, BootFlow);
        MainWindow = shell;
        shell.Show();
        // PC starts powered off (like the web GUI); user powers on via beige PC button.
    }

    /// <summary>
    /// V2 compatibility: re-open the main WLM window (used when the UI style
    /// or scene changes in Options). We only ship the Live skin, so this just
    /// ensures a MainWindowLive exists and is focused.
    /// </summary>
    public void ReopenMainWindow()
    {
        // WLM windows live inside the XP sim — never show a top-level window.
        WlmSim.WlmSimHost.OpenMainWindow();
    }
}

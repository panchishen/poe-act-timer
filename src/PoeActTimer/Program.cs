using System.Globalization;
using PoeActTimer.UI;

namespace PoeActTimer;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "PoeActTimer.SingleInstance", out bool first);
        if (!first) return;

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => AppController.LogError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppController.LogMessage("Unhandled: " + e.ExceptionObject);
        ApplicationConfiguration.Initialize();

        AppController controller;
        try
        {
            controller = new AppController();
        }
        catch (Exception ex)
        {
            // The controller already disposed its tray icon, overlay and hotkeys.
            AppController.LogError(ex);
            try
            {
                var strings = Strings.For(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en");
                MessageBox.Show(strings.StartFailed, "PoE Act Timer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception)
            {
            }
            return;
        }
        Application.Run();
        GC.KeepAlive(controller);
    }
}

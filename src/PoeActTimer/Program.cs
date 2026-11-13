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
            AppController.LogError(ex);
            return;
        }
        Application.Run();
        GC.KeepAlive(controller);
    }
}

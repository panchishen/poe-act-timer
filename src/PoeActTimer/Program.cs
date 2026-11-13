namespace PoeActTimer;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "PoeActTimer.SingleInstance", out bool first);
        if (!first) return;
        ApplicationConfiguration.Initialize();
        var controller = new AppController();
        Application.Run();
        GC.KeepAlive(controller);
    }
}

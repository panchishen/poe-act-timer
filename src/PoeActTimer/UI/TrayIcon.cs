using System.Drawing.Drawing2D;
using PoeActTimer.Core;

namespace PoeActTimer.UI;

public sealed class TrayIcon : IDisposable
{
    readonly NotifyIcon icon = new();
    readonly IntPtr handle;
    readonly Icon image;
    readonly ContextMenuStrip menu = new();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);
    readonly ToolStripMenuItem pause = new(), startNow = new(), reset = new(), history = new(), chooseLog = new(), exit = new();

    public TrayIcon(Action onPause, Action onStartNow, Action onReset, Action onHistory, Action onChooseLog, Action onExit)
    {
        pause.Click += (_, _) => onPause();
        startNow.Click += (_, _) => onStartNow();
        reset.Click += (_, _) => onReset();
        history.Click += (_, _) => onHistory();
        chooseLog.Click += (_, _) => onChooseLog();
        exit.Click += (_, _) => onExit();

        (image, handle) = CreateImage();
        menu.Items.AddRange(new ToolStripItem[]
        {
            pause, startNow, reset, new ToolStripSeparator(), history, chooseLog, new ToolStripSeparator(), exit,
        });
        icon.ContextMenuStrip = menu;
        icon.Icon = image;
        icon.Visible = true;
    }

    public void Update(Strings s, TimerStatus status, bool manualPause)
    {
        Set(pause, manualPause ? s.TrayResume : s.TrayPause);
        Set(startNow, s.TrayStartNow);
        Set(reset, s.TrayReset);
        Set(history, s.TrayHistory);
        Set(chooseLog, s.TrayChooseLog);
        Set(exit, s.TrayExit);
        var tip = "PoE Act Timer — " + s.StatusText(status);
        tip = tip.Length > 63 ? tip[..63] : tip;
        if (icon.Text != tip) icon.Text = tip;
    }

    static void Set(ToolStripMenuItem item, string text)
    {
        if (item.Text != text) item.Text = text;
    }

    static (Icon, IntPtr) CreateImage()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var fill = new SolidBrush(ColorTranslator.FromHtml("#2C2C2A"));
            using var pen = new Pen(ColorTranslator.FromHtml("#97C459"), 3);
            g.FillEllipse(fill, 1, 1, 30, 30);
            g.DrawEllipse(pen, 3, 3, 26, 26);
            g.DrawLine(pen, 16, 16, 16, 8);
            g.DrawLine(pen, 16, 16, 22, 16);
        }
        var hicon = bitmap.GetHicon();
        return (Icon.FromHandle(hicon), hicon);
    }

    public void Dispose()
    {
        icon.Visible = false;
        icon.Dispose();
        image.Dispose();
        DestroyIcon(handle);
        menu.Dispose();
    }
}

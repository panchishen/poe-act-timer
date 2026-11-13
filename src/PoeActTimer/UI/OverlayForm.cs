using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using PoeActTimer.Core;

namespace PoeActTimer.UI;

public sealed class OverlayForm : Form
{
    const int WsExLayered = 0x80000, WsExToolWindow = 0x80, WsExNoActivate = 0x8000000, WsExTopMost = 0x8;
    const int WmMouseActivate = 0x21, MaNoActivate = 3;
    const int LogicalWidth = 240, CompactHeight = 94;
    const uint SwpNoSize = 0x1, SwpNoMove = 0x2, SwpNoActivate = 0x10;
    const int UlwAlpha = 2;
    const byte AcSrcOver = 0, AcSrcAlpha = 1;
    static readonly IntPtr HwndTopMost = new(-1), HwndNoTopMost = new(-2);

    static readonly Color TextMain = ColorTranslator.FromHtml("#F1EFE8");
    static readonly Color TextMuted = ColorTranslator.FromHtml("#888780");
    static readonly Color TextRow = ColorTranslator.FromHtml("#B4B2A9");
    static readonly Color TextFaint = ColorTranslator.FromHtml("#5F5E5A");
    static readonly Color LinkHover = ColorTranslator.FromHtml("#D3D1C7");
    static readonly Color Hairline = Color.FromArgb(36, 255, 255, 255);
    static readonly StringFormat Typo = CreateTypo();

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref NativePoint pptDst, ref NativeSize psize,
        IntPtr hdcSrc, ref NativePoint pprSrc, int crKey, ref BlendFunction pblend, int dwFlags);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct NativeSize { public int Cx, Cy; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BlendFunction { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    readonly FontSet fonts = new();
    OverlayView? view;
    Strings? strings;
    IReadOnlyList<(string Label, string Combo)> hints = [];
    bool expanded, hoverLink, hoverPause, dragging;
    bool? topMost;
    RectangleF pauseRect, linkRect;
    Point dragOffset;

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
    }

    public event Action? PauseClicked;
    public event Action? ExpandToggled;
    public event Action<Point>? Moved;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double BackgroundOpacity { get; set; } = 0.68;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Expanded
    {
        get => expanded;
        set { expanded = value; Render(); }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExLayered | WsExToolWindow | WsExNoActivate | WsExTopMost;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmMouseActivate) { m.Result = MaNoActivate; return; }
        base.WndProc(ref m);
    }

    public void Present(OverlayView newView, Strings newStrings, IReadOnlyList<(string Label, string Combo)> newHints)
    {
        view = newView;
        strings = newStrings;
        hints = newHints;
        Render();
    }

    public void SetTopMost(bool on)
    {
        if (topMost == on || !IsHandleCreated) return;
        topMost = on;
        SetWindowPos(Handle, on ? HwndTopMost : HwndNoTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    float DpiScale => DeviceDpi / 96f;

    void Render()
    {
        if (view is null || strings is null || !IsHandleCreated) return;
        float k = DpiScale;
        float height = expanded ? 340 + 16 * hints.Count : CompactHeight;
        using var bitmap = new Bitmap((int)Math.Ceiling(LogicalWidth * k), (int)Math.Ceiling(height * k), PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.ScaleTransform(k, k);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            Draw(g, LogicalWidth, height, view, strings);
        }
        Size = bitmap.Size;
        Push(bitmap);
    }

    void Draw(Graphics g, float w, float h, OverlayView v, Strings s)
    {
        using (var body = RoundRect(new RectangleF(0.5f, 0.5f, w - 1, h - 1), 8))
        using (var fill = new SolidBrush(Color.FromArgb((int)(BackgroundOpacity * 255), 10, 10, 10)))
        using (var border = new Pen(Hairline, 1))
        {
            g.FillPath(fill, body);
            g.DrawPath(border, body);
        }

        DrawText(g, TimeFormat.Format(v.Total), fonts.MonoTotal, TextMain, 12, 10);
        if (v.CurrentAct > 0)
        {
            float x = 12 + DrawText(g, s.Act(v.CurrentAct) + " · ", fonts.Ui13, TextMuted, 12, 44);
            if (v.Status == TimerStatus.Finished) DrawText(g, s.ActDone, fonts.Ui13, TextMuted, x, 44);
            else DrawText(g, TimeFormat.Format(v.ActTimes[v.CurrentAct]), fonts.Mono13, TextMuted, x, 44);
        }

        bool showPause = v.Status is not (TimerStatus.NoRun or TimerStatus.Finished);
        pauseRect = showPause ? new RectangleF(w - 12 - 44, 14, 44, 44) : RectangleF.Empty;
        if (showPause) DrawPauseButton(g, pauseRect, v.ManualPause);

        var (dot, text) = StatusColors(v.Status);
        using (var dotBrush = new SolidBrush(dot)) g.FillEllipse(dotBrush, 12, 74, 8, 8);
        DrawText(g, v.LogMissing ? s.LogMissing : s.StatusText(v.Status), fonts.Ui12, text, 26, 70);

        var link = expanded ? s.Less : s.More;
        var linkFont = hoverLink ? fonts.Ui12Underline : fonts.Ui12;
        float linkWidth = Measure(g, link, linkFont);
        linkRect = new RectangleF(w - 12 - linkWidth, 68, linkWidth, 18);
        DrawText(g, link, linkFont, hoverLink ? LinkHover : TextMuted, linkRect.X, 70);

        if (!expanded) return;

        using var separator = new Pen(Hairline, 1);
        g.DrawLine(separator, 12, 94, w - 12, 94);
        float y = 102;
        DrawText(g, v.CharacterName ?? "—", fonts.Ui12, TextMain, 12, y);
        y += 18;
        for (int act = 1; act <= 10; act++)
        {
            Color color;
            string value;
            if (v.Status == TimerStatus.Finished || act < v.CurrentAct) { color = TextRow; value = TimeFormat.Format(v.ActTimes[act]); }
            else if (act == v.CurrentAct) { color = TextMain; value = TimeFormat.Format(v.ActTimes[act]); }
            else { color = TextFaint; value = "—"; }
            DrawText(g, s.Act(act), fonts.Ui12, color, 12, y);
            DrawRight(g, value, value == "—" ? fonts.Ui12 : fonts.Mono12, color, w - 12, y);
            y += 18;
        }
        g.DrawLine(separator, 12, y + 2, w - 12, y + 2);
        y += 6;
        DrawText(g, s.LoadingDeducted, fonts.Ui12, TextRow, 12, y);
        DrawRight(g, TimeFormat.Format(v.Loading), fonts.Mono12, TextRow, w - 12, y);
        y += 18;
        g.DrawLine(separator, 12, y + 2, w - 12, y + 2);
        y += 8;
        foreach (var (label, combo) in hints)
        {
            DrawText(g, label, fonts.Ui11, TextFaint, 12, y);
            DrawRight(g, combo, fonts.Mono11, TextFaint, w - 12, y);
            y += 16;
        }
    }

    void DrawPauseButton(Graphics g, RectangleF r, bool paused)
    {
        using (var path = RoundRect(r, 8))
        using (var fill = new SolidBrush(Color.FromArgb(hoverPause ? 36 : 15, 255, 255, 255)))
        using (var border = new Pen(Color.FromArgb(64, 255, 255, 255), 1))
        {
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }
        using var brush = new SolidBrush(TextMain);
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        if (paused)
        {
            g.FillPolygon(brush, new[] { new PointF(cx - 6, cy - 9), new PointF(cx - 6, cy + 9), new PointF(cx + 9, cy) });
        }
        else
        {
            g.FillRectangle(brush, cx - 7, cy - 9, 5, 18);
            g.FillRectangle(brush, cx + 2, cy - 9, 5, 18);
        }
    }

    static (Color Dot, Color Text) StatusColors(TimerStatus status) => status switch
    {
        TimerStatus.Running => (ColorTranslator.FromHtml("#97C459"), ColorTranslator.FromHtml("#C0DD97")),
        TimerStatus.Afk => (ColorTranslator.FromHtml("#EF9F27"), ColorTranslator.FromHtml("#FAC775")),
        TimerStatus.ManualPause => (ColorTranslator.FromHtml("#85B7EB"), ColorTranslator.FromHtml("#B5D4F4")),
        _ => (ColorTranslator.FromHtml("#888780"), ColorTranslator.FromHtml("#D3D1C7")),
    };

    static StringFormat CreateTypo()
    {
        var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        return format;
    }

    static float Measure(Graphics g, string text, Font font) => g.MeasureString(text, font, PointF.Empty, Typo).Width;

    static float DrawText(Graphics g, string text, Font font, Color color, float x, float y)
    {
        using var brush = new SolidBrush(color);
        g.DrawString(text, font, brush, x, y, Typo);
        return Measure(g, text, font);
    }

    static void DrawRight(Graphics g, string text, Font font, Color color, float right, float y) =>
        DrawText(g, text, font, color, right - Measure(g, text, font), y);

    static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    void Push(Bitmap bitmap)
    {
        IntPtr screen = GetDC(IntPtr.Zero);
        IntPtr memory = CreateCompatibleDC(screen);
        IntPtr hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
        IntPtr previous = SelectObject(memory, hBitmap);
        try
        {
            var size = new NativeSize { Cx = bitmap.Width, Cy = bitmap.Height };
            var source = new NativePoint();
            var destination = new NativePoint { X = Left, Y = Top };
            var blend = new BlendFunction { BlendOp = AcSrcOver, SourceConstantAlpha = 255, AlphaFormat = AcSrcAlpha };
            UpdateLayeredWindow(Handle, screen, ref destination, ref size, memory, ref source, 0, ref blend, UlwAlpha);
        }
        finally
        {
            SelectObject(memory, previous);
            DeleteObject(hBitmap);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    PointF Logical(Point p) => new(p.X / DpiScale, p.Y / DpiScale);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        var p = Logical(e.Location);
        if (pauseRect.Contains(p)) { PauseClicked?.Invoke(); return; }
        if (linkRect.Contains(p)) { ExpandToggled?.Invoke(); return; }
        dragging = true;
        dragOffset = e.Location;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (dragging)
        {
            var cursor = Cursor.Position;
            Location = new Point(cursor.X - dragOffset.X, cursor.Y - dragOffset.Y);
            return;
        }
        var p = Logical(e.Location);
        bool overLink = linkRect.Contains(p), overPause = pauseRect.Contains(p);
        Cursor = overLink || overPause ? Cursors.Hand : Cursors.SizeAll;
        if (overLink != hoverLink || overPause != hoverPause)
        {
            hoverLink = overLink;
            hoverPause = overPause;
            Render();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!dragging) return;
        dragging = false;
        Capture = false;
        Moved?.Invoke(Location);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (!hoverLink && !hoverPause) return;
        hoverLink = hoverPause = false;
        Render();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        Render();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) fonts.Dispose();
        base.Dispose(disposing);
    }
}

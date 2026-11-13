using System.Runtime.InteropServices;

namespace PoeActTimer.Infra;

public sealed class HotkeyManager : NativeWindow, IDisposable
{
    const int WmHotkey = 0x0312;
    const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModWin = 0x8, ModNoRepeat = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    readonly Dictionary<int, Action> actions = new();
    int nextId = 1;

    public HotkeyManager() => CreateHandle(new CreateParams());

    public bool Register(string combo, Action action)
    {
        if (!TryParse(combo, out uint modifiers, out uint key)) return false;
        int id = nextId++;
        if (!RegisterHotKey(Handle, id, modifiers | ModNoRepeat, key)) return false;
        actions[id] = action;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in actions.Keys) UnregisterHotKey(Handle, id);
        actions.Clear();
    }

    public static bool TryParse(string combo, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;
        var parts = combo.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;
        foreach (var part in parts[..^1])
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl": case "control": modifiers |= ModControl; break;
                case "alt": modifiers |= ModAlt; break;
                case "shift": modifiers |= ModShift; break;
                case "win": modifiers |= ModWin; break;
                default: return false;
            }
        }
        if (!Enum.TryParse(parts[^1], ignoreCase: true, out Keys parsed)) return false;
        key = (uint)parsed;
        return true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && actions.TryGetValue((int)m.WParam, out var action)) action();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterAll();
        DestroyHandle();
    }
}

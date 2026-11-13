using System.Diagnostics;
using System.Runtime.InteropServices;
using PoeActTimer.Core;

namespace PoeActTimer.Infra;

public sealed class GameWatcher
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LastInputInfo info);

    [StructLayout(LayoutKind.Sequential)]
    struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    HashSet<int> cachedIds = new();
    DateTime cachedAt = DateTime.MinValue;

    public GameSnapshot Snapshot()
    {
        var now = DateTime.UtcNow;
        if ((now - cachedAt).TotalSeconds >= 1)
        {
            cachedIds = GameProcessIds();
            cachedAt = now;
        }

        bool running = cachedIds.Count > 0;
        bool focused = false;
        var window = GetForegroundWindow();
        if (running && window != IntPtr.Zero)
        {
            GetWindowThreadProcessId(window, out uint pid);
            focused = cachedIds.Contains((int)pid) && !IsIconic(window);
        }
        return new GameSnapshot(running, focused, IdleTime());
    }

    public static HashSet<int> GameProcessIds()
    {
        var ids = new HashSet<int>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.ProcessName.StartsWith("PathOfExile", StringComparison.OrdinalIgnoreCase))
                    ids.Add(process.Id);
            }
        }
        return ids;
    }

    static TimeSpan IdleTime()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
        uint elapsed = unchecked((uint)Environment.TickCount - info.Time);
        return TimeSpan.FromMilliseconds(elapsed);
    }
}

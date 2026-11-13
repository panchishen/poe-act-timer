using System.Runtime.InteropServices;
using System.Text;

namespace PoeActTimer.Infra;

public static class ClientLogLocator
{
    const uint ProcessQueryLimitedInformation = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint access, bool inherit, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr handle);

    public static string? Find(string? configuredPath)
    {
        foreach (var pid in GameWatcher.GameProcessIds())
        {
            var exe = ImagePath(pid);
            if (exe is null) continue;
            var log = Path.Combine(Path.GetDirectoryName(exe)!, "logs", "Client.txt");
            if (File.Exists(log)) return log;
        }
        if (!string.IsNullOrEmpty(configuredPath) && File.Exists(configuredPath)) return configuredPath;
        return null;
    }

    static string? ImagePath(int processId)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var buffer = new StringBuilder(1024);
            int size = buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString(0, size) : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }
}

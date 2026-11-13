using System.Text;

namespace PoeActTimer.Core;

public sealed class LogTailer
{
    public LogTailer(string path, long offset)
    {
        Path = path;
        Offset = offset;
    }

    public string Path { get; }
    public long Offset { get; private set; }

    public static long CurrentLength(string path) => new FileInfo(path).Length;

    public List<string> ReadNewLines()
    {
        var lines = new List<string>();
        using var fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long length = fs.Length;
        if (length < Offset) { Offset = length; return lines; }
        if (length == Offset) return lines;

        fs.Seek(Offset, SeekOrigin.Begin);
        var buffer = new byte[length - Offset];
        int read = 0;
        while (read < buffer.Length)
        {
            int n = fs.Read(buffer, read, buffer.Length - read);
            if (n == 0) break;
            read += n;
        }

        int start = 0;
        for (int i = 0; i < read; i++)
        {
            if (buffer[i] != (byte)'\n') continue;
            lines.Add(Encoding.UTF8.GetString(buffer, start, i - start).TrimEnd('\r'));
            start = i + 1;
        }
        Offset += start;
        return lines;
    }
}

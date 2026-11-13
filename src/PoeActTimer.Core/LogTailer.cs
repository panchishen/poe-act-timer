using System.Text;

namespace PoeActTimer.Core;

public sealed class LogTailer
{
    public const int MaxChunkBytes = 4 * 1024 * 1024;

    readonly int maxChunkBytes;

    public LogTailer(string path, long offset, int maxChunkBytes = MaxChunkBytes)
    {
        if (maxChunkBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxChunkBytes));
        Path = path;
        Offset = offset;
        this.maxChunkBytes = maxChunkBytes;
    }

    public string Path { get; }
    public long Offset { get; private set; }

    public static long CurrentLength(string path) => new FileInfo(path).Length;

    /// <summary>Reads at most one chunk; returns the complete lines found in it.</summary>
    public List<string> ReadNewLines()
    {
        var lines = new List<string>();
        using var fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long length = fs.Length;
        if (length < Offset) Offset = 0; // the log was truncated or replaced: read the new file from its start
        if (length == Offset) return lines;

        int size = (int)Math.Min(length - Offset, maxChunkBytes);
        fs.Seek(Offset, SeekOrigin.Begin);
        var buffer = new byte[size];
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

        // A full chunk without any newline is a pathological line: skip it so we never get stuck.
        if (start == 0 && read == maxChunkBytes) start = read;
        Offset += start;
        return lines;
    }

    /// <summary>Reads everything currently available, chunk by chunk (for catch-up).</summary>
    public IEnumerable<string> ReadAllNewLines()
    {
        while (true)
        {
            long before = Offset;
            var lines = ReadNewLines();
            if (lines.Count == 0 && Offset == before) yield break;
            foreach (var line in lines) yield return line;
        }
    }
}

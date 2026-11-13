using System.Text;
using PoeActTimer.Core;
using Xunit;

namespace PoeActTimer.Core.Tests;

public sealed class LogTailerTests : IDisposable
{
    readonly string path = Path.Combine(Path.GetTempPath(), $"poe-tailer-{Guid.NewGuid():N}.txt");

    public void Dispose() => File.Delete(path);

    void Append(string text) => File.AppendAllText(path, text, new UTF8Encoding(false));

    [Fact]
    public void ReadsOnlyCompleteLines()
    {
        Append("first\r\nвторая\npart");
        var tailer = new LogTailer(path, 0);
        Assert.Equal(new[] { "first", "вторая" }, tailer.ReadNewLines());
        Append("ial\n");
        Assert.Equal(new[] { "partial" }, tailer.ReadNewLines());
        Assert.Empty(tailer.ReadNewLines());
        Assert.Equal(new FileInfo(path).Length, tailer.Offset);
    }

    [Fact]
    public void StartsFromGivenOffset()
    {
        Append("old\n");
        var tailer = new LogTailer(path, LogTailer.CurrentLength(path));
        Append("new\n");
        Assert.Equal(new[] { "new" }, tailer.ReadNewLines());
    }

    [Fact]
    public void HandlesTruncation()
    {
        Append("aaaaaaaaaa\n");
        var tailer = new LogTailer(path, LogTailer.CurrentLength(path));
        File.WriteAllText(path, "b\n");
        Assert.Empty(tailer.ReadNewLines());
        Assert.Equal(new FileInfo(path).Length, tailer.Offset);
        Append("c\n");
        Assert.Equal(new[] { "c" }, tailer.ReadNewLines());
    }

    [Fact]
    public void DoesNotBlockWriters()
    {
        using var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        writer.Write("line\n"u8);
        writer.Flush();
        var tailer = new LogTailer(path, 0);
        Assert.Equal(new[] { "line" }, tailer.ReadNewLines());
    }

    [Fact]
    public void ReadAllNewLinesReturnsEverythingInSmallChunks()
    {
        Append("aaa\nbbbbbb\ncc\n");
        var tailer = new LogTailer(path, 0, maxChunkBytes: 8);
        Assert.Equal(new[] { "aaa", "bbbbbb", "cc" }, tailer.ReadAllNewLines().ToList());
        Assert.Equal(new FileInfo(path).Length, tailer.Offset);
    }

    [Fact]
    public void ReadNewLinesIsBoundedAndContinues()
    {
        Append("aaa\nbbbbbb\ncc\n");
        var tailer = new LogTailer(path, 0, maxChunkBytes: 8);
        Assert.Equal(new[] { "aaa" }, tailer.ReadNewLines());
        Assert.Equal(4, tailer.Offset);
        Assert.Equal(new[] { "bbbbbb" }, tailer.ReadNewLines());
        Assert.Equal(new[] { "cc" }, tailer.ReadNewLines());
    }

    [Fact]
    public void OverlongLineIsSkippedWithoutHanging()
    {
        Append(new string('x', 20));
        var tailer = new LogTailer(path, 0, maxChunkBytes: 8);
        Assert.Empty(tailer.ReadAllNewLines().ToList());
        Assert.True(tailer.Offset > 0);
    }

    [Fact]
    public void MultibyteCharSplitAcrossWritesIsIntact()
    {
        var bytes = new UTF8Encoding(false).GetBytes("вторая\n");
        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            fs.Write(bytes, 0, 3);
        var tailer = new LogTailer(path, 0);
        Assert.Empty(tailer.ReadNewLines());
        using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            fs.Write(bytes, 3, bytes.Length - 3);
        Assert.Equal(new[] { "вторая" }, tailer.ReadNewLines());
    }
}

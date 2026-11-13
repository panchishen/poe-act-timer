using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace PoeActTimer.UI;

public sealed class FontSet : IDisposable
{
    readonly PrivateFontCollection collection = new();
    readonly List<IntPtr> memory = new();

    public FontSet()
    {
        AddResource("JetBrainsMono-Regular.ttf");
        AddResource("JetBrainsMono-Medium.ttf");
        var regular = Family("JetBrains Mono") ?? new FontFamily(GenericFontFamilies.Monospace);
        var medium = Family("JetBrains Mono Medium") ?? regular;

        MonoTotal = new Font(medium, 26, FontStyle.Regular, GraphicsUnit.Pixel);
        Mono13 = new Font(regular, 13, FontStyle.Regular, GraphicsUnit.Pixel);
        Mono12 = new Font(regular, 12, FontStyle.Regular, GraphicsUnit.Pixel);
        Mono11 = new Font(regular, 11, FontStyle.Regular, GraphicsUnit.Pixel);
        Ui13 = new Font("Segoe UI", 13, FontStyle.Regular, GraphicsUnit.Pixel);
        Ui12 = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
        Ui12Underline = new Font("Segoe UI", 12, FontStyle.Underline, GraphicsUnit.Pixel);
        Ui11 = new Font("Segoe UI", 11, FontStyle.Regular, GraphicsUnit.Pixel);
    }

    public Font MonoTotal { get; }
    public Font Mono13 { get; }
    public Font Mono12 { get; }
    public Font Mono11 { get; }
    public Font Ui13 { get; }
    public Font Ui12 { get; }
    public Font Ui12Underline { get; }
    public Font Ui11 { get; }

    FontFamily? Family(string name) => collection.Families.FirstOrDefault(f => f.Name == name);

    void AddResource(string name)
    {
        using var stream = typeof(FontSet).Assembly.GetManifestResourceStream(name);
        if (stream is null) return;
        var data = new byte[stream.Length];
        stream.ReadExactly(data);
        var pointer = Marshal.AllocCoTaskMem(data.Length);
        Marshal.Copy(data, 0, pointer, data.Length);
        collection.AddMemoryFont(pointer, data.Length);
        memory.Add(pointer);
    }

    public void Dispose()
    {
        foreach (var font in new[] { MonoTotal, Mono13, Mono12, Mono11, Ui13, Ui12, Ui12Underline, Ui11 }) font.Dispose();
        collection.Dispose();
        foreach (var pointer in memory) Marshal.FreeCoTaskMem(pointer);
    }
}

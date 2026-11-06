using SkiaSharp;

// Renders the DisplaySwitch logo into every icon asset the app and installers need.
// The glyph path is the same as Icon.Logo in src/DisplaySwitch.App/Design/Icons.axaml.

const string LogoPath = "M4.5 4.5h15A1.5 1.5 0 0 1 21 6v9a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 15V6a1.5 1.5 0 0 1 1.5-1.5z M9 20h6 M8.5 9h7l-2-2 M15.5 12h-7l2 2";
const float GridSize = 24f;
var gradientStart = SKColor.Parse("#7B6CFF");
var gradientEnd = SKColor.Parse("#3D74F5");

var root = args.Length > 0 ? args[0] : FindRepositoryRoot();

// Embedded in the app: window/taskbar icon, Linux tray icon, macOS menu bar template.
var appAssets = Path.Combine(root, "src", "DisplaySwitch.App", "Assets");
// Used only by installers and desktop integration, so they are not embedded in the app.
var packagingIcons = Path.Combine(root, "packaging", "icons");
Directory.CreateDirectory(appAssets);
Directory.CreateDirectory(packagingIcons);

int[] icoSizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
File.WriteAllBytes(Path.Combine(appAssets, "displayswitch.ico"), BuildIco([.. icoSizes.Select(size => (size, RenderBadge(size)))]));
File.WriteAllBytes(Path.Combine(appAssets, "displayswitch-64.png"), RenderBadge(64));
// macOS menu bar "template" image: black glyph on transparent, 18 pt at @2x.
File.WriteAllBytes(Path.Combine(appAssets, "tray-template.png"), RenderGlyph(36, SKColors.Black, strokeUnits: 2.1f));

foreach (var size in new[] { 256, 512 })
{
    File.WriteAllBytes(Path.Combine(packagingIcons, $"displayswitch-{size}.png"), RenderBadge(size));
}

// macOS application icon: ICNS container of PNG entries (type codes from Apple's icns format).
(string Type, int Size)[] icnsEntries = [("ic11", 32), ("ic12", 64), ("ic07", 128), ("ic08", 256), ("ic13", 256), ("ic09", 512), ("ic14", 512), ("ic10", 1024)];
File.WriteAllBytes(Path.Combine(packagingIcons, "displayswitch.icns"), BuildIcns([.. icnsEntries.Select(e => (e.Type, RenderBadge(e.Size)))]));
Console.WriteLine($"Icons written to {appAssets} and {packagingIcons}");

byte[] RenderBadge(int size)
{
    using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
    var canvas = surface.Canvas;
    canvas.Clear(SKColors.Transparent);

    // Leave a small margin at large sizes, as platform icon grids expect; fill the cell at small sizes for legibility.
    var inset = size >= 128 ? size * 0.06f : 0f;
    var rect = new SKRect(inset, inset, size - inset, size - inset);
    var radius = rect.Width * 0.225f;

    using (var fill = new SKPaint { IsAntialias = true })
    {
        fill.Shader = SKShader.CreateLinearGradient(new SKPoint(rect.Left, rect.Top), new SKPoint(rect.Right, rect.Bottom), [gradientStart, gradientEnd], SKShaderTileMode.Clamp);
        canvas.DrawRoundRect(rect, radius, radius, fill);
    }

    // Soft top highlight for depth.
    using (var highlight = new SKPaint { IsAntialias = true })
    {
        highlight.Shader = SKShader.CreateLinearGradient(new SKPoint(0, rect.Top), new SKPoint(0, rect.MidY), [new SKColor(255, 255, 255, 46), SKColors.Transparent], SKShaderTileMode.Clamp);
        canvas.DrawRoundRect(rect, radius, radius, highlight);
    }

    // Thicker strokes at small sizes keep the glyph readable in the taskbar and tray.
    var strokeUnits = size <= 24 ? 2.6f : size <= 48 ? 2.2f : 1.9f;
    DrawGlyph(canvas, rect, glyphScale: 0.64f, SKColors.White, strokeUnits);
    return Encode(surface);
}

byte[] RenderGlyph(int size, SKColor color, float strokeUnits)
{
    using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
    surface.Canvas.Clear(SKColors.Transparent);
    DrawGlyph(surface.Canvas, new SKRect(0, 0, size, size), glyphScale: 1f, color, strokeUnits);
    return Encode(surface);
}

void DrawGlyph(SKCanvas canvas, SKRect area, float glyphScale, SKColor color, float strokeUnits)
{
    using var path = SKPath.ParseSvgPathData(LogoPath);
    var scale = area.Width * glyphScale / GridSize;
    canvas.Save();
    canvas.Translate(area.MidX - (GridSize * scale / 2), area.MidY - (GridSize * scale / 2));
    canvas.Scale(scale);
    using var stroke = new SKPaint
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = strokeUnits,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
        Color = color,
    };
    canvas.DrawPath(path, stroke);
    canvas.Restore();
}

static byte[] Encode(SKSurface surface)
{
    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
}

// ICO container with PNG-compressed entries (supported since Windows Vista).
static byte[] BuildIco(IReadOnlyList<(int Size, byte[] Png)> images)
{
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);
    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)images.Count);
    var offset = 6 + (16 * images.Count);
    foreach (var (size, png) in images)
    {
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(png.Length);
        writer.Write(offset);
        offset += png.Length;
    }

    foreach (var (_, png) in images)
    {
        writer.Write(png);
    }

    return stream.ToArray();
}

// ICNS: "icns" magic and total length, then per entry a 4-byte type, length (including header) and PNG data. Big-endian.
static byte[] BuildIcns(IReadOnlyList<(string Type, byte[] Png)> entries)
{
    using var stream = new MemoryStream();
    var totalLength = 8 + entries.Sum(e => 8 + e.Png.Length);
    WriteHeader(stream, "icns", totalLength);
    foreach (var (type, png) in entries)
    {
        WriteHeader(stream, type, 8 + png.Length);
        stream.Write(png);
    }

    return stream.ToArray();

    static void WriteHeader(Stream target, string type, int length)
    {
        target.Write(System.Text.Encoding.ASCII.GetBytes(type));
        Span<byte> size = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(size, length);
        target.Write(size);
    }
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DisplaySwitch.slnx")))
    {
        directory = directory.Parent;
    }

    return directory?.FullName ?? throw new InvalidOperationException("Run from inside the repository, or pass the output folder.");
}

using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using SkiaSharp;

// Rebuild checked-in platform icons from the small, editable SVG; no platform SDK required.
// Run from the repository root. The intentionally small SVG subset rejects unsupported art.
var source = Path.GetFullPath(args.SingleOrDefault() ?? "App/MealPrepApp.Desktop/Assets/Icons/app-icon.svg");
var directory = Path.GetDirectoryName(source)!;
var svg = XDocument.Load(source).Root!;
if ((string?)svg.Attribute("viewBox") != "0 0 1024 1024")
    throw new InvalidDataException("The icon master must use a 1024×1024 viewBox.");
var images = new[] { 16, 24, 32, 48, 64, 128, 256, 512, 1024 }.ToDictionary(size => size, Render);
File.WriteAllBytes(Path.Combine(directory, "app-icon.png"), images[1024]);
File.WriteAllBytes(Path.Combine(directory, "app-icon-512.png"), images[512]);
File.WriteAllBytes(Path.Combine(directory, "app-icon.ico"), WindowsIcon());
File.WriteAllBytes(Path.Combine(directory, "app-icon.icns"), MacIcon());
Console.WriteLine($"Generated PNG (1024px), ICO (16–256px) and ICNS (16–1024px): {directory}");

byte[] Render(int size)
{
    // Supersample small representations so the leaf remains smooth at taskbar sizes.
    var renderSize = Math.Max(256, size);
    using var surface = SKSurface.Create(new SKImageInfo(renderSize, renderSize, SKColorType.Rgba8888, SKAlphaType.Premul));
    var canvas = surface.Canvas;
    canvas.Clear(SKColors.Transparent);
    canvas.Scale(renderSize / 1024f);
    foreach (var element in svg.Elements())
    {
        if (element.Name.LocalName == "title") continue;
        using var path = Shape(element);
        foreach (var style in new[] { SKPaintStyle.Fill, SKPaintStyle.Stroke })
        {
            var color = (string?)element.Attribute(style == SKPaintStyle.Fill ? "fill" : "stroke") ?? "none";
            if (color == "none") continue;
            var opacity = Number(element, "opacity", 1);
            using var paint = new SKPaint
            {
                IsAntialias = true, Style = style,
                Color = SKColor.Parse(color).WithAlpha((byte)Math.Round(255 * opacity)),
                StrokeWidth = Number(element, "stroke-width", 1),
                StrokeCap = (string?)element.Attribute("stroke-linecap") == "round" ? SKStrokeCap.Round : SKStrokeCap.Butt,
                StrokeJoin = (string?)element.Attribute("stroke-linejoin") == "round" ? SKStrokeJoin.Round : SKStrokeJoin.Miter
            };
            canvas.DrawPath(path, paint);
        }
    }
    using var image = surface.Snapshot();
    using var bitmap = SKBitmap.FromImage(image);
    using var scaled = bitmap.Resize(new SKImageInfo(size, size), SKFilterQuality.High);
    using var output = SKImage.FromBitmap(scaled);
    using var png = output.Encode(SKEncodedImageFormat.Png, 100);
    return png.ToArray();
}

static float Number(XElement element, string name, float fallback = 0) =>
    float.TryParse((string?)element.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

static SKPath Shape(XElement element)
{
    var path = new SKPath();
    switch (element.Name.LocalName)
    {
        case "rect":
            var x = Number(element, "x"); var y = Number(element, "y");
            path.AddRoundRect(new SKRect(x, y, x + Number(element, "width"), y + Number(element, "height")), Number(element, "rx"), Number(element, "rx"));
            break;
        case "circle":
            path.AddCircle(Number(element, "cx"), Number(element, "cy"), Number(element, "r"));
            break;
        case "path":
            path.Dispose();
            return SKPath.ParseSvgPathData((string?)element.Attribute("d")) ?? throw new InvalidDataException("Invalid icon path.");
        default:
            path.Dispose();
            throw new InvalidDataException($"Unsupported SVG shape: {element.Name.LocalName}");
    }
    return path;
}

byte[] WindowsIcon()
{
    int[] sizes = [16, 24, 32, 48, 64, 128, 256];
    using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
    writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
    var offset = 6 + 16 * sizes.Length;
    foreach (var size in sizes)
    {
        writer.Write((byte)(size == 256 ? 0 : size)); writer.Write((byte)(size == 256 ? 0 : size));
        writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
        writer.Write(images[size].Length); writer.Write(offset); offset += images[size].Length;
    }
    foreach (var size in sizes) writer.Write(images[size]);
    return stream.ToArray();
}

byte[] MacIcon()
{
    // Both standard and Retina representations, including 512pt @2x for the Dock/Finder.
    (string Type, int Size)[] representations = [("icp4", 16), ("icp5", 32), ("icp6", 64),
        ("ic07", 128), ("ic08", 256), ("ic09", 512), ("ic10", 1024),
        ("ic11", 32), ("ic12", 64), ("ic13", 256), ("ic14", 512)];
    using var stream = new MemoryStream();
    stream.Write(Encoding.ASCII.GetBytes("icns"));
    BigEndian(8 + representations.Sum(item => 8 + images[item.Size].Length));
    foreach (var (type, size) in representations)
    {
        stream.Write(Encoding.ASCII.GetBytes(type)); BigEndian(8 + images[size].Length);
        stream.Write(images[size]);
    }
    return stream.ToArray();
    void BigEndian(int value)
    {
        Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(bytes, value); stream.Write(bytes);
    }
}

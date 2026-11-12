using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace Launcher.App;

public static class BrandAssets
{
    public static DrawingImage Image { get; } = Load();
    private static DrawingImage Load()
    {
        using var stream = typeof(BrandAssets).Assembly.GetManifestResourceStream("Brand.logo.svg")!;
        var root = XDocument.Load(stream).Root!;
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 256, 256))));
        static double Number(XElement item, string key) => double.Parse(item.Attribute(key)!.Value, CultureInfo.InvariantCulture);
        static Brush? Fill(string? value) => value is null or "none" ? null : (Brush)new BrushConverter().ConvertFromInvariantString(value)!;
        foreach (var item in root.Elements())
        {
            Geometry shape;
            if (item.Name.LocalName == "rect") shape = new RectangleGeometry(new Rect(Number(item,"x"), Number(item,"y"), Number(item,"width"), Number(item,"height")), Number(item,"rx"), Number(item,"rx"));
            else if (item.Name.LocalName == "path") shape = Geometry.Parse(item.Attribute("d")!.Value);
            else continue;
            var stroke = Fill(item.Attribute("stroke")?.Value);
            drawing.Children.Add(new GeometryDrawing(Fill(item.Attribute("fill")?.Value), stroke is null ? null : new Pen(stroke, Number(item,"stroke-width")), shape));
        }
        var image = new DrawingImage(drawing); image.Freeze(); return image;
    }
    public static void Export(string directory)
    {
        Directory.CreateDirectory(directory);
        static byte[] Png(int size)
        {
            var drawing = new DrawingVisual();
            using (var context = drawing.RenderOpen()) context.DrawImage(Image, new Rect(0, 0, size * 4, size * 4));
            var large = new RenderTargetBitmap(size * 4, size * 4, 96, 96, PixelFormats.Pbgra32); large.Render(drawing);
            var final = new DrawingVisual(); RenderOptions.SetBitmapScalingMode(final, BitmapScalingMode.HighQuality);
            using (var context = final.RenderOpen()) context.DrawImage(large, new Rect(0, 0, size, size));
            var pixels = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32); pixels.Render(final);
            var metadata = new BitmapMetadata("png");
            metadata.SetQuery("/tEXt/{str=origin}", "Rendered from original design/brand.svg by BrandAssets.Export; 4x supersampling, no source raster.");
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(pixels, null, metadata, null));
            using var output = new MemoryStream(); encoder.Save(output); return output.ToArray();
        }
        File.WriteAllBytes(Path.Combine(directory,"App.png"), Png(1024));
        var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 }; var frames = sizes.Select(Png).ToArray();
        using var icon = new BinaryWriter(File.Create(Path.Combine(directory,"App.ico")));
        icon.Write((ushort)0); icon.Write((ushort)1); icon.Write((ushort)sizes.Length);
        var offset = 6 + 16 * sizes.Length;
        for (var i = 0; i < sizes.Length; i++)
        {
            var dimension = (byte)(sizes[i] == 256 ? 0 : sizes[i]);
            icon.Write(dimension); icon.Write(dimension); icon.Write((byte)0); icon.Write((byte)0); icon.Write((ushort)1); icon.Write((ushort)32);
            icon.Write(frames[i].Length); icon.Write(offset); offset += frames[i].Length;
        }
        foreach (var frame in frames) icon.Write(frame);
    }
}

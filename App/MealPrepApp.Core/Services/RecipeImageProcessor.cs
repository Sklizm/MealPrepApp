using SkiaSharp;

namespace MealPrepApp.Services;

public sealed record ProcessedRecipeImage(byte[] Image, byte[] Thumbnail);

public static class RecipeImageProcessor
{
    internal const int MaximumInputBytes = 10 * 1024 * 1024;
    public const int MaximumImportedPhotoBytes = 384 * 1024;

    public static async Task<ProcessedRecipeImage> LoadAsync(string path)
    {
        await using var file = File.OpenRead(path);
        if (file.Length > MaximumInputBytes)
            throw new InvalidOperationException(L.Text("Text.TheImageExceeds10Mib"));
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        using var normalized = Decode(stream.ToArray());
        return new(Encode(normalized, 2048, 85), Encode(normalized, 320, 75));
    }

    public static ProcessedRecipeImage ProcessImportedPhoto(byte[] bytes)
    {
        using var image = Decode(bytes);
        const double aspect = 16d / 9;
        var width = Math.Min(image.Width, (int)Math.Round(image.Height * aspect));
        var height = Math.Min(image.Height, Math.Max(1, (int)Math.Round(image.Width / aspect)));
        var x = (image.Width - width) / 2;
        var y = (image.Height - height) / 2;
        using var crop = new SKBitmap();
        if (!image.ExtractSubset(crop, new SKRectI(x, y, x + width, y + height)))
            throw new InvalidOperationException(L.Text("Text.TheImageCouldNotBeResized"));

        var main = Encode(image, 2048, 85);
        for (var edge = 2048; main.Length > MaximumImportedPhotoBytes && edge >= 320; edge = edge * 3 / 4)
            main = Encode(image, edge, 72);

        if (main.Length > MaximumImportedPhotoBytes)
            throw new InvalidOperationException(L.Text("Text.TheImageCouldNotBeResized"));
        return new(main, Encode(crop, 320, 75));
    }

    private static SKBitmap Decode(byte[] bytes)
    {
        if (bytes.Length > MaximumInputBytes)
            throw new InvalidOperationException(L.Text("Text.TheImageExceeds10Mib"));
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data)
            ?? throw new InvalidOperationException(L.Text("Text.InvalidImageFormat"));
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > 25_000_000)
            throw new InvalidOperationException(L.Text("Text.TheImageExceeds25Megapixels"));
        using var decoded = new SKBitmap(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
            throw new InvalidOperationException(L.Text("Text.TheImageCouldNotBeDecoded"));
        return Orient(decoded, codec.EncodedOrigin);
    }

    private static SKBitmap Orient(SKBitmap image, SKEncodedOrigin origin)
    {
        var swapped = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var result = new SKBitmap(swapped ? image.Height : image.Width,
            swapped ? image.Width : image.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.White);
        var w = image.Width;
        var h = image.Height;
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => Matrix(-1, 0, w, 0, 1, 0),
            SKEncodedOrigin.BottomRight => Matrix(-1, 0, w, 0, -1, h),
            SKEncodedOrigin.BottomLeft => Matrix(1, 0, 0, 0, -1, h),
            SKEncodedOrigin.LeftTop => Matrix(0, 1, 0, 1, 0, 0),
            SKEncodedOrigin.RightTop => Matrix(0, -1, h, 1, 0, 0),
            SKEncodedOrigin.RightBottom => Matrix(0, -1, h, -1, 0, w),
            SKEncodedOrigin.LeftBottom => Matrix(0, 1, 0, -1, 0, w),
            _ => SKMatrix.CreateIdentity()
        };
        canvas.SetMatrix(matrix);
        canvas.DrawBitmap(image, 0, 0);
        return result;
    }

    private static SKMatrix Matrix(float a, float b, float c, float d, float e, float f) =>
        new() { ScaleX = a, SkewX = b, TransX = c, SkewY = d, ScaleY = e, TransY = f, Persp2 = 1 };

    private static byte[] Encode(SKBitmap image, int maxEdge, int quality)
    {
        var scale = Math.Min(1d, (double)maxEdge / Math.Max(image.Width, image.Height));
        var info = new SKImageInfo(Math.Max(1, (int)Math.Round(image.Width * scale)),
            Math.Max(1, (int)Math.Round(image.Height * scale)));
        using var resized = image.Resize(info, SKFilterQuality.High)
            ?? throw new InvalidOperationException(L.Text("Text.TheImageCouldNotBeResized"));
        using var encodedImage = SKImage.FromBitmap(resized);
        using var jpeg = encodedImage.Encode(SKEncodedImageFormat.Jpeg, quality);
        return jpeg.ToArray();
    }
}

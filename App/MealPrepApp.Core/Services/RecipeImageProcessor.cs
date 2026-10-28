using SkiaSharp;

namespace MealPrepApp.Services;

public sealed record ProcessedRecipeImage(byte[] Image, byte[] Thumbnail);

public static class RecipeImageProcessor
{
    public static async Task<ProcessedRecipeImage> LoadAsync(string path)
    {
        await using var file = File.OpenRead(path);
        if (file.Length > 10 * 1024 * 1024)
            throw new InvalidOperationException("Imaginea depaseste 10 MiB.");
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        using var data = SKData.CreateCopy(stream.ToArray());
        using var codec = SKCodec.Create(data)
            ?? throw new InvalidOperationException("Format de imagine invalid.");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > 25_000_000)
            throw new InvalidOperationException("Imaginea depaseste 25 megapixeli.");
        using var decoded = new SKBitmap(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
            throw new InvalidOperationException("Imaginea nu a putut fi decodata.");
        using var normalized = Orient(decoded, codec.EncodedOrigin);
        return new(Encode(normalized, 2048, 85), Encode(normalized, 320, 75));
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
            ?? throw new InvalidOperationException("Imaginea nu a putut fi redimensionata.");
        using var encodedImage = SKImage.FromBitmap(resized);
        using var jpeg = encodedImage.Encode(SKEncodedImageFormat.Jpeg, quality);
        return jpeg.ToArray();
    }
}

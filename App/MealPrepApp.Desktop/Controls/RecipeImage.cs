using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace MealPrepApp.Desktop.Controls;

/// <summary>Owns exactly one decoded bitmap and releases it when replaced or detached.</summary>
public sealed class RecipeImage : Image
{
    public static readonly StyledProperty<byte[]?> BytesProperty =
        AvaloniaProperty.Register<RecipeImage, byte[]?>(nameof(Bytes));
    public static readonly StyledProperty<int> DecodePixelWidthProperty =
        AvaloniaProperty.Register<RecipeImage, int>(nameof(DecodePixelWidth), 2048);
    public int DecodePixelWidth
    {
        get => GetValue(DecodePixelWidthProperty);
        set => SetValue(DecodePixelWidthProperty, value);
    }
    private Bitmap? _bitmap;

    public byte[]? Bytes { get => GetValue(BytesProperty); set => SetValue(BytesProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BytesProperty || change.Property == DecodePixelWidthProperty) UpdateBitmap();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateBitmap();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void UpdateBitmap()
    {
        Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
        if (Bytes is not { Length: > 0 } bytes) return;
        try
        {
            using var stream = new MemoryStream(bytes);
            using var data = SkiaSharp.SKData.CreateCopy(bytes);
            using var codec = SkiaSharp.SKCodec.Create(data);
            if (codec is null) return;
            _bitmap = Bitmap.DecodeToWidth(stream,
                Math.Max(1, Math.Min(DecodePixelWidth, codec.Info.Width)));
            Source = _bitmap;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
        {
            MealPrepApp.Services.AppDiagnostics.Record(ex);
        }
    }
}

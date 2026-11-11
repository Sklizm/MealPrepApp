using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using MealPrepApp.Desktop.Views.Ingrediente;
using SkiaSharp;
using Xunit;

namespace MealPrepApp.Tests;

public sealed partial class RegressionTests
{
    [AvaloniaFact]
    public void AppIconIsEmbeddedAndAppliedToWindowsAndDialogsInBothThemes()
    {
        using var stream = AssetLoader.Open(new Uri("avares://MealPrepApp.Desktop/Assets/Icons/app-icon.png"));
        using var bitmap = SKBitmap.Decode(stream);
        Assert.Equal(1024, bitmap.Width); Assert.Equal(1024, bitmap.Height);
        Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);
        Assert.Equal(SKColor.Parse("#B87960"), bitmap.GetPixel(150, 512));
        Assert.NotEqual(bitmap.GetPixel(150, 512), bitmap.GetPixel(512, 512));
        var original = Application.Current!.RequestedThemeVariant;
        try
        {
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var window in new Window[] { new(), new IngredientAddDialog() })
            {
                Application.Current.RequestedThemeVariant = theme;
                try
                {
                    window.Show(); Dispatcher.UIThread.RunJobs();
                    Assert.NotNull(window.Icon);
                }
                finally { window.Close(); }
            }
        }
        finally { Application.Current.RequestedThemeVariant = original; }
    }
}

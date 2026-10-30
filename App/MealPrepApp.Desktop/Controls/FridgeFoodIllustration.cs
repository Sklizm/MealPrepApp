using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MealPrepApp.Desktop.Controls;

/// <summary>Small, resolution-independent shelf illustrations; no timers or image decoding.</summary>
public sealed class FridgeFoodIllustration : Control
{
    public static readonly StyledProperty<string> KindProperty = AvaloniaProperty.Register<FridgeFoodIllustration, string>(nameof(Kind), "Other");
    public string Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    static FridgeFoodIllustration() => AffectsRender<FridgeFoodIllustration>(KindProperty);
    private static readonly IBrush Cream = Brush.Parse("#F7EAD4"), Green = Brush.Parse("#9BB77B"), LightGreen = Brush.Parse("#C5D79C"), Red = Brush.Parse("#C17C70"), Gold = Brush.Parse("#E9B969"), Brown = Brush.Parse("#78513B");
    private static readonly Pen Outline = new(Brush.Parse("#49392E"), 1.7, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
    private static readonly Dictionary<string, Geometry> Shapes = new()
    {
        ["steak"] = Geometry.Parse("M15 27 C12 13 31 10 41 17 C47 21 49 24 57 25 C72 28 72 44 64 51 C55 58 45 49 36 53 C20 61 8 46 15 27 Z"),
        ["lettuce"] = Geometry.Parse("M38 57 C20 56 9 46 14 36 C5 24 20 17 27 20 C30 7 48 11 51 20 C65 14 74 29 65 37 C74 50 53 60 38 57 Z"),
        ["milk"] = Geometry.Parse("M26 16 L46 16 55 27 55 58 25 58 25 27 31 22 Z"),
        ["bag"] = Geometry.Parse("M23 14 L55 14 52 22 58 58 20 58 26 22 Z"),
        ["leaf"] = Geometry.Parse("M37 45 C14 42 16 19 20 18 C39 16 43 32 37 45 M39 43 C40 23 56 18 64 23 C62 42 50 49 39 43"),
        ["bottle"] = Geometry.Parse("M32 10 L46 10 46 21 Q57 27 55 37 L55 57 Q40 63 25 57 L25 37 Q23 27 32 21 Z")
    };
    public override void Render(DrawingContext context)
    {
        var scale = Math.Min(Bounds.Width / 80, Bounds.Height / 68);
        using var transform = context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation((Bounds.Width - 80 * scale) / 2, (Bounds.Height - 68 * scale) / 2));
        void Ellipse(IBrush fill, double x, double y, double rx, double ry) => context.DrawEllipse(fill, Outline, new Point(x,y), rx,ry);
        void Box(IBrush fill, double x, double y, double w, double h, double r = 3) => context.DrawRectangle(fill, Outline, new Rect(x,y,w,h),r,r);
        void Line(double x, double y, double xx, double yy) => context.DrawLine(Outline,new Point(x,y),new Point(xx,yy));
        switch (Kind)
        {
            case "Produce":
                context.DrawGeometry(Green, Outline, Shapes["lettuce"]);
                Ellipse(LightGreen,39,37,17,18); Line(39,52,38,25); Line(38,39,29,31); Line(39,44,49,32); break;
            case "Meat":
                context.DrawGeometry(Red, Outline, Shapes["steak"]); Ellipse(Cream,53,36,10,8); Ellipse(Red,53,36,4,3);
                Line(23,28,31,38); Line(22,38,28,44); Line(33,23,40,31); break;
            case "Dairy":
                context.DrawGeometry(Cream, Outline, Shapes["milk"]); Box(Green,25,32,30,15,1); Line(25,27,55,27); Line(42,17,35,27);
                Ellipse(Gold,59,50,9,12); break;
            case "Jar":
                Box(Gold,24,19,32,39,8); Box(Green,22,13,36,9,2); Box(Cream,24,30,32,17,1); Ellipse(Red,40,39,7,6); Line(40,32,43,28); break;
            case "Herbs":
                context.DrawGeometry(Green, Outline, Shapes["leaf"]); Line(30,55,48,29); Line(31,54,25,29); Box(Cream,26,47,23,12,2); break;
            case "Pasta":
                context.DrawGeometry(Cream, Outline, Shapes["bag"]); Box(Gold,28,27,22,22,3); Line(31,30,35,46); Line(38,30,42,46); Line(45,30,48,43); Line(28,19,50,19); break;
            case "Drink":
                context.DrawGeometry(Gold, Outline, Shapes["bottle"]); Box(Green,30,8,18,7,2); Box(Cream,25,35,30,15,1); Ellipse(Gold,40,42,6,6); break;
            default:
                Box(Cream,17,29,47,28,7); Box(Green,14,24,53,8,3); Box(Gold,33,39,15,9,2); break;
        }
    }
}

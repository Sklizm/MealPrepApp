using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace MealPrepApp.Desktop.Controls;

public enum NavigationIconKind { Home, Recipes, Calendar, Fridge, Shopping, Ingredients, Reports }

/// <summary>A finite clock-driven illustration. No subscription survives a completed pose or detach.</summary>
public sealed class AnimatedNavigationIcon : Control
{
    public static readonly StyledProperty<NavigationIconKind> KindProperty =
        AvaloniaProperty.Register<AnimatedNavigationIcon, NavigationIconKind>(nameof(Kind));
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<AnimatedNavigationIcon, double>(nameof(Progress));
    public static readonly StyledProperty<bool> ReducedMotionProperty =
        AvaloniaProperty.Register<AnimatedNavigationIcon, bool>(nameof(ReducedMotion));
    public NavigationIconKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public bool ReducedMotion { get => GetValue(ReducedMotionProperty); set => SetValue(ReducedMotionProperty, value); }
    private Button? _target;
    private CancellationTokenSource? _animationCts;
    private bool _engaged;
    internal Func<Animation, CancellationToken, Task>? AnimationRunner { get; set; }
    internal Task AnimationTask { get; private set; } = Task.CompletedTask;
    internal bool HasActiveAnimation => _animationCts != null;

    public AnimatedNavigationIcon() => ActualThemeVariantChanged += (_, _) => InvalidateVisual();

    static AnimatedNavigationIcon() => AffectsRender<AnimatedNavigationIcon>(KindProperty, ProgressProperty);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _target = this.FindAncestorOfType<Button>();
        if (_target != null) { _target.PropertyChanged += TargetChanged; UpdateEngagement(); }
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_target != null) _target.PropertyChanged -= TargetChanged;
        _target = null; StopAnimation(); _engaged = false; Progress = 0;
        base.OnDetachedFromVisualTree(e);
    }
    private void TargetChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsPointerOverProperty || e.Property == IsKeyboardFocusWithinProperty) UpdateEngagement();
    }
    private void UpdateEngagement() => SetEngagement(_target?.IsPointerOver == true, _target?.IsKeyboardFocusWithin == true);
    internal void SetEngagement(bool pointer, bool focus)
    {
        var engaged = pointer || focus;
        if (_engaged == engaged) return;
        _engaged = engaged;
        AnimateTo(engaged ? 1 : 0, engaged ? 780 : 180);
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ReducedMotionProperty)
        {
            StopAnimation(); Progress = _engaged && !ReducedMotion ? 1 : 0; InvalidateVisual();
        }
    }
    private void StopAnimation()
    {
        var previous = _animationCts; _animationCts = null;
        previous?.Cancel(); previous?.Dispose();
    }
    private void AnimateTo(double target, int milliseconds)
    {
        var from = Progress;
        StopAnimation(); Progress = from;
        if (ReducedMotion) { Progress = 0; return; }
        if (Math.Abs(from - target) < .001) { Progress = target; return; }
        var source = _animationCts = new CancellationTokenSource();
        var animation = new Animation
        {
            FillMode = FillMode.Forward,
            Duration = TimeSpan.FromMilliseconds(milliseconds), Easing = new CubicEaseInOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(ProgressProperty, from) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(ProgressProperty, target) } }
            }
        };
        AnimationTask = RunAnimationAsync(animation, source, target);
    }
    private async Task RunAnimationAsync(Animation animation, CancellationTokenSource source, double target)
    {
        try
        {
            await (AnimationRunner?.Invoke(animation, source.Token) ?? animation.RunAsync(this, source.Token));
            if (ReferenceEquals(_animationCts, source)) Progress = target;
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(_animationCts, source)) { _animationCts = null; source.Dispose(); }
        }
    }

    private static readonly IBrush Paper = new SolidColorBrush(Color.Parse("#F7EAD4"));
    private static readonly IBrush Sage = new SolidColorBrush(Color.Parse("#BCD1A5"));
    private static readonly IBrush Brown = new SolidColorBrush(Color.Parse("#78513B"));
    private static readonly IBrush Red = new SolidColorBrush(Color.Parse("#A53038"));
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#E9B969"));
    private static readonly IBrush Ink = new SolidColorBrush(Color.Parse("#49392E"));
    private static readonly Pen Outline = new(Ink, 1.8, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
    private static readonly Dictionary<string, Geometry> Shapes = new()
    {
        ["roof"] = Geometry.Parse("M4,23 L24,6 44,23 40,27 24,13 8,27 Z"),
        ["basket"] = Geometry.Parse("M8,24 L40,24 36,40 12,40 Z"),
        ["leaf"] = Geometry.Parse("M23,24 Q15,10 21,9 Q29,12 25,24 M25,24 Q27,9 34,13 Q36,20 25,24"),
        ["pepper"] = Geometry.Parse("M19,15 L30,15 34,37 Q25,42 15,37 Z")
    };
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 48;
        using var transform = context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation((Bounds.Width - 48 * scale) / 2, (Bounds.Height - 48 * scale) / 2));
        var p = ReducedMotion ? 0 : Math.Clamp(Progress, 0, 1);
        var visibleInk = ActualThemeVariant == ThemeVariant.Dark ? Paper : Ink;
        void Box(IBrush fill, double x, double y, double w, double h, double radius = 2) => context.DrawRectangle(fill, Outline, new Rect(x,y,w,h),radius,radius);
        void Line(double x, double y, double xx, double yy, IBrush? brush = null, double width = 1.8) => context.DrawLine(new Pen(brush ?? Ink,width,lineCap:PenLineCap.Round),new Point(x,y),new Point(xx,yy));
        void Fruit(IBrush fill, double x, double y, double radius) => context.DrawEllipse(fill,Outline,new Point(x,y),radius,radius);
        switch (Kind)
        {
            case NavigationIconKind.Home:
                Box(Paper,10,21,28,22); context.DrawGeometry(Sage,Outline,Shapes["roof"]);
                Box(Gold,21,28,10,15,1); Box(Brown,21,28,10*(1-.86*p),15,1);
                Box(Sage,13,28,5,6,1); break;
            case NavigationIconKind.Recipes:
                Box(Sage,8,9,29,34,3); Box(Paper,11,5,29,35,3);
                Line(17,12,30,12,Brown,2.4);
                for (var i=0;i<4;i++)
                {
                    var amount = Math.Clamp(p*4-i,0,1);
                    Line(17,19+i*5,21,19+i*5,Brown);
                    if(amount>0) Stroke(context,Ink,1.7,amount,new Point(21,19+i*5),new Point(25,18+i*5),new Point(28,20+i*5),new Point(34,19+i*5));
                }
                break;
            case NavigationIconKind.Calendar:
                Box(Paper,7,9,34,32,3); Box(Sage,7,9,34,9,2);
                Line(15,6,15,13); Line(33,6,33,13);
                for(var row=0;row<2;row++) for(var col=0;col<3;col++) Box(Sage,12+col*9,23+row*9,3,3,1);
                Stroke(context,Red,3.4,ReducedMotion ? 1 : p,new Point(17,29),new Point(23,35),new Point(36,22)); break;
            case NavigationIconKind.Fridge:
                Box(Paper,9,5,30,38,3); Box(Sage,12,20,24,20,1);
                Fruit(Red,18,33,3.5); Fruit(Gold,29,34,3.5); Line(16,29,18,30,Sage,2);
                Box(Paper,9,5,30,13,2); Line(14,10,14,14);
                Box(Paper,9,19,30*(1-.85*p),24,2); Line(12,24,12,31); break;
            case NavigationIconKind.Shopping:
                var shake = Math.Sin(p*Math.PI*6)*2.3*Math.Sin(p*Math.PI);
                using (context.PushTransform(Matrix.CreateTranslation(shake,0)))
                {
                    Fruit(Red,17,22,5); Fruit(Gold,30,21,5); context.DrawGeometry(Sage,Outline,Shapes["leaf"]);
                    context.DrawGeometry(Paper,Outline,Shapes["basket"]); Line(16,24,22,14); Line(32,24,26,14);
                    for(var i=0;i<4;i++) Line(16+i*5,28,17+i*4.5,36,Brown,1.4);
                }
                break;
            case NavigationIconKind.Ingredients:
                var angle = (-.6*p + Math.Sin(p*Math.PI*8)*.13*Math.Sin(p*Math.PI));
                using(context.PushTransform(Matrix.CreateTranslation(-24,-24)*Matrix.CreateRotation(angle)*Matrix.CreateTranslation(24,24)))
                {
                    context.DrawGeometry(Paper,Outline,Shapes["pepper"]); Box(Sage,17,9,15,7,2);
                    for(var i=0;i<3;i++) Fruit(Ink,20+i*4,11, .65);
                    Line(21,22,28,22,Brown,1.3); Line(20,27,29,27,Brown,1.3);
                }
                break;
            case NavigationIconKind.Reports:
                Line(8,8,8,41,visibleInk); Line(8,41,42,41,visibleInk);
                var growth = .3+.7*p;
                Box(Sage,13,40-12*growth,6,12*growth,1); Box(Gold,23,40-22*growth,6,22*growth,1); Box(Paper,33,40-31*growth,6,31*growth,1);
                Stroke(context,visibleInk,2,ReducedMotion ? 1 : .2+.8*p,new Point(12,29),new Point(23,22),new Point(31,24),new Point(42,8)); break;
        }
    }
    private static void Stroke(DrawingContext context, IBrush brush, double width, double progress, params Point[] points)
    {
        var lengths = points.Zip(points.Skip(1),(a,b)=>Math.Sqrt(Math.Pow(b.X-a.X,2)+Math.Pow(b.Y-a.Y,2))).ToArray();
        var remaining = lengths.Sum()*Math.Clamp(progress,0,1);
        var pen = new Pen(brush,width,lineCap:PenLineCap.Round,lineJoin:PenLineJoin.Round);
        for(var i=0;i<lengths.Length && remaining>0;i++)
        {
            var amount = Math.Min(1,remaining/lengths[i]);
            context.DrawLine(pen,points[i],points[i]+(points[i+1]-points[i])*amount); remaining-=lengths[i];
        }
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MealPrepApp.Services;
using SkiaSharp;

namespace MealPrepApp.Desktop.Views;

/// <summary>Crop and irreversible redaction happen before uploading.</summary>
public sealed class AssistantImageWindow : Window
{
    private SKBitmap _bitmap;
    private readonly Image _preview=new(){Stretch=Stretch.Uniform,Width=660,Height=420};
    private Bitmap? _display;
    private Point? _start;private Point? _end;
    private readonly TextBlock _selection=new();
    public AssistantImageWindow():this(Blank()){}
    private static byte[] Blank(){using var image=new SKBitmap(2,2);using(var canvas=new SKCanvas(image))canvas.Clear(SKColors.White);using var data=image.Encode(SKEncodedImageFormat.Jpeg,90);return data.ToArray();}
    public AssistantImageWindow(byte[] image)
    {
        _bitmap=SKBitmap.Decode(image);Width=740;Height=650;Title=L.Text("Ai.PhotoReview");WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var crop=Button("Ai.Crop",()=>Transform(false));var redact=Button("Ai.Redact",()=>Transform(true));
        var rotate=Button("Ai.Rotate",()=>{var next=new SKBitmap(_bitmap.Height,_bitmap.Width);using(var canvas=new SKCanvas(next)){canvas.Translate(next.Width,0);canvas.RotateDegrees(90);canvas.DrawBitmap(_bitmap,0,0);} _bitmap.Dispose();_bitmap=next;Render();});
        var accept=Button("Ai.UploadReviewed",()=>{using var encoded=_bitmap.Encode(SKEncodedImageFormat.Jpeg,90);Close(encoded.ToArray());});
        _preview.PointerPressed+=(_,e)=>{_start=e.GetPosition(_preview);_end=_start;e.Pointer.Capture(_preview);};
        _preview.PointerMoved+=(_,e)=>{if(_start is not null&&e.GetCurrentPoint(_preview).Properties.IsLeftButtonPressed){_end=e.GetPosition(_preview);_selection.Text=L.Text("Ai.Selection",(int)Math.Abs(_end.Value.X-_start.Value.X),(int)Math.Abs(_end.Value.Y-_start.Value.Y));}};
        _preview.PointerReleased+=(_,e)=>{_end=e.GetPosition(_preview);e.Pointer.Capture(null);};
        Content=new ScrollViewer{Content=new StackPanel{Margin=new Thickness(20),Spacing=12,Children={new TextBlock{Text=L.Text("Ai.ImageHint"),TextWrapping=TextWrapping.Wrap},_preview,_selection,new WrapPanel{Children={crop,redact,rotate}},accept}}};Render();
        Closed+=(_,_)=>{_display?.Dispose();_bitmap.Dispose();};
    }
    private static Button Button(string key,Action action){var b=new Button{Content=L.Text(key),Margin=new Thickness(0,0,8,8)};b.Click+=(_,_)=>action();return b;}
    private void Render(){using var encoded=_bitmap.Encode(SKEncodedImageFormat.Png,100);var prior=_display;_display=new Bitmap(new MemoryStream(encoded.ToArray()));_preview.Source=_display;prior?.Dispose();_start=null;_end=null;_selection.Text="";}
    private void Transform(bool redact)
    {
        if(_start is not {} start||_end is not {} end)return;
        var scale=Math.Min(_preview.Bounds.Width/_bitmap.Width,_preview.Bounds.Height/_bitmap.Height);
        var x=(_preview.Bounds.Width-_bitmap.Width*scale)/2;var y=(_preview.Bounds.Height-_bitmap.Height*scale)/2;
        var rect=new SKRectI(Math.Clamp((int)((Math.Min(start.X,end.X)-x)/scale),0,_bitmap.Width),Math.Clamp((int)((Math.Min(start.Y,end.Y)-y)/scale),0,_bitmap.Height),Math.Clamp((int)((Math.Max(start.X,end.X)-x)/scale),0,_bitmap.Width),Math.Clamp((int)((Math.Max(start.Y,end.Y)-y)/scale),0,_bitmap.Height));
        if(rect.Width<2||rect.Height<2)return;
        if(redact){using var canvas=new SKCanvas(_bitmap);using var paint=new SKPaint{Color=SKColors.Black,Style=SKPaintStyle.Fill};canvas.DrawRect(rect,paint);}
        else {var next=new SKBitmap(rect.Width,rect.Height);if(!_bitmap.ExtractSubset(next,rect)){next.Dispose();return;}_bitmap.Dispose();_bitmap=next;}
        Render();
    }
}

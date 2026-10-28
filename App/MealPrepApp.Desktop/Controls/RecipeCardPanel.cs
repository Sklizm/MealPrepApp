using Avalonia;
using Avalonia.Controls;
namespace MealPrepApp.Desktop.Controls;
public sealed class RecipeCardPanel:Panel
{
    public static readonly StyledProperty<bool> ListViewProperty=AvaloniaProperty.Register<RecipeCardPanel,bool>(nameof(ListView));
    public bool ListView {get=>GetValue(ListViewProperty);set=>SetValue(ListViewProperty,value);}
    static RecipeCardPanel(){AffectsMeasure<RecipeCardPanel>(ListViewProperty);}
    private int Columns(double width)=>ListView ? 1 : Math.Max(1,(int)((width+16)/256));
    protected override Size MeasureOverride(Size available)
    {
        var width=double.IsFinite(available.Width) ? available.Width : 768;
        var columns=Columns(width);var cell=(width-(columns-1)*16)/columns;double height=0;
        foreach(var child in Children){child.Measure(new Size(cell,double.PositiveInfinity));height=Math.Max(height,child.DesiredSize.Height);}
        return new Size(width,Math.Ceiling((double)Children.Count/columns)*(height+16));
    }
    protected override Size ArrangeOverride(Size final)
    {
        var columns=Columns(final.Width);var cell=(final.Width-(columns-1)*16)/columns;var height=Children.Count==0 ? 0 : Children.Max(c=>c.DesiredSize.Height);
        for(int i=0;i<Children.Count;i++)Children[i].Arrange(new Rect((i%columns)*(cell+16),(i/columns)*(height+16),cell,height));
        return final;
    }
}

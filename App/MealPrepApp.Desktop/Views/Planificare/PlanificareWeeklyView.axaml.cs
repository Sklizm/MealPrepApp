using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MealPrepApp.Models;
using MealPrepApp.ViewModels.Planificare;
namespace MealPrepApp.Desktop.Views.Planificare;
public partial class PlanificareWeeklyView:UserControl
{
    private Avalonia.Point _pressed;
    private MealPlanEntry? _dragEntry;
    public PlanificareWeeklyView()
    {
        InitializeComponent();
        SizeChanged+=(_,_)=>{if(DataContext is PlanificareWeeklyViewModel vm) vm.AgendaView=Bounds.Width<800;};
        AddHandler(PointerPressedEvent,(_,e)=>
        {
            if(e.Source is Control source)
            {
                var button=source.GetVisualAncestors().Prepend(source).OfType<Button>().FirstOrDefault(b=>b.DataContext is MealPlanEntry);
                if(button!=null)OnMealPointerPressed(button,e);
            }
        },Avalonia.Interactivity.RoutingStrategies.Tunnel,true);
        AddHandler(PointerMovedEvent,OnPointerMoved,Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent,(_,_)=>_dragEntry=null,Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }
    private void OnMealPointerPressed(object? sender,PointerPressedEventArgs e)
    {
        if(sender is Control {DataContext:MealPlanEntry entry} && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed){_pressed=e.GetPosition(this);_dragEntry=entry;}
    }
#pragma warning disable CS0618
    private async void OnPointerMoved(object? sender,PointerEventArgs e)
    {
        if(_dragEntry is not {} entry || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Math.Abs(e.GetPosition(this).X-_pressed.X)+Math.Abs(e.GetPosition(this).Y-_pressed.Y)<8) return;
        _dragEntry=null;
        var data=new DataObject();data.Set("MealPrep.Meal",entry);
        try {await DragDrop.DoDragDrop(e,data,DragDropEffects.Move);} catch(Exception ex){MealPrepApp.Services.AppDiagnostics.Record(ex);}
    }
    private void OnDragOver(object? sender,DragEventArgs e) {e.DragEffects=e.Data.Contains("MealPrep.Meal") ? DragDropEffects.Move : DragDropEffects.None;e.Handled=true;}
    private async void OnDrop(object? sender,DragEventArgs e)
    {
        if(sender is Control {DataContext:WeekCellViewModel cell} && e.Data.Get("MealPrep.Meal") is MealPlanEntry entry && DataContext is PlanificareWeeklyViewModel vm) await vm.MoveEntryAsync(entry,cell.Date,cell.CategoryId);
        e.Handled=true;
    }
#pragma warning restore CS0618
}

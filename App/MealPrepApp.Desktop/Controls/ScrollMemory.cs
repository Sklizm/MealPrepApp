using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
namespace MealPrepApp.Desktop.Controls;
public sealed class ScrollMemory:AvaloniaObject
{
    public static readonly AttachedProperty<bool> EnabledProperty=AvaloniaProperty.RegisterAttached<ScrollMemory,ScrollViewer,bool>("Enabled");
    public static void SetEnabled(ScrollViewer control,bool value)=>control.SetValue(EnabledProperty,value);
    public static bool GetEnabled(ScrollViewer control)=>control.GetValue(EnabledProperty);
    private sealed class Position {public Vector Offset;}
    private static readonly ConditionalWeakTable<object,Position> Positions=new();
    static ScrollMemory()
    {
        EnabledProperty.Changed.AddClassHandler<ScrollViewer>((control,args)=>
        {
            if(args.NewValue is not true)return;
            control.DetachedFromVisualTree+=(_,_)=>{if(control.DataContext is {} vm)Positions.GetOrCreateValue(vm).Offset=control.Offset;};
            control.AttachedToVisualTree+=(_,_)=>Dispatcher.UIThread.Post(()=>{if(control.DataContext is {} vm && Positions.TryGetValue(vm,out var position)) control.Offset=position.Offset;},DispatcherPriority.Loaded);
        });
    }
}

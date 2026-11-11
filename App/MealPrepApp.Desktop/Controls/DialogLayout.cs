using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MealPrepApp.ViewModels;

namespace MealPrepApp.Desktop.Controls;

/// <summary>Shared three-part dialog layout. Only the middle row scrolls; actions stay visible.</summary>
public sealed class DialogLayout : Grid
{
    public DialogLayout()
    {
        RowDefinitions = new RowDefinitions("Auto,*,Auto");
        Margin = new Thickness(24);
        AttachedToVisualTree += (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is Window window)
            {
                _window = window;
                window.MaxHeight = Math.Min(560, window.Screens.ScreenFromWindow(window)?.WorkingArea.Height / window.RenderScaling - 40 ?? 560);
                window.AddHandler(KeyDownEvent, HandleKey, RoutingStrategies.Bubble);
                window.Opened += SetInitialFocus;
            }
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (_window != null)
            {
                _window.RemoveHandler(KeyDownEvent, HandleKey);
                _window.Opened -= SetInitialFocus;
            }
            _window = null;
        };
    }
    private Window? _window;
    private void SetInitialFocus(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_window?.IsVisible != true) return;
            var fields = this.GetVisualDescendants().OfType<InputElement>()
                .Where(c => c.IsEffectivelyVisible && c.IsEffectivelyEnabled);
            var target = fields.FirstOrDefault(c => c is TextBox { IsReadOnly: false } or ComboBox or AutoCompleteBox)
                ?? fields.OfType<Button>().FirstOrDefault(b => b.Classes.Contains("DialogCancel"))
                ?? fields.OfType<Button>().FirstOrDefault();
            target?.Focus();
        });
    }
    private void HandleKey(object? sender, KeyEventArgs e)
    {
        _window = sender as Window;
        if (e.Handled || _window == null) return;
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().Any(v => v is AutoCompleteBox { IsDropDownOpen: true } or ComboBox { IsDropDownOpen: true } or CalendarDatePicker { IsDropDownOpen: true })) return;
        if (_window.DataContext is ViewModelBase { IsBusy: true }) return;
        if (e.Key == Key.Escape)
        {
            var cancel = this.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Classes.Contains("DialogCancel") && b.IsEffectivelyVisible && b.IsEffectivelyEnabled);
            if (cancel != null) { cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true; }
        }
        if (e.Key == Key.Enter)
        {
            if (e.Source is Visual input && input.GetSelfAndVisualAncestors().Any(v => v is TextBox { AcceptsReturn: true } or Button)) return;
            var primary = this.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Classes.Contains("Primary") && b.IsEffectivelyVisible && b.IsEffectivelyEnabled);
            if (primary == null || primary.Classes.Contains("Danger")) return;
            if (primary.Command is { } command) { if (command.CanExecute(primary.CommandParameter)) command.Execute(primary.CommandParameter); }
            else primary.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            e.Handled = true;
        }
    }
}

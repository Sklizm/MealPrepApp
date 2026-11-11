using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using MealPrepApp.Services;

namespace MealPrepApp.Desktop.Localization;

/// <summary>Refresh calendar labels without replacing the picker, its date, or partially entered text.</summary>
public sealed class LocalizedCalendarDatePicker : CalendarDatePicker
{
    private IDisposable? _languageSubscription;
    private Calendar? _calendar;
    private TextBox? _input;
    private Button? _today;
    public LocalizedCalendarDatePicker()
    {
        CalendarOpened += (_, _) => UpdateToday();
        CalendarClosed += (_, _) => _input?.Focus();
    }
    private void UpdateToday()
    {
        var date = DateTime.Today;
        if (_today != null) _today.IsEnabled = (DisplayDateStart == null || date >= DisplayDateStart.Value.Date)
            && (DisplayDateEnd == null || date <= DisplayDateEnd.Value.Date) && BlackoutDates?.Contains(date) != true;
    }
    private void SelectToday(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        UpdateToday();
        if (_today?.IsEnabled != true) return;
        SetCurrentValue(SelectedDateProperty, DateTime.Today);
        SetCurrentValue(DisplayDateProperty, DateTime.Today);
        SetCurrentValue(IsDropDownOpenProperty, false);
        _input?.Focus();
    }
    protected override Type StyleKeyOverride => typeof(CalendarDatePicker);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_today != null) _today.Click -= SelectToday;
        base.OnApplyTemplate(e);
        _calendar = e.NameScope.Find<Calendar>("PART_Calendar");
        _input = e.NameScope.Find<TextBox>("PART_TextBox");
        _today = e.NameScope.Find<Button>("PART_Today");
        if (_today != null) _today.Click += SelectToday;
        UpdateToday();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _languageSubscription?.Dispose();
        _languageSubscription = L.Subscribe(this, static picker => picker.RefreshLanguage());
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _languageSubscription?.Dispose();
        _languageSubscription = null;
        base.OnDetachedFromVisualTree(e);
    }
    private void RefreshLanguage()
    {
        if (_calendar is not null)
        {
            // Avalonia 11.3 has no culture invalidation API. Its week-start change redraws
            // the existing calendar buttons. Restore the regional week start synchronously;
            // neither SelectedDate nor DisplayDate changes, and no page data is touched.
            var first = _calendar.FirstDayOfWeek;
            _calendar.SetCurrentValue(Calendar.FirstDayOfWeekProperty, (DayOfWeek)(((int)first + 1) % 7));
            _calendar.SetCurrentValue(Calendar.FirstDayOfWeekProperty, first);
        }
        // Keep the editor buffer, including incomplete long dates, untouched.
    }
}

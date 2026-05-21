using System.Collections.Generic;
using MealPrepApp.Models;

namespace MealPrepApp.ViewModels.Planificare;

/// <summary>
/// One day cell in the monthly calendar grid. Rebuilt wholesale on each refresh, so it is a
/// plain data holder — the parent's <c>Days</c> collection drives the UI, not per-cell change
/// notification. <see cref="Entries"/> renders as one chip per planned meal, coloured by slot.
/// </summary>
public sealed class DayCellViewModel
{
    public DateTime Date { get; init; }
    public int DayNumber => Date.Day;

    /// <summary>False for the leading/trailing days borrowed from the adjacent months.</summary>
    public bool IsCurrentMonth { get; init; }
    public bool IsToday => Date.Date == DateTime.Today;

    public IReadOnlyList<MealPlanEntry> Entries { get; init; } = Array.Empty<MealPlanEntry>();
}

/// <summary>One (day, meal-slot) cell in the weekly grid — the entries planned for that day in
/// that slot, plus the coordinates the add button needs to pre-fill the modal.</summary>
public sealed class WeekCellViewModel
{
    public DateTime Date { get; init; }
    public int CategoryId { get; init; }
    public IReadOnlyList<MealPlanEntry> Entries { get; init; } = Array.Empty<MealPlanEntry>();
}

/// <summary>One day row in the weekly grid: a day label and its four slot cells
/// (Breakfast / Lunch / Dinner / Snack).</summary>
public sealed class WeekRowViewModel
{
    public DateTime Date { get; init; }
    public string DayLabel { get; init; } = "";
    public bool IsToday => Date.Date == DateTime.Today;

    /// <summary>The four slot cells, in column order.</summary>
    public IReadOnlyList<WeekCellViewModel> Cells { get; init; } = Array.Empty<WeekCellViewModel>();
}

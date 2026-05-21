using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;

namespace MealPrepApp.ViewModels.Planificare;

/// <summary>
/// Backs the shared "Planifica masa" modal. Two flows:
/// <list type="bullet">
/// <item><b>Add</b> — pick a recipe (debounced autocomplete), a meal slot, a date; optional
/// servings + notes. Persists via <c>sp_PlanMeal</c>.</item>
/// <item><b>Edit</b> — the recipe is fixed (the proc cannot change it); only slot / date /
/// servings / notes are editable. Persists via <c>sp_UpdatePlannedMeal</c>, plus a Sterge
/// button that calls <c>sp_UnplanMeal</c>.</item>
/// </list>
/// </summary>
public sealed partial class PlanMealDialogViewModel : ViewModelBase
{
    private readonly MealPlanRepository _mealPlan;
    private readonly RecipeRepository _recipes;
    private readonly LookupRepository _lookups;
    private readonly IDialogService _dialog;
    private readonly ISessionService _session;

    private int? _editingEntryId;
    private bool _suppressSearch;
    private CancellationTokenSource? _searchCts;
    private const int SearchDebounceMs = 300;

    public ObservableCollection<Category> Categories { get; } = new();
    public ObservableCollection<RecipeListItem> RecipeResults { get; } = new();

    [ObservableProperty]
    private string _title = "Planifica masa";

    [ObservableProperty]
    private bool _isEdit;

    [ObservableProperty]
    private DateTime _plannedDate = DateTime.Today;

    [ObservableProperty]
    private Category? _selectedCategory;

    [ObservableProperty]
    private string _recipeSearchTerm = "";

    [ObservableProperty]
    private RecipeListItem? _selectedRecipe;

    [ObservableProperty]
    private int? _servings;

    [ObservableProperty]
    private string? _notes;

    /// <summary>Edit mode fixes the recipe, so the autocomplete is hidden in favour of a label.</summary>
    public bool CanPickRecipe => !IsEdit;

    /// <summary>The fixed recipe title shown in edit mode.</summary>
    [ObservableProperty]
    private string _fixedRecipeTitle = "";

    /// <summary>Raised after a successful save/delete so the caller refreshes and closes.</summary>
    public event EventHandler? SaveSucceeded;

    public PlanMealDialogViewModel(
        MealPlanRepository mealPlan,
        RecipeRepository recipes,
        LookupRepository lookups,
        IDialogService dialog,
        ISessionService session)
    {
        _mealPlan = mealPlan;
        _recipes = recipes;
        _lookups = lookups;
        _dialog = dialog;
        _session = session;
    }

    /// <summary>Opens the add flow pre-filled with a date + slot; optionally a recipe (e.g. when
    /// launched from a recipe's detail screen).</summary>
    public async Task InitForAddAsync(DateTime date, int categoryId, RecipeListItem? recipe = null)
    {
        IsEdit = false;
        OnPropertyChanged(nameof(CanPickRecipe));
        Title = "Planifica masa";
        _editingEntryId = null;
        PlannedDate = date.Date;
        Servings = null;
        Notes = null;

        await LoadCategoriesAsync();
        SelectedCategory = FindCategory(categoryId) ?? FindCategory(MealSlots.DefaultSlotId);

        if (recipe is not null)
        {
            _suppressSearch = true;
            SelectedRecipe = recipe;
            RecipeSearchTerm = recipe.Title;
            _suppressSearch = false;
            if (SelectedCategory is null && recipe.CategoryID is int cid)
                SelectedCategory = FindCategory(cid);
            Servings = recipe.Servings;
        }
        else
        {
            SelectedRecipe = null;
            RecipeSearchTerm = "";
        }
    }

    /// <summary>Opens the edit flow for an existing entry. Recipe is fixed.</summary>
    public async Task InitForEditAsync(MealPlanEntry entry)
    {
        IsEdit = true;
        OnPropertyChanged(nameof(CanPickRecipe));
        Title = "Editeaza masa planificata";
        _editingEntryId = entry.MealPlanEntryID;
        PlannedDate = entry.PlannedDate.Date;
        Servings = entry.Servings;
        Notes = entry.Notes;
        FixedRecipeTitle = entry.RecipeTitle;

        await LoadCategoriesAsync();
        SelectedCategory = FindCategory(entry.CategoryID);
    }

    private async Task LoadCategoriesAsync()
    {
        ClearError();
        IsBusy = true;
        try
        {
            var categories = await _lookups.GetCategoriesAsync();
            Categories.Clear();
            foreach (var c in categories.OrderBy(c => c.CategoryID))
                Categories.Add(c);
        }
        catch (AppDbException ex)
        {
            ErrorMessage = ex.FriendlyMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Category? FindCategory(int id) => Categories.FirstOrDefault(c => c.CategoryID == id);

    /// <summary>Debounced recipe autocomplete (add flow only). Selecting a result writes the
    /// title back into the term, which we suppress so it doesn't re-trigger a search.</summary>
    partial void OnRecipeSearchTermChanged(string value)
    {
        if (_suppressSearch || IsEdit) return;
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        _ = DebouncedSearchAsync(value, _searchCts.Token);
    }

    private async Task DebouncedSearchAsync(string term, CancellationToken token)
    {
        try { await Task.Delay(SearchDebounceMs, token); }
        catch (TaskCanceledException) { return; }
        if (token.IsCancellationRequested) return;

        term = (term ?? "").Trim();
        if (term.Length == 0)
        {
            RecipeResults.Clear();
            return;
        }

        try
        {
            var matches = await _recipes.SearchRecipesByTitleAsync(term);
            if (token.IsCancellationRequested) return;
            RecipeResults.Clear();
            foreach (var r in matches) RecipeResults.Add(r);
        }
        catch (AppDbException ex)
        {
            ErrorMessage = ex.FriendlyMessage;
        }
    }

    partial void OnSelectedRecipeChanged(RecipeListItem? value)
    {
        if (value is null || IsEdit) return;
        _suppressSearch = true;
        RecipeSearchTerm = value.Title;
        _suppressSearch = false;
        if (Servings is null) Servings = value.Servings;
    }

    [RelayCommand]
    private async Task Save()
    {
        ClearError();

        if (SelectedCategory is null)
        {
            ErrorMessage = "Alege o masa (slot).";
            return;
        }
        if (!IsEdit && SelectedRecipe is null)
        {
            ErrorMessage = "Alege o reteta.";
            return;
        }
        if (Servings is { } s && s <= 0)
        {
            ErrorMessage = "Numarul de portii trebuie sa fie mai mare decat 0.";
            return;
        }

        IsBusy = true;
        try
        {
            if (IsEdit && _editingEntryId.HasValue)
            {
                await _mealPlan.UpdatePlannedMealAsync(
                    _editingEntryId.Value, _session.CurrentUserId,
                    SelectedCategory.CategoryID, PlannedDate, Servings, Trimmed(Notes));
            }
            else
            {
                await _mealPlan.PlanMealAsync(
                    _session.CurrentUserId, SelectedRecipe!.RecipeID,
                    SelectedCategory.CategoryID, PlannedDate, Servings, Trimmed(Notes));
            }
            SaveSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (AppDbException ex)
        {
            ErrorMessage = ex.FriendlyMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (!IsEdit || !_editingEntryId.HasValue) return;

        if (!_dialog.Confirm("Sterge masa planificata",
                "Sigur vrei sa stergi aceasta masa din plan?"))
            return;

        IsBusy = true;
        try
        {
            await _mealPlan.UnplanMealAsync(_editingEntryId.Value, _session.CurrentUserId);
            SaveSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (AppDbException ex)
        {
            ErrorMessage = ex.FriendlyMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string? Trimmed(string? s)
    {
        var t = s?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }
}

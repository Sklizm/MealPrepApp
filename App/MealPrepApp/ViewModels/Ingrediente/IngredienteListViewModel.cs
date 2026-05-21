using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
using MealPrepApp.Views.Ingrediente;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.ViewModels.Ingrediente;

/// <summary>
/// The ingredient list sub-view: a search box, category dropdown, and a list that is either
/// flat ("Toate") or grouped by <c>IngredientCategoryName</c> ("Categorii"). Per-row delete
/// pre-checks recipe usage so a RESTRICT FK never surfaces as a raw error. v1 has no
/// <c>sp_DeleteIngredient</c> so delete is informational only.
/// </summary>
public sealed partial class IngredienteListViewModel : ViewModelBase, IAsyncLoadable
{
    private static readonly IngredientCategory AllCategories = new()
    {
        IngredientCategoryID = 0,
        Name = "Toate categoriile",
    };

    private const string UngroupedLabel = "Fara categorie";

    private readonly IngredientRepository _ingredients;
    private readonly LookupRepository _lookups;
    private readonly IDialogService _dialog;
    private readonly IServiceProvider _services;

    private bool _loaded;
    private CancellationTokenSource? _searchCts;
    private const int SearchDebounceMs = 300;

    public ObservableCollection<Ingredient> Ingredients { get; } = new();
    public ObservableCollection<IngredientCategory> Categories { get; } = new();

    public ICollectionView IngredientsView { get; }

    [ObservableProperty]
    private string _searchTerm = "";

    [ObservableProperty]
    private IngredientCategory? _selectedCategory;

    [ObservableProperty]
    private bool _useGrouping;

    public IngredienteListViewModel(
        IngredientRepository ingredients,
        LookupRepository lookups,
        IDialogService dialog,
        IServiceProvider services)
    {
        _ingredients = ingredients;
        _lookups = lookups;
        _dialog = dialog;
        _services = services;

        IngredientsView = CollectionViewSource.GetDefaultView(Ingredients);
        IngredientsView.SortDescriptions.Add(new SortDescription(nameof(Ingredient.IngredientCategoryName), ListSortDirection.Ascending));
        IngredientsView.SortDescriptions.Add(new SortDescription(nameof(Ingredient.Name), ListSortDirection.Ascending));
    }

    partial void OnUseGroupingChanged(bool value)
    {
        IngredientsView.GroupDescriptions.Clear();
        if (value)
            IngredientsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Ingredient.IngredientCategoryName)));
    }

    public async Task LoadAsync()
    {
        if (_loaded)
            return;
        ClearError();
        IsBusy = true;
        try
        {
            var categories = await _lookups.GetIngredientCategoriesAsync();
            Categories.Clear();
            Categories.Add(AllCategories);
            foreach (var category in categories)
                Categories.Add(category);
            SelectedCategory = AllCategories;

            await RefreshAsync();
        }
        catch (AppDbException ex)
        {
            ErrorMessage = ex.FriendlyMessage;
        }
        finally
        {
            IsBusy = false;
            _loaded = true;
        }
    }

    private async Task RefreshAsync()
    {
        ClearError();
        IsBusy = true;
        try
        {
            var term = SearchTerm?.Trim() ?? "";
            IReadOnlyList<Ingredient> rows;

            if (term.Length > 0)
            {
                var matches = await _ingredients.SearchIngredientsAsync(term);
                if (matches.Count == 0)
                {
                    rows = Array.Empty<Ingredient>();
                }
                else
                {
                    var matchIds = matches.Select(m => m.IngredientID).ToHashSet();
                    var all = await _ingredients.GetIngredientsAsync();
                    rows = all.Where(i => matchIds.Contains(i.IngredientID)).ToList();
                }
            }
            else
            {
                var categoryId = SelectedCategory is { IngredientCategoryID: > 0 } c
                    ? c.IngredientCategoryID
                    : (int?)null;
                rows = await _ingredients.GetIngredientsAsync(categoryId);
            }

            Ingredients.Clear();
            foreach (var row in rows)
            {
                if (string.IsNullOrEmpty(row.IngredientCategoryName))
                    row.IngredientCategoryName = UngroupedLabel;
                Ingredients.Add(row);
            }
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

    partial void OnSelectedCategoryChanged(IngredientCategory? value)
    {
        if (_loaded)
            _ = RefreshAsync();
    }

    /// <summary>As-you-type filtering with a 300ms debounce — each keystroke cancels the
    /// previous pending refresh so only one query fires once the user pauses.</summary>
    partial void OnSearchTermChanged(string value)
    {
        if (!_loaded) return;
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        _ = DebouncedRefreshAsync(_searchCts.Token);
    }

    private async Task DebouncedRefreshAsync(CancellationToken token)
    {
        try { await Task.Delay(SearchDebounceMs, token); }
        catch (TaskCanceledException) { return; }
        if (token.IsCancellationRequested) return;
        await RefreshAsync();
    }

    [RelayCommand]
    private Task ClearSearch()
    {
        SearchTerm = "";
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task AddIngredient()
    {
        var dialogVm = _services.GetRequiredService<IngredientAddDialogViewModel>();
        await dialogVm.LoadAsync();
        var added = _dialog.ShowDialog<IngredientAddDialog>(dialogVm);
        if (added)
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteIngredient(Ingredient? item)
    {
        if (item is null)
            return;

        IsBusy = true;
        try
        {
            var usage = await _ingredients.GetIngredientUsageAsync(item.IngredientID);
            if (usage is { RecipeCount: > 0 })
            {
                _dialog.ShowError(
                    $"Ingredientul \"{item.Name}\" este folosit in {usage.RecipeCount} retete si nu poate fi sters.");
                return;
            }

            _dialog.ShowInfo("Stergere indisponibila",
                "Stergerea ingredientelor nu este disponibila in aceasta versiune.");
        }
        catch (AppDbException ex)
        {
            _dialog.ShowError(ex.FriendlyMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

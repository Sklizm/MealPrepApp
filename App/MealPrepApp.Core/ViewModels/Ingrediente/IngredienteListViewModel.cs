using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.ViewModels.Ingrediente;

public sealed class IngredientGroup : ObservableCollection<Ingredient>
{
    public string Name { get; }

    public IngredientGroup(string name, IEnumerable<Ingredient> items) : base(items)
    {
        Name = name;
    }
}

/// <summary>
/// The ingredient list sub-view: a search box, category dropdown, and a list that is either
/// flat ("Toate") or grouped by <c>IngredientCategoryName</c> ("Categorii").
/// </summary>
public sealed partial class IngredienteListViewModel : ViewModelBase, IAsyncLoadable, IDisposable
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

    [RelayCommand] private Task Retry()=>RefreshAsync();
    private bool _loaded;
    private int _requestVersion;
    private CancellationTokenSource? _loadCts;
    public void CancelPending()
    {
        ++_requestVersion;
        _loadCts?.Cancel();
        _searchCts?.Cancel();
        IsBusy = false;
    }

    private bool _disposed;
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ++_requestVersion;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }

    private CancellationTokenSource? _searchCts;
    private const int SearchDebounceMs = 300;

    public ObservableCollection<Ingredient> Ingredients { get; } = new();
    public ObservableCollection<IngredientCategory> Categories { get; } = new();
    public ObservableCollection<IngredientGroup> GroupedIngredients { get; } = new();

    public IEnumerable IngredientsView => UseGrouping ? (IEnumerable)GroupedIngredients : (IEnumerable)Ingredients;

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
        Ingredients.CollectionChanged+=(_,_)=>UpdateGrouping();
    }

    partial void OnUseGroupingChanged(bool value)
    {
        UpdateGrouping();
        OnPropertyChanged(nameof(IngredientsView));
    }

    private void UpdateGrouping()
    {
        GroupedIngredients.Clear();
        if (!UseGrouping) return;

        var groups = Ingredients
            .GroupBy(i => string.IsNullOrWhiteSpace(i.IngredientCategoryName) ? UngroupedLabel : i.IngredientCategoryName)
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            GroupedIngredients.Add(new IngredientGroup(group.Key, group));
        }
    }

    public async Task LoadAsync()
    {
        if (_disposed) return;
        if (_loaded && !_disposed)
            return;
        ClearError();
        IsBusy = true;
        try
        {
            var categories = await _lookups.GetIngredientCategoriesAsync();
            if (_disposed) return;
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
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppDiagnostics.Record(ex);
            ErrorMessage = "Operatia nu a putut fi finalizata. Consulta jurnalul aplicatiei.";
        }
        finally
        {
            IsBusy = false;
            _loaded = true;
        }
    }

    private async Task RefreshAsync()
    {
        if (_disposed) return;
        var version = ++_requestVersion;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        ClearError();
        IsBusy = true;
        try
        {
            var term = SearchTerm?.Trim() ?? "";
            var categoryId = SelectedCategory is { IngredientCategoryID: > 0 } category
                ? category.IngredientCategoryID : (int?)null;
            var all = await _ingredients.GetIngredientsAsync(categoryId, _loadCts.Token);
            var rows = all.Where(i => term.Length == 0 ||
                i.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)).ToList();
            if (version != _requestVersion) return;

            Ingredients.Clear();
            foreach (var row in rows)
            {
                if (string.IsNullOrEmpty(row.IngredientCategoryName))
                    row.IngredientCategoryName = UngroupedLabel;
                Ingredients.Add(row);
            }
            UpdateGrouping();
            OnPropertyChanged(nameof(IngredientsView));
        }
        catch (AppDbException ex)
        {
            if (version == _requestVersion) ErrorMessage = ex.FriendlyMessage;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppDiagnostics.Record(ex);
            if (version == _requestVersion) ErrorMessage = "Operatia nu a putut fi finalizata. Consulta jurnalul aplicatiei.";
        }
        finally
        {
            if (version == _requestVersion) IsBusy = false;
        }
    }

    partial void OnSelectedCategoryChanged(IngredientCategory? value)
    {
        if (_loaded && !_disposed)
            _ = RefreshAsync();
    }

    /// <summary>As-you-type filtering with a 300ms debounce — each keystroke cancels the
    /// previous pending refresh so only one query fires once the user pauses.</summary>
    partial void OnSearchTermChanged(string value)
    {
        if (!_loaded || _disposed) return;
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        ++_requestVersion;
        _loadCts?.Cancel();
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
        var added = await _dialog.ShowDialogAsync(dialogVm);
        if (added)
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task EditNutrition(Ingredient? item)
    {
        if (item is null)
            return;

        var dialogVm = _services.GetRequiredService<IngredientNutritionDialogViewModel>();
        await dialogVm.LoadAsync(item);
        await _dialog.ShowDialogAsync(dialogVm);
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
                await _dialog.ShowErrorAsync(
                    $"Ingredientul \"{item.Name}\" este folosit in {usage.RecipeCount} retete si nu poate fi sters.");
                return;
            }

            await _dialog.ShowInfoAsync("Stergere indisponibila",
                "Stergerea ingredientelor nu este disponibila in aceasta versiune.");
        }
        catch (AppDbException ex)
        {
            await _dialog.ShowErrorAsync(ex.FriendlyMessage);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppDiagnostics.Record(ex);
            ErrorMessage = "Operatia nu a putut fi finalizata. Consulta jurnalul aplicatiei.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

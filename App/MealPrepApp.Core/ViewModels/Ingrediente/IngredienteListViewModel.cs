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
/// Cached ingredient catalog with immediate local filtering and stable sort identities.
/// Retains the flat and grouped collection APIs for the legacy client.
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
    private IReadOnlyList<Ingredient> _source = Array.Empty<Ingredient>();
    private bool _populating;
    private int _requestVersion;
    private CancellationTokenSource? _loadCts;
    public void CancelPending()
    {
        ++_requestVersion;
        _loadCts?.Cancel();
        IsBusy = false;
    }

    private bool _disposed;
    public override void Dispose()
    {
        ReleaseLocalization();
        if (_disposed) return;
        _disposed = true;
        ++_requestVersion;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }

    public IReadOnlyList<CatalogCategory> CategoryIndex { get; private set; } = Array.Empty<CatalogCategory>();
    public int TotalIngredientCount => _source.Count;
    public int NutritionIngredientCount { get; private set; }
    [LocalizedProperty]
    public string CatalogSummary => L.Text("Catalog.Summary", L.Count("Count.Ingredients", TotalIngredientCount), NutritionIngredientCount);
    [LocalizedProperty]
    public string SelectedCategoryTitle => BuiltInLabels.Display(SelectedCategory?.Name ?? AllCategories.Name);
    [LocalizedProperty]
    public string ResultSummary => L.Count("Count.CatalogResults", Ingredients.Count).ToString();
    public CatalogCategory? CatalogSelection
    {
        get => CategoryIndex.FirstOrDefault(c => c.Category == SelectedCategory);
        set { if (value != null) SelectedCategory = value.Category; }
    }
    public IReadOnlyList<string> SortOrders { get; } = [IngredientSortIds.NameAscending, IngredientSortIds.NameDescending, IngredientSortIds.Category, IngredientSortIds.MissingNutrition];
    [ObservableProperty] private string _sortOrder = IngredientSortIds.NameAscending;
    [LocalizedProperty]
    public string EmptyMessage => _source.Count == 0 ? L.Text("Text.AddYourFirstIngredientToGetStarted")
        : !string.IsNullOrWhiteSpace(SearchTerm) ? L.Text("Text.NoIngredientsFoundTryAnotherSearch") : L.Text("Catalog.EmptyCategory");
    public bool IsEmpty => Ingredients.Count == 0;
    partial void OnSortOrderChanged(string value) => ApplyFilters();
    [RelayCommand] private void SelectCatalogCategory(CatalogCategory? entry)
    {
        if (entry != null) SelectedCategory = entry.Category;
    }

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
        if (_populating) return;
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

    public Task LoadAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_disposed) return;
        var version = ++_requestVersion;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;
        ClearError();
        IsBusy = true;
        try
        {
            var categories = await _lookups.GetIngredientCategoriesAsync(token);
            if (version != _requestVersion || _disposed) return;
            var all = await _ingredients.GetIngredientsAsync(null, token);
            if (version != _requestVersion || _disposed) return;
            var selectedId = SelectedCategory?.IngredientCategoryID ?? 0;
            _source = all;
            NutritionIngredientCount = all.Count(i => i.HasNutrition);
            _populating = true;
            try
            {
                Categories.Clear();
                Categories.Add(AllCategories);
                // Stable identity order, independent of translated labels and search.
                foreach (var category in categories.Concat(all.Where(i => i.IngredientCategoryID is > 0)
                    .Select(i => new IngredientCategory { IngredientCategoryID = i.IngredientCategoryID!.Value, Name = i.IngredientCategoryName ?? UngroupedLabel }))
                    .DistinctBy(c => c.IngredientCategoryID).OrderBy(c => c.IngredientCategoryID))
                    Categories.Add(category);
                Categories.Add(new IngredientCategory { IngredientCategoryID = -1, Name = UngroupedLabel });
                SelectedCategory = Categories.FirstOrDefault(c => c.IngredientCategoryID == selectedId) ?? AllCategories;
                CategoryIndex = Categories.Select(c => new CatalogCategory(c,
                    c.IngredientCategoryID == 0 ? all.Count : all.Count(i => CategoryId(i) == c.IngredientCategoryID))).ToArray();
            }
            finally { _populating = false; }
            OnPropertyChanged(nameof(CategoryIndex));
            OnPropertyChanged(nameof(CatalogSelection));
            OnPropertyChanged(nameof(TotalIngredientCount));
            OnPropertyChanged(nameof(NutritionIngredientCount));
            OnPropertyChanged(nameof(CatalogSummary));
            ApplyFilters();
        }
        catch (AppDbException ex)
        {
            if (version == _requestVersion) ErrorContent = ex.LocalizedMessage;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppDiagnostics.Record(ex);
            if (version == _requestVersion) ErrorContent = L.Message("Text.TheOperationCouldNotBeCompletedCheckTheAppLog");
        }
        finally
        {
            if (version == _requestVersion) IsBusy = false;
        }
    }

    private static int CategoryId(Ingredient item) => item.IngredientCategoryID is > 0 ? item.IngredientCategoryID.Value : -1;

    partial void OnSelectedCategoryChanged(IngredientCategory? value) { ApplyFilters(); OnPropertyChanged(nameof(CatalogSelection)); }
    partial void OnSearchTermChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        if (_disposed || _populating) return;
        var categoryId = SelectedCategory?.IngredientCategoryID ?? 0;
        var term = SearchTerm?.Trim() ?? "";
        var rows = _source.Where(i => (categoryId == 0 || CategoryId(i) == categoryId)
            && i.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase));
        var order = Categories.Select((c, index) => (c.IngredientCategoryID, index)).ToDictionary(c => c.IngredientCategoryID, c => c.index);
        rows = SortOrder switch
        {
            IngredientSortIds.NameDescending => rows.OrderByDescending(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(i => i.IngredientID),
            IngredientSortIds.Category => rows.OrderBy(i => order.GetValueOrDefault(CategoryId(i), int.MaxValue)).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(i => i.IngredientID),
            IngredientSortIds.MissingNutrition => rows.OrderBy(i => i.HasNutrition).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(i => i.IngredientID),
            _ => rows.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(i => i.IngredientID)
        };
        _populating = true;
        try { Ingredients.Clear(); foreach (var row in rows) Ingredients.Add(row); }
        finally { _populating = false; }
        UpdateGrouping();
        OnPropertyChanged(nameof(IngredientsView));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(SelectedCategoryTitle));
        OnPropertyChanged(nameof(ResultSummary));
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
        if (await _dialog.ShowDialogAsync(dialogVm)) await RefreshAsync();
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
                    L.Message("Ingredient.InUse", item.Name, usage.RecipeCount));
                return;
            }

            await _dialog.ShowInfoAsync(L.Message("Text.DeletionUnavailable"),
                L.Message("Text.DeletingIngredientsIsUnavailableInThisVersion"));
        }
        catch (AppDbException ex)
        {
            await _dialog.ShowErrorAsync(ex.LocalizedMessage);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppDiagnostics.Record(ex);
            ErrorContent = L.Message("Text.TheOperationCouldNotBeCompletedCheckTheAppLog");
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed record CatalogCategory(IngredientCategory Category, int Count)
{
    public string Name => Category.Name;
    public string Symbol => Category.IngredientCategoryID switch { 0 => "≡", -1 => "○", _ => "◇" };
    // Presentation identity only: custom and synthetic categories use the existing food box.
    public string Illustration => Name switch
    {
        "Produse" => "Produce",
        "Lactate si oua" => "Dairy",
        "Carne si peste" => "Meat",
        "Conserve" => "Jar",
        "Condimente si ierburi" => "Herbs",
        "Cereale si paste" => "Pasta",
        "Bauturi" => "Drink",
        _ => "Other"
    };
}

public static class IngredientSortIds
{
    public const string NameAscending = "Catalog.NameAscending";
    public const string NameDescending = "Catalog.NameDescending";
    public const string Category = "Catalog.Category";
    public const string MissingNutrition = "Catalog.MissingNutrition";
}

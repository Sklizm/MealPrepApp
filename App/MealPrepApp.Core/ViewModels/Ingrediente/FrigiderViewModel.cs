using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.ViewModels.Ingrediente;

/// <summary>
/// The pantry sub-view: lists what the current user has in stock and lets them
/// add / edit / remove rows. Backed by sp_GetPantry + sp_AddPantryItem +
/// sp_UpdatePantryQuantity + sp_RemovePantryItem.
/// </summary>
public sealed partial class FrigiderViewModel : ViewModelBase, IAsyncLoadable, IDisposable
{
    private readonly PantryRepository _pantry;
    private readonly IngredientRepository _ingredients;
    private readonly LookupRepository _lookups;
    private readonly ISessionService _session;
    private readonly IDialogService _dialog;
    private readonly IServiceProvider _services;

    private bool _populating, _disposed;
    private CancellationTokenSource? _read;
    private int _version;
    private IReadOnlyList<IngredientCategory> _categoryLookups = Array.Empty<IngredientCategory>();
    private IReadOnlyList<PantryItem> _visibleItems = Array.Empty<PantryItem>();
    private IReadOnlyList<PantryGroup> _groups = Array.Empty<PantryGroup>();
    [ObservableProperty] private bool _useGrouping;
    [ObservableProperty] private bool _isMutating;
    [ObservableProperty] private string? _selectedCategory;
    [ObservableProperty] private string _searchTerm = "";
    [ObservableProperty] private string _sortOrder = PantrySortIds.Ingredient;
    public IReadOnlyList<string> SortOrders { get; } = new[] { PantrySortIds.Ingredient, PantrySortIds.Quantity, PantrySortIds.RecentlyAdded };
    public IReadOnlyList<PantryItem> VisibleItems => _visibleItems;
    public IReadOnlyList<FridgeStockRow> DisplayRows { get; private set; } = Array.Empty<FridgeStockRow>();
    public IReadOnlyList<PantryGroup> Groups => _groups;
    public IReadOnlyList<FridgeCategory> Categories { get; private set; } = Array.Empty<FridgeCategory>();
    [LocalizedProperty]
    public string CategoryTitle => SelectedCategory ?? L.Text("Text.AllIngredients");
    [LocalizedProperty]
    public string StockSummary => L.Text("Pantry.Summary", L.Count("Count.Ingredients", Items.Select(i => i.IngredientID).Distinct().Count()), L.Count("Count.Stock", Items.Count));
    public bool IsEmpty => _visibleItems.Count == 0;
    [LocalizedProperty]
    public string EmptyMessage => SearchTerm.Length > 0 ? L.Text("Text.NoIngredientsFoundTryAnotherSearch") : L.Text("Text.NoIngredientsHereAddItemsToYourPantry");
    partial void OnSearchTermChanged(string value) => RebuildRows();
    partial void OnSortOrderChanged(string value) => RebuildRows();
    partial void OnSelectedCategoryChanged(string? value)
    {
        foreach (var category in Categories) category.IsSelected = category.Name == value;
        RebuildRows(); OnPropertyChanged(nameof(CategoryTitle));
    }
    [RelayCommand] private void SelectCategory(string? category) => SelectedCategory = category;
    [RelayCommand] private Task Cook() => _services.GetRequiredService<INavigationService>()
        .NavigateToAsync<MealPrepApp.ViewModels.Retete.ReteteListViewModel>(vm => vm.PrepareDestination("Frigider", ""));

    private static string CategoryOf(PantryItem item) => string.IsNullOrWhiteSpace(item.CategoryName) ? "Fără categorie" : item.CategoryName;
    private void RebuildCategories()
    {
        var counts = Items.GroupBy(CategoryOf).ToDictionary(g => g.Key, g => g.Select(i => i.IngredientID).Distinct().Count());
        var names = FridgeCategory.DefaultNames.Concat(_categoryLookups.Select(c => c.Name)).Concat(counts.Keys).Distinct().ToArray();
        Categories = names.Select(name => new FridgeCategory(name, counts.GetValueOrDefault(name), name == SelectedCategory)).ToArray();
        OnPropertyChanged(nameof(Categories)); OnPropertyChanged(nameof(StockSummary));
    }
    private void RebuildRows()
    {
        var filtered = Items.Where(i => (SelectedCategory == null || CategoryOf(i) == SelectedCategory)
            && i.IngredientName.Contains(SearchTerm.Trim(), StringComparison.CurrentCultureIgnoreCase));
        _visibleItems = (SortOrder switch { PantrySortIds.Quantity => filtered.OrderBy(i => i.Quantity), PantrySortIds.RecentlyAdded => filtered.OrderByDescending(i => i.AddedAt), _ => filtered.OrderBy(i => i.IngredientName) }).ToArray();
        _groups = _visibleItems.GroupBy(CategoryOf).OrderBy(g => g.Key).Select(g => new PantryGroup(g.Key, g)).ToArray();
        DisplayRows = SelectedCategory == null
            ? _groups.SelectMany(g => g.Select((item, index) => new FridgeStockRow(item, index == 0))).ToArray()
            : _visibleItems.Select(item => new FridgeStockRow(item, false)).ToArray();
        OnPropertyChanged(nameof(DisplayRows));
        OnPropertyChanged(nameof(VisibleItems)); OnPropertyChanged(nameof(Groups));
        OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(EmptyMessage));
    }
    private void Rebuild() { RebuildCategories(); RebuildRows(); }
    private void CancelRead() { ++_version; var old = _read; _read = null; old?.Cancel(); old?.Dispose(); }
    public override void Dispose() { ReleaseLocalization(); if (_disposed) return; _disposed = true; CancelRead(); }
    [RelayCommand] private Task Retry()=>RefreshAsync();
    [RelayCommand] private async Task SaveQuantity(PantryItem? item)
    {
        if(item is null || _disposed || IsBusy)return;
        if(item.Quantity<=0){ErrorContent = L.Message("Text.TheQuantityMustBePositive");return;}
        CancelRead(); IsBusy=true; IsMutating=true;ClearError();
        try {await _pantry.UpdatePantryQuantityAsync(item.UserPantryID,_session.CurrentUserId,item.Quantity);NotificationContent = L.Message("Text.QuantityUpdated");}
        catch(AppDbException ex){ErrorContent = ex.LocalizedMessage;}
        catch(Exception ex){AppDiagnostics.Record(ex);ErrorContent = L.Message("Text.TheQuantityCouldNotBeUpdated");}
        finally{IsBusy=false; IsMutating=false; Rebuild();}
    }
    public ObservableCollection<PantryItem> Items { get; } = new();

    public FrigiderViewModel(
        PantryRepository pantry,
        IngredientRepository ingredients,
        LookupRepository lookups,
        ISessionService session,
        IDialogService dialog,
        IServiceProvider services)
    {
        _pantry = pantry;
        _ingredients = ingredients;
        _lookups = lookups;
        _session = session;
        _dialog = dialog;
        _services = services;
        Items.CollectionChanged += (_, _) => { if (!_populating) Rebuild(); };
        Rebuild();
    }

    public Task LoadAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_disposed || IsMutating) return;
        CancelRead();
        var version = _version;
        var source = _read = new CancellationTokenSource();
        ClearError(); IsBusy = true;
        try
        {
            var user = _session.CurrentUserId;
            var stock = _pantry.GetPantryAsync(user, source.Token);
            var categories = _lookups.GetIngredientCategoriesAsync(source.Token);
            await Task.WhenAll(stock, categories);
            if (_disposed || version != _version) return;
            _categoryLookups = await categories;
            _populating = true;
            try { Items.Clear(); foreach (var row in await stock) Items.Add(row); }
            finally { _populating = false; }
            Rebuild();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!_disposed && version == _version)
            {
                AppDiagnostics.Record(ex);
                ErrorContent = ex is AppDbException db ? db.LocalizedMessage : L.Message("Text.ThePantryCouldNotBeLoadedTryAgain");
            }
        }
        finally
        {
            if (version == _version) { IsBusy = false; _read = null; source.Dispose(); }
        }
    }

    private async Task EditStockAsync(PantryItem? item, bool add)
    {
        if (_disposed || IsBusy) return;
        CancelRead(); IsBusy = true; IsMutating = true; ClearError();
        var saved = false;
        try
        {
            var vm = _services.GetRequiredService<PantryItemDialogViewModel>();
            if (add) await vm.InitForAddAsync(); else await vm.InitForEditAsync(item!);
            saved = await _dialog.ShowDialogAsync(vm);
        }
        catch (Exception ex) { AppDiagnostics.Record(ex); ErrorContent = L.Message("Text.TheEditorCouldNotBeOpened"); }
        finally { IsBusy = false; IsMutating = false; }
        if (saved && !_disposed) { NotificationContent = L.Message("Text.StockUpdated"); await RefreshAsync(); }
    }
    [RelayCommand] private Task AddItem() => EditStockAsync(null, true);
    [RelayCommand] private Task EditItem(PantryItem? item) => item == null ? Task.CompletedTask : EditStockAsync(item, false);
    [RelayCommand]
    private async Task RemoveItem(PantryItem? item)
    {
        if (item == null || _disposed || IsBusy) return;
        CancelRead(); IsBusy = true; IsMutating = true; ClearError();
        try
        {
            if (!await _dialog.ConfirmAsync(L.Message("Text.RemoveFromPantry"), L.Message("Pantry.RemovePrompt", item.IngredientName))) return;
            await _pantry.RemovePantryItemAsync(item.UserPantryID, _session.CurrentUserId);
            if (!_disposed) { Items.Remove(item); NotificationContent = L.Message("Text.IngredientRemovedFromPantry"); }
        }
        catch (Exception ex)
        {
            AppDiagnostics.Record(ex);
            if (!_disposed) ErrorContent = ex is AppDbException db ? db.LocalizedMessage : L.Message("Text.TheIngredientCouldNotBeRemoved");
        }
        finally { IsBusy = false; IsMutating = false; }
    }
}

public sealed class PantryGroup(string name, IEnumerable<PantryItem> items) : ObservableCollection<PantryItem>(items) { public string Name { get; } = name; }

public sealed partial class FridgeCategory(string name, int count, bool selected) : LocalizedObservableObject
{
    public string Name { get; } = name;
    public int Count { get; } = count;
    [ObservableProperty] private bool _isSelected = selected;
    public static readonly string[] DefaultNames = ["Produse", "Lactate si oua", "Carne si peste", "Conserve", "Condimente si ierburi", "Cereale si paste", "Bauturi", "Altele"];
    public bool HasStock => Count > 0;
    public double IllustrationOpacity => HasStock ? 1 : .55;
    [LocalizedProperty]
    public string Summary => Count == 0 ? L.Text("Text.NoStock") : L.Count("Count.Ingredients", Count).ToString();
    [LocalizedProperty]
    public string AccessibleName => $"{BuiltInLabels.Display(Name)}, {Summary}";
    public string Illustration => Name.ToLowerInvariant() switch
    {
        var n when n.Contains("produse") || n.Contains("legum") || n.Contains("fruct") => "Produce",
        var n when n.Contains("lactate") => "Dairy",
        var n when n.Contains("carne") => "Meat",
        var n when n.Contains("conserve") => "Jar",
        var n when n.Contains("condiment") => "Herbs",
        var n when n.Contains("cereale") => "Pasta",
        var n when n.Contains("bauturi") || n.Contains("băuturi") => "Drink",
        _ => "Other"
    };
}

public sealed record FridgeStockRow(PantryItem Item, bool StartsCategory);

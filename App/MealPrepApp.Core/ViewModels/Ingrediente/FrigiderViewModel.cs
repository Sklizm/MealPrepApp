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
/// The Frigider (pantry) sub-view: lists what the current user has in stock and lets them
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
    [ObservableProperty] private string _sortOrder = "Ingredient";
    public IReadOnlyList<string> SortOrders { get; } = new[] { "Ingredient", "Cantitate", "Adăugat recent" };
    public IReadOnlyList<PantryItem> VisibleItems => _visibleItems;
    public IReadOnlyList<FridgeStockRow> DisplayRows { get; private set; } = Array.Empty<FridgeStockRow>();
    public IReadOnlyList<PantryGroup> Groups => _groups;
    public IReadOnlyList<FridgeCategory> Categories { get; private set; } = Array.Empty<FridgeCategory>();
    public string CategoryTitle => SelectedCategory ?? "Toate ingredientele";
    public string StockSummary => $"{Items.Select(i => i.IngredientID).Distinct().Count()} ingrediente · {Items.Count} poziții în stoc";
    public bool IsEmpty => _visibleItems.Count == 0;
    public string EmptyMessage => SearchTerm.Length > 0 ? "Niciun ingredient găsit. Încearcă altă căutare." : "Nu ai ingrediente aici. Adaugă produse în frigider.";
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
        _visibleItems = (SortOrder switch { "Cantitate" => filtered.OrderBy(i => i.Quantity), "Adăugat recent" => filtered.OrderByDescending(i => i.AddedAt), _ => filtered.OrderBy(i => i.IngredientName) }).ToArray();
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
    public void Dispose() { if (_disposed) return; _disposed = true; CancelRead(); }
    [RelayCommand] private Task Retry()=>RefreshAsync();
    [RelayCommand] private async Task SaveQuantity(PantryItem? item)
    {
        if(item is null || _disposed || IsBusy)return;
        if(item.Quantity<=0){ErrorMessage="Cantitatea trebuie să fie pozitivă.";return;}
        CancelRead(); IsBusy=true; IsMutating=true;ClearError();
        try {await _pantry.UpdatePantryQuantityAsync(item.UserPantryID,_session.CurrentUserId,item.Quantity);Notification="Cantitatea a fost actualizată.";}
        catch(AppDbException ex){ErrorMessage=ex.FriendlyMessage;}
        catch(Exception ex){AppDiagnostics.Record(ex);ErrorMessage="Cantitatea nu a putut fi actualizată.";}
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
                ErrorMessage = ex is AppDbException db ? db.FriendlyMessage : "Frigiderul nu a putut fi încărcat. Încearcă din nou.";
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
        catch (Exception ex) { AppDiagnostics.Record(ex); ErrorMessage = "Editorul nu a putut fi deschis."; }
        finally { IsBusy = false; IsMutating = false; }
        if (saved && !_disposed) { Notification = "Stocul a fost actualizat."; await RefreshAsync(); }
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
            if (!await _dialog.ConfirmAsync("Șterge din frigider", $"Ștergi «{item.IngredientName}» din frigider?")) return;
            await _pantry.RemovePantryItemAsync(item.UserPantryID, _session.CurrentUserId);
            if (!_disposed) { Items.Remove(item); Notification = "Ingredientul a fost șters din frigider."; }
        }
        catch (Exception ex)
        {
            AppDiagnostics.Record(ex);
            if (!_disposed) ErrorMessage = ex is AppDbException db ? db.FriendlyMessage : "Ingredientul nu a putut fi șters.";
        }
        finally { IsBusy = false; IsMutating = false; }
    }
}

public sealed class PantryGroup(string name, IEnumerable<PantryItem> items) : ObservableCollection<PantryItem>(items) { public string Name { get; } = name; }

public sealed partial class FridgeCategory(string name, int count, bool selected) : ObservableObject
{
    public string Name { get; } = name;
    public int Count { get; } = count;
    [ObservableProperty] private bool _isSelected = selected;
    public static readonly string[] DefaultNames = ["Produse", "Lactate si oua", "Carne si peste", "Conserve", "Condimente si ierburi", "Cereale si paste", "Bauturi", "Altele"];
    public bool HasStock => Count > 0;
    public double IllustrationOpacity => HasStock ? 1 : .55;
    public string Summary => Count == 0 ? "Fără stoc" : Count == 1 ? "1 ingredient" : $"{Count} ingrediente";
    public string AccessibleName => $"{Name}, {Summary}";
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

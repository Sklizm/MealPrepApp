using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.ViewModels.Retete;

/// <summary>
/// The Retete list screen: sidebar (Toate / Favorite / Recente), title search, category
/// filter, and a card grid. Cards have per-card actions — open detail, delete — there is
/// no select-then-act. "+ Adauga reteta" and a card click navigate into the shell content region.
/// </summary>
public sealed partial class ReteteListViewModel : ViewModelBase, IAsyncLoadable, IDisposable
{
    private const int PageSize = 40;

    /// <summary>Category-filter sentinel: id 0 means "all categories".</summary>
    private static readonly Category AllCategories = new() { CategoryID = 0, Name = "Toate categoriile" };

    private readonly RecipeRepository _recipes;
    private readonly DraftRepository _drafts;
    private readonly FavoriteRepository _favorites;
    private readonly DashboardRepository _dashboard;
    private readonly LookupRepository _lookups;
    private readonly ISessionService _session;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialog;
    private readonly IServiceProvider _services;

    private readonly PantryRepository? _pantry;
    [RelayCommand] private Task ShowPantry() {ActiveFilter="Frigider";PageNumber=1;return RefreshAsync();}
    [RelayCommand] private Task Retry()=>RefreshAsync();
    private bool _loaded;
    private int _requestVersion;
    private CancellationTokenSource? _loadCts;
    [ObservableProperty] private int _pageNumber = 1;
    [ObservableProperty] private bool _hasNextPage;

    [RelayCommand]
    private Task PreviousPage()
    {
        if (PageNumber > 1) PageNumber--;
        return RefreshAsync();
    }

    [RelayCommand]
    private Task NextPage()
    {
        if (HasNextPage) PageNumber++;
        return RefreshAsync();
    }

    private bool _disposed;
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Preferences.PropertyChanged-=OnPreferencesChanged;
        _requestVersion++;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }

    private CancellationTokenSource? _searchCts;
    private const int SearchDebounceMs = 300;

    public ObservableCollection<RecipeListItem> Recipes { get; } = new();
    public ObservableCollection<RecipeDraftListItem> Drafts { get; } = new();
    public ObservableCollection<Category> Categories { get; } = new();

    /// <summary>Active sidebar entry: "Toate", "Favorite", "Recente" or "Drafts".</summary>
    [ObservableProperty]
    private string _activeFilter = "Toate";

    public PresentationPreferences Preferences { get; }
    [ObservableProperty] private bool _listView;
    [ObservableProperty] private string _sortOrder="Titlu";
    public IReadOnlyList<string> SortOrders { get; } = new[] { "Titlu", "Cele mai noi", "Timp de preparare" };
    partial void OnListViewChanged(bool value) { Preferences.RecipeListView=value; }
    partial void OnSortOrderChanged(string value) { if(_loaded) {PageNumber=1;_ = RefreshAsync();} }
    private void SortRows()
    {
        var sorted=SortOrder switch { "Cele mai noi" => Recipes.OrderByDescending(r=>r.CreatedAt), "Timp de preparare" => Recipes.OrderBy(r=>(r.PrepTimeMinutes ?? 0)+(r.CookTimeMinutes ?? 0)), _ => Recipes.OrderBy(r=>r.Title) };
        var rows=sorted.ToArray(); Recipes.Clear(); foreach(var row in rows) Recipes.Add(row);
    }
    public bool IsShowingDrafts => ActiveFilter == "Drafts";
    public bool IsShowingRecipes => !IsShowingDrafts;

    partial void OnActiveFilterChanged(string value)
    {
        OnPropertyChanged(nameof(IsShowingDrafts));
        OnPropertyChanged(nameof(IsShowingRecipes));
    }

    [ObservableProperty]
    private string _searchTerm = "";

    [ObservableProperty]
    private Category? _selectedCategory;

    public ReteteListViewModel(
        RecipeRepository recipes,
        DraftRepository drafts,
        FavoriteRepository favorites,
        DashboardRepository dashboard,
        LookupRepository lookups,
        ISessionService session,
        INavigationService navigation,
        IDialogService dialog,
        IServiceProvider services, PresentationPreferences? preferences=null, PantryRepository? pantry=null)
    {
        _recipes = recipes;
        _drafts = drafts;
        _favorites = favorites;
        _dashboard = dashboard;
        _lookups = lookups;
        _session = session;
        _navigation = navigation;
        _dialog = dialog;
        _services = services;
        _pantry=pantry;
        Preferences=preferences ?? new PresentationPreferences();
        _listView=Preferences.RecipeListView;
        Preferences.PropertyChanged+=OnPreferencesChanged;
    }

    private void OnPreferencesChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
    {if(e.PropertyName==nameof(Preferences.RecipeListView))ListView=Preferences.RecipeListView;}
    public async Task LoadAsync()
    {
        if (_disposed) return;
        if (_loaded) { await RefreshAsync(); return; }
        ClearError();
        IsBusy = true;
        try
        {
            var categories = await _lookups.GetCategoriesAsync();
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

    /// <summary>Re-runs the query for the current sidebar filter / search / category state.</summary>
    public Task ApplyDestinationAsync() { _searchCts?.Cancel(); PageNumber=1; return RefreshAsync(); }

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
            if (ActiveFilter == "Drafts")
            {
                var drafts = await _drafts.GetDraftsAsync(_session.CurrentUserId);
                if (version != _requestVersion) return;
                Drafts.Clear();
                foreach (var draft in drafts.Where(d=>(SelectedCategory is not {CategoryID:>0} || d.CategoryID==SelectedCategory.CategoryID) && (d.Title ?? "").Contains(SearchTerm.Trim(),StringComparison.CurrentCultureIgnoreCase)))
                    Drafts.Add(draft);
                HasNextPage=false;
                Recipes.Clear();
                return;
            }

            Drafts.Clear();
            var term = SearchTerm?.Trim() ?? "";
            IReadOnlyList<RecipeListItem> rows;
            if(ActiveFilter=="Frigider" && _pantry != null)
            {
                var stock=await _pantry.GetPantryAsync(_session.CurrentUserId);
                var matches=await _recipes.FindRecipesByIngredientsAsync(stock.Select(p=>p.IngredientID).Distinct().ToArray(),1);
                var filtered=matches.OrderBy(r=>r.Title).Where(r=>(SelectedCategory is not {CategoryID:>0} || r.CategoryID==SelectedCategory.CategoryID) && r.Title.Contains(term,StringComparison.CurrentCultureIgnoreCase)).ToArray();
                rows=filtered.Skip((PageNumber-1)*PageSize).Take(PageSize).Select(r=>new RecipeListItem{RecipeID=r.RecipeID,Title=r.Title,CategoryName=r.CategoryName,CategoryID=r.CategoryID,TotalCount=filtered.Length,MatchLabel=$"{r.MatchedIngredients}/{r.TotalIngredients} ingrediente potrivite • cantități neverificate"}).ToArray();
            }
            else rows = await _recipes.BrowseAsync(_session.CurrentUserId, ActiveFilter, term,
                SelectedCategory is { CategoryID: > 0 } category ? category.CategoryID : null,
                PageNumber, PageSize, token, SortOrder);
            if (version != _requestVersion) return;
            HasNextPage = rows.Count > 0 && PageNumber * PageSize < rows[0].TotalCount;

            Recipes.Clear();
            foreach (var row in rows)
                Recipes.Add(row);
            if(ActiveFilter=="Frigider")SortRows();
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

    [RelayCommand]
    private Task ShowToate()
    {
        PageNumber = 1;
        ActiveFilter = "Toate";
        return RefreshAsync();
    }

    [RelayCommand]
    private Task ShowFavorite()
    {
        PageNumber = 1;
        ActiveFilter = "Favorite";
        return RefreshAsync();
    }

    [RelayCommand]
    private Task ShowRecente()
    {
        PageNumber = 1;
        ActiveFilter = "Recente";
        return RefreshAsync();
    }

    [RelayCommand]
    private Task ShowDrafts()
    {
        PageNumber = 1;
        ActiveFilter = "Drafts";
        return RefreshAsync();
    }

    /// <summary>As-you-type filtering with a 300ms debounce — each keystroke cancels the
    /// previous pending refresh so only one query fires once the user pauses.</summary>
    partial void OnSearchTermChanged(string value)
    {
        if (!_loaded || _disposed) return;
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _requestVersion++;
        _loadCts?.Cancel();
        PageNumber = 1;
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

    /// <summary>Category-filter changes re-query immediately — but only once the initial load is done.</summary>
    partial void OnSelectedCategoryChanged(Category? value)
    {
        if (_loaded && !_disposed)
        {
            PageNumber = 1;
            _ = RefreshAsync();
        }
    }

    [RelayCommand]
    private Task OpenRecipe(RecipeListItem? item)
    {
        if (item is null)
            return Task.CompletedTask;

        var detail = _services.GetRequiredService<ReteteDetailViewModel>();
        detail.RecipeId = item.RecipeID;
        return _navigation.NavigateToAsync(detail);
    }

    [RelayCommand]
    private Task CreateRecipe()
    {
        var editor = _services.GetRequiredService<ReteteEditorViewModel>();
        editor.RecipeId = null;
        editor.DraftId = null;
        return _navigation.NavigateToAsync(editor);
    }

    [RelayCommand]
    private async Task DeleteRecipe(RecipeListItem? item)
    {
        if (item is null)
            return;

        if (!await _dialog.ConfirmAsync("Sterge reteta", $"Sterge reteta \"{item.Title}\"?"))
            return;

        IsBusy = true;
        try
        {
            await _recipes.DeleteRecipeAsync(item.RecipeID, _session.CurrentUserId);
            Recipes.Remove(item);
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

    [RelayCommand]
    private Task OpenDraft(RecipeDraftListItem? item)
    {
        if (item is null)
            return Task.CompletedTask;

        var editor = _services.GetRequiredService<ReteteEditorViewModel>();
        editor.RecipeId = null;
        editor.DraftId = item.DraftID;
        return _navigation.NavigateToAsync(editor);
    }

    [RelayCommand]
    private async Task DeleteDraft(RecipeDraftListItem? item)
    {
        if (item is null)
            return;

        var title = string.IsNullOrWhiteSpace(item.Title) ? "fara titlu" : item.Title;
        if (!await _dialog.ConfirmAsync("Sterge draft", $"Sterge draftul \"{title}\"?"))
            return;

        IsBusy = true;
        try
        {
            await _drafts.DeleteDraftAsync(item.DraftID, _session.CurrentUserId);
            Drafts.Remove(item);
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

    private static RecipeListItem MapRecent(RecentRecipe r) => new()
    {
        RecipeID = r.RecipeID,
        Title = r.Title,
        CategoryID = r.CategoryID,
        CategoryName = r.CategoryName,
        PrepTimeMinutes = r.PrepTimeMinutes,
        CookTimeMinutes = r.CookTimeMinutes,
        Servings = r.Servings,
        CreatedAt = r.CreatedAt,
    };
}

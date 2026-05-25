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
public sealed partial class ReteteListViewModel : ViewModelBase, IAsyncLoadable
{
    private const int PageSize = 200;

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

    private bool _loaded;
    private CancellationTokenSource? _searchCts;
    private const int SearchDebounceMs = 300;

    public ObservableCollection<RecipeListItem> Recipes { get; } = new();
    public ObservableCollection<RecipeDraftListItem> Drafts { get; } = new();
    public ObservableCollection<Category> Categories { get; } = new();

    /// <summary>Active sidebar entry: "Toate", "Favorite", "Recente" or "Drafts".</summary>
    [ObservableProperty]
    private string _activeFilter = "Toate";

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
        IServiceProvider services)
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
    }

    public async Task LoadAsync()
    {
        ClearError();
        IsBusy = true;
        try
        {
            var categories = await _lookups.GetCategoriesAsync();
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

    /// <summary>Re-runs the query for the current sidebar filter / search / category state.</summary>
    private async Task RefreshAsync()
    {
        ClearError();
        IsBusy = true;
        try
        {
            if (ActiveFilter == "Drafts")
            {
                var drafts = await _drafts.GetDraftsAsync(_session.CurrentUserId);
                Drafts.Clear();
                foreach (var draft in drafts)
                    Drafts.Add(draft);
                Recipes.Clear();
                return;
            }

            Drafts.Clear();
            var term = SearchTerm?.Trim() ?? "";
            IReadOnlyList<RecipeListItem> rows;

            if (term.Length > 0)
            {
                rows = await _recipes.SearchRecipesByTitleAsync(term, pageSize: PageSize);
            }
            else if (ActiveFilter == "Favorite")
            {
                rows = await _favorites.GetFavoriteRecipesAsync(_session.CurrentUserId, pageSize: PageSize);
            }
            else if (ActiveFilter == "Recente")
            {
                var recent = await _dashboard.GetRecentRecipesAsync(_session.CurrentUserId, topN: PageSize);
                rows = recent.Select(MapRecent).ToList();
            }
            else
            {
                var categoryId = SelectedCategory is { CategoryID: > 0 } c ? c.CategoryID : (int?)null;
                rows = await _recipes.GetRecipesAsync(categoryId: categoryId, pageSize: PageSize);
            }

            Recipes.Clear();
            foreach (var row in rows)
                Recipes.Add(row);
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
    private Task ShowToate()
    {
        ActiveFilter = "Toate";
        return RefreshAsync();
    }

    [RelayCommand]
    private Task ShowFavorite()
    {
        ActiveFilter = "Favorite";
        return RefreshAsync();
    }

    [RelayCommand]
    private Task ShowRecente()
    {
        ActiveFilter = "Recente";
        return RefreshAsync();
    }

    [RelayCommand]
    private Task ShowDrafts()
    {
        ActiveFilter = "Drafts";
        return RefreshAsync();
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

    /// <summary>Category-filter changes re-query immediately — but only once the initial load is done.</summary>
    partial void OnSelectedCategoryChanged(Category? value)
    {
        if (_loaded)
            _ = RefreshAsync();
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

        if (!_dialog.Confirm("Sterge reteta", $"Sterge reteta \"{item.Title}\"?"))
            return;

        IsBusy = true;
        try
        {
            await _recipes.DeleteRecipeAsync(item.RecipeID, _session.CurrentUserId);
            Recipes.Remove(item);
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
        if (!_dialog.Confirm("Sterge draft", $"Sterge draftul \"{title}\"?"))
            return;

        IsBusy = true;
        try
        {
            await _drafts.DeleteDraftAsync(item.DraftID, _session.CurrentUserId);
            Drafts.Remove(item);
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

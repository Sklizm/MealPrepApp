using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
using MealPrepApp.ViewModels.Planificare;
using MealPrepApp.Views.Planificare;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.ViewModels.Retete;

/// <summary>
/// The full-screen recipe detail view: header + description + instructions + ingredient lines,
/// with favorite toggle, edit, delete and back actions. Shown in the shell content region
/// (the tab strip stays visible). <see cref="RecipeId"/> must be set before navigation.
/// </summary>
public sealed partial class ReteteDetailViewModel : ViewModelBase, IAsyncLoadable
{
    private readonly RecipeRepository _recipes;
    private readonly FavoriteRepository _favorites;
    private readonly ISessionService _session;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialog;
    private readonly IServiceProvider _services;

    /// <summary>The recipe to show. Set by the caller before <see cref="LoadAsync"/> runs.</summary>
    public int RecipeId { get; set; }

    [ObservableProperty]
    private RecipeFull? _recipe;

    [ObservableProperty]
    private bool _isFavorite;

    public ReteteDetailViewModel(
        RecipeRepository recipes,
        FavoriteRepository favorites,
        ISessionService session,
        INavigationService navigation,
        IDialogService dialog,
        IServiceProvider services)
    {
        _recipes = recipes;
        _favorites = favorites;
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
            Recipe = await _recipes.GetRecipeFullAsync(RecipeId);
            if (Recipe is null)
            {
                ErrorMessage = "Reteta nu a fost gasita.";
                return;
            }

            // No single-recipe "is favorite?" proc — derive it from the user's favorites list.
            var favorites = await _favorites.GetFavoriteRecipesAsync(_session.CurrentUserId, pageSize: 500);
            IsFavorite = favorites.Any(f => f.RecipeID == RecipeId);
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
    private async Task ToggleFavorite()
    {
        IsBusy = true;
        try
        {
            IsFavorite = await _favorites.ToggleFavoriteAsync(_session.CurrentUserId, RecipeId);
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
    private Task Edit()
    {
        var editor = _services.GetRequiredService<ReteteEditorViewModel>();
        editor.RecipeId = RecipeId;
        return _navigation.NavigateToAsync(editor);
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (Recipe is null)
            return;

        if (!_dialog.Confirm("Sterge reteta", $"Sterge reteta \"{Recipe.Title}\"?"))
            return;

        IsBusy = true;
        try
        {
            await _recipes.DeleteRecipeAsync(RecipeId, _session.CurrentUserId);
            await GoBackToListAsync();
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

    /// <summary>Opens the shared plan-meal modal pre-filled with this recipe; the slot defaults
    /// to the recipe's own category (or Breakfast if it has none).</summary>
    [RelayCommand]
    private async Task AddToPlan()
    {
        if (Recipe is null)
            return;

        var recipe = new RecipeListItem
        {
            RecipeID = Recipe.RecipeID,
            Title = Recipe.Title,
            CategoryID = Recipe.CategoryID,
            CategoryName = Recipe.CategoryName,
            Servings = Recipe.Servings,
        };

        var vm = _services.GetRequiredService<PlanMealDialogViewModel>();
        await vm.InitForAddAsync(DateTime.Today, Recipe.CategoryID ?? MealSlots.DefaultSlotId, recipe);
        _dialog.ShowDialog<PlanMealDialog>(vm);
    }

    [RelayCommand]
    private Task Back() => GoBackToListAsync();

    private Task GoBackToListAsync() => _navigation.NavigateToAsync<ReteteListViewModel>();
}

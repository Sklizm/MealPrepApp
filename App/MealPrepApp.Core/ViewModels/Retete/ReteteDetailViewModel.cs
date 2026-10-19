using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
using MealPrepApp.ViewModels.Planificare;
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
    private readonly NutritionRepository _nutritionRepository;
    private readonly FavoriteRepository _favorites;
    private readonly ISessionService _session;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialog;
    private readonly IServiceProvider _services;
    private readonly IFilePickerService _filePicker;

    /// <summary>The recipe to show. Set by the caller before <see cref="LoadAsync"/> runs.</summary>
    public int RecipeId { get; set; }

    [ObservableProperty]
    private RecipeFull? _recipe;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private byte[]? _photoBytes;

    [ObservableProperty]
    private RecipeNutritionSummary? _nutrition;

    public bool HasPhoto => PhotoBytes is not null && PhotoBytes.Length > 0;

    partial void OnPhotoBytesChanged(byte[]? value)
    {
        OnPropertyChanged(nameof(HasPhoto));
    }

    public ReteteDetailViewModel(
        RecipeRepository recipes,
        NutritionRepository nutrition,
        FavoriteRepository favorites,
        ISessionService session,
        INavigationService navigation,
        IDialogService dialog,
        IServiceProvider services,
        IFilePickerService filePicker)
    {
        _recipes = recipes;
        _nutritionRepository = nutrition;
        _favorites = favorites;
        _session = session;
        _navigation = navigation;
        _dialog = dialog;
        _services = services;
        _filePicker = filePicker;
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

            var photo = await _recipes.GetRecipePhotoAsync(RecipeId);
            PhotoBytes = photo?.ImageData;

            Nutrition = await _nutritionRepository.GetRecipeNutritionAsync(RecipeId);
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

        var confirmed = _dialog.Confirm(
            "Sterge reteta",
            $"Sigur vrei sa stergi reteta \"{Recipe.Title}\"?\nActiunea este ireversibila si va sterge si ingredientele asociate.");

        if (!confirmed)
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

    [RelayCommand]
    private async Task ChoosePhoto()
    {
        var filePath = await _filePicker.OpenFileDialogAsync(
            "Alege poza retetei",
            "Imagini|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Toate fisierele|*.*");

        if (string.IsNullOrEmpty(filePath))
            return;

        IsBusy = true;
        try
        {
            var imageData = await File.ReadAllBytesAsync(filePath);
            await _recipes.SetRecipePhotoAsync(RecipeId, _session.CurrentUserId, imageData, "image/jpeg");
            PhotoBytes = imageData;
        }
        catch (AppDbException ex)
        {
            _dialog.ShowError(ex.FriendlyMessage);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or InvalidOperationException)
        {
            _dialog.ShowError($"Poza nu a putut fi citita. Alege o imagine valida. Detalii: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeletePhoto()
    {
        if (!HasPhoto)
            return;

        if (!_dialog.Confirm("Sterge poza", "Sterge poza acestei retete?"))
            return;

        IsBusy = true;
        try
        {
            await _recipes.DeleteRecipePhotoAsync(RecipeId, _session.CurrentUserId);
            PhotoBytes = null;
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
        _dialog.ShowDialog(vm);
    }

    [RelayCommand]
    private Task Back() => GoBackToListAsync();

    private Task GoBackToListAsync() => _navigation.NavigateToAsync<ReteteListViewModel>();
}

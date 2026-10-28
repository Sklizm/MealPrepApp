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

    public CookingSession Cooking { get; }
    [ObservableProperty] private decimal _selectedServings=1;
    public bool CanScale => Recipe?.Servings is > 0;
    public bool CanEdit => Recipe?.UserID == _session.CurrentUserId;
    public string FavoriteLabel => IsFavorite ? "Elimină din favorite" : "Adaugă la favorite";
    public string PhotoLabel => HasPhoto ? "Schimbă fotografia" : "Adaugă fotografie";
    public RecipeNutritionSummary? ScaledNutrition => Nutrition is null ? null : ServingCalculator.Scale(Nutrition, CanScale ? ServingCalculator.Factor(Recipe!.Servings, SelectedServings) : 1);
    public IReadOnlyList<CookingIngredient> ScaledIngredients => Recipe?.Ingredients.Select(i=> new CookingIngredient { Name=i.IngredientName, Quantity=i.Quantity*(CanScale ? ServingCalculator.Factor(Recipe.Servings, SelectedServings) : 1), Unit=i.UnitAbbreviation, Notes=i.Notes }).ToArray() ?? Array.Empty<CookingIngredient>();
    partial void OnSelectedServingsChanged(decimal value) { if(value is <=0 or >1000) { SelectedServings=1; return; } OnPropertyChanged(nameof(ScaledIngredients)); OnPropertyChanged(nameof(ScaledNutrition)); }
    partial void OnRecipeChanged(RecipeFull? value) { SelectedServings=value?.Servings is >0 ? value.Servings.Value : 1; foreach(var name in new[]{nameof(CanScale),nameof(CanEdit),nameof(ScaledIngredients)}) OnPropertyChanged(name); }
    partial void OnNutritionChanged(RecipeNutritionSummary? value) => OnPropertyChanged(nameof(ScaledNutrition));
    partial void OnIsFavoriteChanged(bool value) => OnPropertyChanged(nameof(FavoriteLabel));
    [RelayCommand] private Task Cook()
    {
        if(Recipe is null) return Task.CompletedTask;
        Cooking.StartRecipe(Recipe,SelectedServings);
        return _navigation.NavigateToAsync(new CookingViewModel(Cooking));
    }
    [RelayCommand] private Task Retry() => LoadAsync();
    public bool HasPhoto => PhotoBytes is not null && PhotoBytes.Length > 0;

    partial void OnPhotoBytesChanged(byte[]? value)
    {
        OnPropertyChanged(nameof(HasPhoto));
        OnPropertyChanged(nameof(PhotoLabel));
    }

    public ReteteDetailViewModel(
        RecipeRepository recipes,
        NutritionRepository nutrition,
        FavoriteRepository favorites,
        ISessionService session,
        INavigationService navigation,
        IDialogService dialog,
        IServiceProvider services,
        IFilePickerService filePicker, CookingSession? cooking=null)
    {
        _recipes = recipes;
        _nutritionRepository = nutrition;
        _favorites = favorites;
        _session = session;
        _navigation = navigation;
        _dialog = dialog;
        _services = services;
        _filePicker = filePicker;
        Cooking=cooking ?? new CookingSession();
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
    private async Task ToggleFavorite()
    {
        IsBusy = true;
        try
        {
            IsFavorite = await _favorites.ToggleFavoriteAsync(_session.CurrentUserId, RecipeId);
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

        var confirmed = await _dialog.ConfirmAsync(
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
            var image = await Task.Run(() => RecipeImageProcessor.LoadAsync(filePath));
            var imageData = image.Image;
            await _recipes.SetRecipePhotoAsync(RecipeId, _session.CurrentUserId, imageData, "image/jpeg", image.Thumbnail);
            PhotoBytes = imageData;
        }
        catch (AppDbException ex)
        {
            await _dialog.ShowErrorAsync(ex.FriendlyMessage);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or InvalidOperationException or UnauthorizedAccessException)
        {
            await _dialog.ShowErrorAsync($"Poza nu a putut fi citita. Alege o imagine valida. Detalii: {ex.Message}");
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
    private async Task DeletePhoto()
    {
        if (!HasPhoto)
            return;

        if (!await _dialog.ConfirmAsync("Sterge poza", "Sterge poza acestei retete?"))
            return;

        IsBusy = true;
        try
        {
            await _recipes.DeleteRecipePhotoAsync(RecipeId, _session.CurrentUserId);
            PhotoBytes = null;
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
        if(CanScale) vm.Servings=(int)SelectedServings;
        await _dialog.ShowDialogAsync(vm);
    }

    [RelayCommand]
    private Task Back() => GoBackToListAsync();

    private Task GoBackToListAsync() => _navigation.NavigateToAsync<ReteteListViewModel>();
}

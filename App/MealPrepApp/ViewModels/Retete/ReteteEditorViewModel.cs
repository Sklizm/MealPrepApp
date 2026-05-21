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
/// The recipe editor — create (<see cref="RecipeId"/> null) or edit an existing recipe.
/// Every field is a form input; ingredient lines are an editable table. On edit it carries the
/// <c>RowVersion</c> from load: a stale token (error 50004) raises a reload-or-cancel conflict prompt.
/// </summary>
public sealed partial class ReteteEditorViewModel : ViewModelBase, IAsyncLoadable
{
    /// <summary>Soft cap mirrored by the editor's notes/description textboxes.</summary>
    public const int NotesMaxLength = 500;

    private readonly RecipeRepository _recipes;
    private readonly IngredientRepository _ingredients;
    private readonly LookupRepository _lookups;
    private readonly ISessionService _session;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialog;
    private readonly IServiceProvider _services;

    private IReadOnlyList<Ingredient> _allIngredients = Array.Empty<Ingredient>();
    private IReadOnlyList<Unit> _allUnits = Array.Empty<Unit>();
    private byte[] _rowVersion = Array.Empty<byte>();

    /// <summary>Recipe to edit, or null to create a new one. Set before <see cref="LoadAsync"/> runs.</summary>
    public int? RecipeId { get; set; }

    public bool IsEdit => RecipeId.HasValue;
    public string ScreenTitle => IsEdit ? "Editeaza reteta" : "Reteta noua";

    public ObservableCollection<Category> Categories { get; } = new();
    public ObservableCollection<RecipeIngredientRowViewModel> IngredientRows { get; } = new();

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string _instructions = "";

    [ObservableProperty]
    private int? _prepTimeMinutes;

    [ObservableProperty]
    private int? _cookTimeMinutes;

    [ObservableProperty]
    private int? _servings;

    [ObservableProperty]
    private Category? _selectedCategory;

    public ReteteEditorViewModel(
        RecipeRepository recipes,
        IngredientRepository ingredients,
        LookupRepository lookups,
        ISessionService session,
        INavigationService navigation,
        IDialogService dialog,
        IServiceProvider services)
    {
        _recipes = recipes;
        _ingredients = ingredients;
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
            // Lookup lists feed the category dropdown and every ingredient row's pickers.
            var categories = await _lookups.GetCategoriesAsync();
            Categories.Clear();
            foreach (var category in categories)
                Categories.Add(category);

            _allUnits = await _lookups.GetUnitsAsync();
            _allIngredients = await _ingredients.GetIngredientsAsync();

            if (IsEdit)
                await PopulateFromExistingAsync(RecipeId!.Value);

            OnPropertyChanged(nameof(ScreenTitle));
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

    private async Task PopulateFromExistingAsync(int recipeId)
    {
        var recipe = await _recipes.GetRecipeFullAsync(recipeId);
        if (recipe is null)
        {
            ErrorMessage = "Reteta nu a fost gasita.";
            return;
        }

        Title = recipe.Title;
        Description = recipe.Description;
        Instructions = recipe.Instructions;
        PrepTimeMinutes = recipe.PrepTimeMinutes;
        CookTimeMinutes = recipe.CookTimeMinutes;
        Servings = recipe.Servings;
        SelectedCategory = Categories.FirstOrDefault(c => c.CategoryID == recipe.CategoryID);
        _rowVersion = recipe.RowVersion;

        IngredientRows.Clear();
        foreach (var line in recipe.Ingredients)
        {
            var row = NewRow();
            row.SelectedIngredient = _allIngredients.FirstOrDefault(i => i.IngredientID == line.IngredientID);
            row.SelectedUnit = _allUnits.FirstOrDefault(u => u.UnitID == line.UnitID);
            row.Quantity = line.Quantity;
            row.Notes = line.Notes;
            IngredientRows.Add(row);
        }
    }

    private RecipeIngredientRowViewModel NewRow()
    {
        var row = new RecipeIngredientRowViewModel(_allIngredients, _allUnits);
        row.RemoveRequested += (s, _) =>
        {
            if (s is RecipeIngredientRowViewModel r)
                IngredientRows.Remove(r);
        };
        return row;
    }

    [RelayCommand]
    private void AddIngredientRow() => IngredientRows.Add(NewRow());

    [RelayCommand]
    private async Task Save()
    {
        ClearError();

        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorMessage = "Titlul retetei este obligatoriu.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Instructions))
        {
            ErrorMessage = "Instructiunile sunt obligatorii.";
            return;
        }

        if (IngredientRows.Any(r => !r.IsComplete))
        {
            ErrorMessage = "Completeaza fiecare rand de ingredient (ingredient, unitate, cantitate) sau elimina-l.";
            return;
        }

        var inputs = IngredientRows.Select(r => r.ToInput()).ToList();
        var categoryId = SelectedCategory?.CategoryID;
        var userId = _session.CurrentUserId;

        IsBusy = true;
        try
        {
            int recipeId;
            if (IsEdit)
            {
                recipeId = RecipeId!.Value;
                await _recipes.UpdateRecipeAsync(
                    recipeId, userId, categoryId, Title.Trim(), Trimmed(Description), Instructions.Trim(),
                    PrepTimeMinutes, CookTimeMinutes, Servings, inputs, _rowVersion);
            }
            else
            {
                recipeId = await _recipes.CreateRecipeAsync(
                    userId, categoryId, Title.Trim(), Trimmed(Description), Instructions.Trim(),
                    PrepTimeMinutes, CookTimeMinutes, Servings, inputs);
            }

            await NavigateToDetailAsync(recipeId);
        }
        catch (AppDbException ex) when (ex.ErrorNumber == 50004)
        {
            if (_dialog.Confirm("Conflict de editare",
                    "Reteta a fost modificata in alta sesiune. Reincarca varianta curenta? " +
                    "(modificarile tale nesalvate se vor pierde)"))
            {
                await ReloadAsync();
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

    [RelayCommand]
    private async Task Cancel()
    {
        if (!_dialog.Confirm("Renunta", "Renunti la modificari? Modificarile nesalvate se pierd."))
            return;

        if (IsEdit)
            await NavigateToDetailAsync(RecipeId!.Value);
        else
            await _navigation.NavigateToAsync<ReteteListViewModel>();
    }

    private Task NavigateToDetailAsync(int recipeId)
    {
        var detail = _services.GetRequiredService<ReteteDetailViewModel>();
        detail.RecipeId = recipeId;
        return _navigation.NavigateToAsync(detail);
    }

    private async Task ReloadAsync()
    {
        if (!IsEdit)
            return;

        await PopulateFromExistingAsync(RecipeId!.Value);
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

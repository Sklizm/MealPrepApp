using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
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
public sealed partial class ReteteEditorViewModel : ViewModelBase, IAsyncLoadable, ILeaveGuard
{
    /// <summary>Char cap on an ingredient's note. Must match RecipeIngredients.Notes NVARCHAR(255)
    /// in the DB — a longer value would be truncated/rejected by SQL Server (error 8152).</summary>
    public const int NotesMaxLength = 255;

    private readonly RecipeRepository _recipes;
    private readonly DraftRepository _drafts;
    private readonly IngredientRepository _ingredients;
    private readonly LookupRepository _lookups;
    private readonly ISessionService _session;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialog;
    private readonly IServiceProvider _services;

    private IReadOnlyList<Ingredient> _allIngredients = Array.Empty<Ingredient>();
    private IReadOnlyList<Unit> _allUnits = Array.Empty<Unit>();
    private string _savedState = "";
    private string Snapshot() => JsonSerializer.Serialize(new { Title, Description, Instructions, PrepTimeMinutes, CookTimeMinutes, Servings, Category=SelectedCategory?.CategoryID, Rows=IngredientRows.Select(r=> new { Ingredient=r.SelectedIngredient?.IngredientID, Unit=r.SelectedUnit?.UnitID, r.Quantity, r.Notes }) });
    public bool IsDirty => Snapshot() != _savedState;
    public async Task<bool> CanLeaveAsync()
    {
        if (IsBusy) return false;
        if (!IsDirty) return true;
        var decision=await _dialog.ChooseLeaveAsync();
        if(decision==LeaveDecision.Discard) return true;
        if(decision==LeaveDecision.Stay) return false;
        await SaveDraft();
        return !HasError && !IsDirty;
    }
    private byte[] _rowVersion = Array.Empty<byte>();

    /// <summary>Recipe to edit, or null to create a new one. Set before <see cref="LoadAsync"/> runs.</summary>
    public int? RecipeId { get; set; }

    /// <summary>Draft to load into the editor, or null for a blank recipe/new edit flow.</summary>
    public int? DraftId { get; set; }

    public bool IsEdit => RecipeId.HasValue;
    public bool IsDraft => DraftId.HasValue && !IsEdit;
    [LocalizedProperty]
    public string ScreenTitle => IsEdit ? L.Text("Text.EditRecipe") : IsDraft ? L.Text("Text.EditDraft") : L.Text("Recipe.New");

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
        DraftRepository drafts,
        IngredientRepository ingredients,
        LookupRepository lookups,
        ISessionService session,
        INavigationService navigation,
        IDialogService dialog,
        IServiceProvider services)
    {
        _recipes = recipes;
        _drafts = drafts;
        _ingredients = ingredients;
        _lookups = lookups;
        _session = session;
        _navigation = navigation;
        _dialog = dialog;
        _services = services;
        _savedState=Snapshot();
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
            else if (DraftId.HasValue)
                await PopulateFromDraftAsync(DraftId.Value);

            OnPropertyChanged(nameof(ScreenTitle));
            OnPropertyChanged(nameof(IsDraft));
            _savedState=Snapshot();
        }
        catch (AppDbException ex)
        {
            ErrorContent = ex.LocalizedMessage;
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

    private async Task PopulateFromExistingAsync(int recipeId)
    {
        var recipe = await _recipes.GetRecipeFullAsync(recipeId);
        if (recipe is null)
        {
            ErrorContent = L.Message("Text.RecipeNotFound");
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

    private async Task PopulateFromDraftAsync(int draftId)
    {
        var draft = await _drafts.GetDraftAsync(draftId, _session.CurrentUserId);
        if (draft is null)
        {
            ErrorContent = L.Message("Text.DraftNotFound");
            return;
        }

        DraftId = draft.DraftID;
        Title = draft.Title ?? "";
        Description = draft.Description;
        Instructions = draft.Instructions ?? "";
        PrepTimeMinutes = draft.PrepTimeMinutes;
        CookTimeMinutes = draft.CookTimeMinutes;
        Servings = draft.Servings;
        SelectedCategory = Categories.FirstOrDefault(c => c.CategoryID == draft.CategoryID);

        IngredientRows.Clear();
        var lines = DeserializeDraftIngredients(draft.IngredientsJson);
        foreach (var line in lines)
        {
            var row = NewRow();
            row.SelectedIngredient = line.IngredientID.HasValue
                ? _allIngredients.FirstOrDefault(i => i.IngredientID == line.IngredientID.Value)
                : null;
            row.SelectedUnit = line.UnitID.HasValue
                ? _allUnits.FirstOrDefault(u => u.UnitID == line.UnitID.Value)
                : null;
            row.Quantity = line.Quantity <= 0m ? 1m : line.Quantity;
            row.Notes = line.Notes;
            IngredientRows.Add(row);
        }
    }

    private static IReadOnlyList<DraftIngredientInput> DeserializeDraftIngredients(string? ingredientsJson)
    {
        if (string.IsNullOrWhiteSpace(ingredientsJson))
            return Array.Empty<DraftIngredientInput>();

        try
        {
            return JsonSerializer.Deserialize<List<DraftIngredientInput>>(ingredientsJson) ?? new List<DraftIngredientInput>();
        }
        catch (JsonException)
        {
            return Array.Empty<DraftIngredientInput>();
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
    private async Task SaveDraft()
    {
        if(IsBusy)return;
        ClearError();
        IsBusy = true;
        try
        {
            var draftInputs = IngredientRows.Select(r => new DraftIngredientInput
            {
                IngredientID = r.SelectedIngredient?.IngredientID,
                UnitID = r.SelectedUnit?.UnitID,
                Quantity = r.Quantity,
                Notes = Trimmed(r.Notes),
            }).ToList();

            DraftId = await _drafts.SaveDraftAsync(
                _session.CurrentUserId,
                DraftId,
                SelectedCategory?.CategoryID,
                Trimmed(Title),
                Trimmed(Description),
                Trimmed(Instructions),
                PrepTimeMinutes,
                CookTimeMinutes,
                Servings,
                draftInputs);

            OnPropertyChanged(nameof(IsDraft));
            OnPropertyChanged(nameof(ScreenTitle));
            _savedState=Snapshot();
            NotificationContent = L.Message("Text.DraftSaved");
        }
        catch (AppDbException ex)
        {
            ErrorContent = ex.LocalizedMessage;
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

    [RelayCommand]
    private async Task Save()
    {
        if(IsBusy)return;
        ClearError();

        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorContent = L.Message("Text.ARecipeTitleIsRequired");
            return;
        }

        if (string.IsNullOrWhiteSpace(Instructions))
        {
            ErrorContent = L.Message("Text.InstructionsAreRequired");
            return;
        }

        if(PrepTimeMinutes is <0 || CookTimeMinutes is <0 || Servings is <=0)
        {ErrorContent = L.Message("Text.TimesMustBeZeroOrPositiveAndServingsMustBePositive");return;}
        if (IngredientRows.Any(r => !r.IsComplete))
        {
            ErrorContent = L.Message("Text.CompleteEachIngredientRowIngredientUnitQuantityOrRemoveIt");
            return;
        }

        // The DB allows an ingredient only once per recipe (UQ_RecipeIngredients_Recipe_Ingr).
        // Catch a repeated ingredient here with a clear message instead of letting the save
        // fail with a raw duplicate-key error (rows are all IsComplete, so SelectedIngredient is set).
        var duplicate = IngredientRows
            .GroupBy(r => r.SelectedIngredient!.IngredientID)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            var name = duplicate.First().SelectedIngredient!.Name;
            ErrorContent = L.Message("Ingredient.Duplicate", name);
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

            if (DraftId.HasValue)
                await _drafts.DeleteDraftAsync(DraftId.Value, userId);

            _savedState=Snapshot();
            IsBusy=false;
            await NavigateToDetailAsync(recipeId);
        }
        catch (AppDbException ex) when (ex.ErrorNumber == 50004)
        {
            if (await _dialog.ConfirmAsync(L.Message("Text.EditingConflict"),
                    L.Message("Message.Join", L.Message("Text.TheRecipeChangedInAnotherSessionReloadTheCurrentVersion"), L.Message("Text.YourUnsavedChangesWillBeLost"))))
            {
                await ReloadAsync();
            }
        }
        catch (AppDbException ex)
        {
            ErrorContent = ex.LocalizedMessage;
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

    [RelayCommand]
    private async Task Cancel()
    {
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
        _savedState=Snapshot();
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

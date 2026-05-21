using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;

namespace MealPrepApp.ViewModels.Ingrediente;

/// <summary>
/// Backs the pantry add/edit modal. Same UI for both flows; the proc differs:
/// <c>sp_AddPantryItem</c> (MERGE upsert) on add, <c>sp_UpdatePantryQuantity</c>
/// (absolute set) on edit. Edit fixes the ingredient + unit — only quantity can change.
/// </summary>
public sealed partial class PantryItemDialogViewModel : ViewModelBase
{
    private readonly PantryRepository _pantry;
    private readonly IngredientRepository _ingredients;
    private readonly LookupRepository _lookups;
    private readonly ISessionService _session;

    private int? _editingPantryId;

    public ObservableCollection<Ingredient> Ingredients { get; } = new();
    public ObservableCollection<Unit> Units { get; } = new();

    [ObservableProperty]
    private Ingredient? _selectedIngredient;

    [ObservableProperty]
    private Unit? _selectedUnit;

    [ObservableProperty]
    private decimal? _quantity;

    [ObservableProperty]
    private string _title = "Adauga in frigider";

    [ObservableProperty]
    private bool _isEdit;

    public event EventHandler? SaveSucceeded;

    public PantryItemDialogViewModel(
        PantryRepository pantry,
        IngredientRepository ingredients,
        LookupRepository lookups,
        ISessionService session)
    {
        _pantry = pantry;
        _ingredients = ingredients;
        _lookups = lookups;
        _session = session;
    }

    public async Task InitForAddAsync()
    {
        IsEdit = false;
        Title = "Adauga in frigider";
        _editingPantryId = null;
        SelectedIngredient = null;
        SelectedUnit = null;
        Quantity = null;
        await LoadLookupsAsync();
    }

    public async Task InitForEditAsync(PantryItem item)
    {
        IsEdit = true;
        Title = "Editeaza cantitatea";
        _editingPantryId = item.UserPantryID;
        Quantity = item.Quantity;

        await LoadLookupsAsync();
        SelectedIngredient = Ingredients.FirstOrDefault(i => i.IngredientID == item.IngredientID);
        SelectedUnit = Units.FirstOrDefault(u => u.UnitID == item.UnitID);
    }

    private async Task LoadLookupsAsync()
    {
        ClearError();
        IsBusy = true;
        try
        {
            var ingredients = await _ingredients.GetIngredientsAsync();
            Ingredients.Clear();
            foreach (var i in ingredients.OrderBy(i => i.Name))
                Ingredients.Add(i);

            var units = await _lookups.GetUnitsAsync();
            Units.Clear();
            foreach (var u in units) Units.Add(u);
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
    private async Task Save()
    {
        ClearError();

        if (SelectedIngredient is null)
        {
            ErrorMessage = "Alege un ingredient.";
            return;
        }
        if (SelectedUnit is null)
        {
            ErrorMessage = "Alege o unitate.";
            return;
        }
        if (!Quantity.HasValue || Quantity.Value <= 0m)
        {
            ErrorMessage = "Cantitatea trebuie sa fie mai mare decat 0.";
            return;
        }

        IsBusy = true;
        try
        {
            if (IsEdit && _editingPantryId.HasValue)
            {
                await _pantry.UpdatePantryQuantityAsync(
                    _editingPantryId.Value, _session.CurrentUserId, Quantity.Value);
            }
            else
            {
                await _pantry.AddPantryItemAsync(
                    _session.CurrentUserId,
                    SelectedIngredient.IngredientID,
                    SelectedUnit.UnitID,
                    Quantity.Value);
            }
            SaveSucceeded?.Invoke(this, EventArgs.Empty);
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
}

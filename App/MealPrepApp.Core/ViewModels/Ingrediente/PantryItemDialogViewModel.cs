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
    private readonly InventoryRepository? _inventory;
    private readonly PantryRepository _pantry;
    [ObservableProperty] private DateTime? _purchasedOn;
    [ObservableProperty] private DateTime? _expiresOn;
    private Guid _operation=Guid.NewGuid();
    private string? _submitted;
    partial void OnSelectedIngredientChanged(Ingredient? value) { if(!IsEdit) SelectedUnit=Units.FirstOrDefault(u=>u.UnitID==value?.DefaultUnitID); }
    [RelayCommand] private void AddOne() => Quantity=(Quantity??0)+1;
    [RelayCommand] private void AddHundred() => Quantity=(Quantity??0)+100;
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

    [LocalizedProperty]
    public string Title { get => L.Text(IsEdit ? "Text.EditQuantity" : "Text.AddToPantry337"); private set => OnPropertyChanged(); }

    [ObservableProperty]
    private bool _isEdit;

    public event EventHandler? SaveSucceeded;

    public PantryItemDialogViewModel(
        PantryRepository pantry,
        IngredientRepository ingredients,
        LookupRepository lookups,
        ISessionService session, InventoryRepository? inventory = null)
    {
        _pantry = pantry;
        _ingredients = ingredients;
        _lookups = lookups;
        _session = session; _inventory = inventory;
    }

    public async Task InitForAddAsync()
    {
        IsEdit = false;
        Title = L.Text("Text.AddToPantry337");
        _editingPantryId = null;
        SelectedIngredient = null;
        SelectedUnit = null;
        Quantity = null;
        await LoadLookupsAsync();
    }

    public async Task InitForEditAsync(PantryItem item)
    {
        IsEdit = true;
        Title = L.Text("Text.EditQuantity");
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
        ClearError();

        if (SelectedIngredient is null)
        {
            ErrorContent = L.Message("Text.ChooseAnIngredient");
            return;
        }
        if (SelectedUnit is null)
        {
            ErrorContent = L.Message("Text.ChooseAUnit");
            return;
        }
        if (!Quantity.HasValue || Quantity.Value <= 0m)
        {
            ErrorContent = L.Message("Text.TheQuantityMustBeGreaterThan0");
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
            else if (_inventory is not null)
            {
                var fingerprint=$"{SelectedIngredient.IngredientID}|{SelectedUnit.UnitID}|{Quantity.Value}|{PurchasedOn:yyyyMMdd}|{ExpiresOn:yyyyMMdd}";
                if(_submitted!=fingerprint) { _submitted=fingerprint; _operation=Guid.NewGuid(); }
                await _inventory.AddAsync(_session.CurrentUserId,_operation,SelectedIngredient.IngredientID,SelectedUnit.UnitID,Quantity.Value,PurchasedOn,ExpiresOn);
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
}

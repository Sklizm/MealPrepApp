using MealPrepApp.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;

namespace MealPrepApp.ViewModels.Ingrediente;

/// <summary>Backs the small "Add ingredient" modal: name + default unit + category.</summary>
public sealed partial class IngredientAddDialogViewModel : ViewModelBase
{
    private readonly IngredientRepository _ingredients;
    private readonly LookupRepository _lookups;
    private readonly ISessionService? _session;
    private Ingredient? _editing;
    public bool IsEdit => _editing is not null;
    [LocalizedProperty] public string EditorTitle => L.Text(IsEdit ? "UX.EditIngredient" : "UX.CreateIngredient");
    public async Task InitForEditAsync(Ingredient ingredient) {
        _editing = ingredient; await LoadAsync(); Name = ingredient.Name;
        SelectedUnit = Units.FirstOrDefault(u=>u.UnitID==ingredient.DefaultUnitID);
        SelectedCategory = Categories.FirstOrDefault(c=>c.IngredientCategoryID==ingredient.IngredientCategoryID);
        OnPropertyChanged(nameof(IsEdit)); OnPropertyChanged(nameof(EditorTitle));
    }

    public ObservableCollection<Unit> Units { get; } = new();
    public ObservableCollection<IngredientCategory> Categories { get; } = new();

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private Unit? _selectedUnit;

    [ObservableProperty]
    private IngredientCategory? _selectedCategory;

    /// <summary>Raised when the proc returns successfully and the dialog should close.</summary>
    public int? CreatedIngredientID { get; private set; }
    public event EventHandler? SaveSucceeded;

    public IngredientAddDialogViewModel(IngredientRepository ingredients, LookupRepository lookups, ISessionService? session = null)
    {
        _ingredients = ingredients;
        _lookups = lookups; _session = session;
    }

    public async Task LoadAsync()
    {
        ClearError();
        IsBusy = true;
        try
        {
            var units = await _lookups.GetUnitsAsync();
            Units.Clear();
            foreach (var u in units) Units.Add(u);

            var categories = await _lookups.GetIngredientCategoriesAsync();
            Categories.Clear();
            foreach (var c in categories) Categories.Add(c);
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

        var name = (Name ?? "").Trim();
        if (name.Length == 0)
        {
            ErrorContent = L.Message("Text.ANameIsRequired");
            return;
        }

        IsBusy = true;
        try
        {
            if (_editing is not null && _session is not null) {
                await _ingredients.UpdateAsync(_session.CurrentUserId,_editing,name,SelectedUnit?.UnitID,SelectedCategory?.IngredientCategoryID);
                CreatedIngredientID = _editing.IngredientID;
            } else CreatedIngredientID = await _ingredients.AddIngredientAsync(name,SelectedUnit?.UnitID,SelectedCategory?.IngredientCategoryID);
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

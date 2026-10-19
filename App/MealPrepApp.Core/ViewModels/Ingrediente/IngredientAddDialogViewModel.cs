using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;

namespace MealPrepApp.ViewModels.Ingrediente;

/// <summary>Backs the small "Adauga ingredient" modal: name + default unit + category.</summary>
public sealed partial class IngredientAddDialogViewModel : ViewModelBase
{
    private readonly IngredientRepository _ingredients;
    private readonly LookupRepository _lookups;

    public ObservableCollection<Unit> Units { get; } = new();
    public ObservableCollection<IngredientCategory> Categories { get; } = new();

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private Unit? _selectedUnit;

    [ObservableProperty]
    private IngredientCategory? _selectedCategory;

    /// <summary>Raised when the proc returns successfully and the dialog should close.</summary>
    public event EventHandler? SaveSucceeded;

    public IngredientAddDialogViewModel(IngredientRepository ingredients, LookupRepository lookups)
    {
        _ingredients = ingredients;
        _lookups = lookups;
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

        var name = (Name ?? "").Trim();
        if (name.Length == 0)
        {
            ErrorMessage = "Numele este obligatoriu.";
            return;
        }

        IsBusy = true;
        try
        {
            await _ingredients.AddIngredientAsync(
                name,
                SelectedUnit?.UnitID,
                SelectedCategory?.IngredientCategoryID);
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

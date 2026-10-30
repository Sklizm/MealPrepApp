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
            await _ingredients.AddIngredientAsync(
                name,
                SelectedUnit?.UnitID,
                SelectedCategory?.IngredientCategoryID);
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

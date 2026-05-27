using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;

namespace MealPrepApp.ViewModels.Ingrediente;

/// <summary>Edits optional nutrition values for one ingredient.</summary>
public sealed partial class IngredientNutritionDialogViewModel : ViewModelBase
{
    private readonly NutritionRepository _nutrition;
    private readonly LookupRepository _lookups;

    public ObservableCollection<Unit> Units { get; } = new();

    public int IngredientID { get; private set; }

    [ObservableProperty]
    private string _ingredientName = "";

    [ObservableProperty]
    private decimal _basisQuantity = 100;

    [ObservableProperty]
    private Unit? _selectedUnit;

    [ObservableProperty]
    private decimal _calories;

    [ObservableProperty]
    private decimal _proteinGrams;

    [ObservableProperty]
    private decimal _carbsGrams;

    [ObservableProperty]
    private decimal _fatGrams;

    public event EventHandler? SaveSucceeded;

    public IngredientNutritionDialogViewModel(NutritionRepository nutrition, LookupRepository lookups)
    {
        _nutrition = nutrition;
        _lookups = lookups;
    }

    public async Task LoadAsync(Ingredient ingredient)
    {
        ClearError();
        IngredientID = ingredient.IngredientID;
        IngredientName = ingredient.Name;
        IsBusy = true;
        try
        {
            var units = await _lookups.GetUnitsAsync();
            Units.Clear();
            foreach (var unit in units)
                Units.Add(unit);

            var existing = await _nutrition.GetIngredientNutritionAsync(IngredientID);
            if (existing is null)
            {
                BasisQuantity = 100;
                SelectedUnit = Units.FirstOrDefault(u => u.Abbreviation == ingredient.DefaultUnitAbbreviation)
                    ?? Units.FirstOrDefault(u => u.Abbreviation == "g")
                    ?? Units.FirstOrDefault();
                Calories = 0;
                ProteinGrams = 0;
                CarbsGrams = 0;
                FatGrams = 0;
            }
            else
            {
                BasisQuantity = existing.BasisQuantity;
                SelectedUnit = Units.FirstOrDefault(u => u.UnitID == existing.BasisUnitID);
                Calories = existing.Calories;
                ProteinGrams = existing.ProteinGrams;
                CarbsGrams = existing.CarbsGrams;
                FatGrams = existing.FatGrams;
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
    public async Task SaveAsync()
    {
        ClearError();
        if (BasisQuantity <= 0)
        {
            ErrorMessage = "Cantitatea de baza trebuie sa fie mai mare decat 0.";
            return;
        }
        if (SelectedUnit is null)
        {
            ErrorMessage = "Alege unitatea de baza.";
            return;
        }
        if (Calories < 0 || ProteinGrams < 0 || CarbsGrams < 0 || FatGrams < 0)
        {
            ErrorMessage = "Valorile nutritionale nu pot fi negative.";
            return;
        }

        IsBusy = true;
        try
        {
            await _nutrition.SetIngredientNutritionAsync(
                IngredientID,
                BasisQuantity,
                SelectedUnit.UnitID,
                Calories,
                ProteinGrams,
                CarbsGrams,
                FatGrams);
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

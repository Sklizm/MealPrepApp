using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Models;

namespace MealPrepApp.ViewModels.Retete;

/// <summary>One editable ingredient line in the recipe editor: ingredient + unit + quantity + notes.
/// The ingredient and unit pickers share the editor's single fetched lists.</summary>
public sealed partial class RecipeIngredientRowViewModel : ObservableObject
{
    /// <summary>Every ingredient — the row's ingredient picker (editable ComboBox) binds to this.</summary>
    public IReadOnlyList<Ingredient> AllIngredients { get; }

    /// <summary>Every unit — the row's unit picker binds to this.</summary>
    public IReadOnlyList<Unit> AllUnits { get; }

    [ObservableProperty]
    private Ingredient? _selectedIngredient;

    [ObservableProperty]
    private Unit? _selectedUnit;

    [ObservableProperty]
    private decimal _quantity = 1m;

    [ObservableProperty]
    private string? _notes;

    /// <summary>Raised when the user clicks the row's remove button; the editor drops the row.</summary>
    public event EventHandler? RemoveRequested;

    public RecipeIngredientRowViewModel(IReadOnlyList<Ingredient> allIngredients, IReadOnlyList<Unit> allUnits)
    {
        AllIngredients = allIngredients;
        AllUnits = allUnits;
    }

    /// <summary>Picking an ingredient pre-selects its default unit, if the user has not chosen one yet.</summary>
    partial void OnSelectedIngredientChanged(Ingredient? value)
    {
        if (value?.DefaultUnitID is int unitId && SelectedUnit is null)
            SelectedUnit = AllUnits.FirstOrDefault(u => u.UnitID == unitId);
    }

    [RelayCommand]
    private void Remove() => RemoveRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>A row counts only once ingredient, unit and a positive quantity are all set.</summary>
    public bool IsComplete =>
        SelectedIngredient is not null && SelectedUnit is not null && Quantity > 0m;

    /// <summary>Projects the row to the input shape <c>sp_CreateRecipe</c> / <c>sp_UpdateRecipe</c> expect.</summary>
    public RecipeIngredientInput ToInput() => new()
    {
        IngredientID = SelectedIngredient!.IngredientID,
        UnitID = SelectedUnit!.UnitID,
        Quantity = Quantity,
        Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
    };
}

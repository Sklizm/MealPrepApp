using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;

namespace MealPrepApp.ViewModels.Ingrediente;

public sealed partial class ShoppingItemDialogViewModel(ShoppingListRepository shopping,
    IngredientRepository ingredients, LookupRepository lookups, ISessionService session,
    int snapshotId, ShoppingListRow? original) : ViewModelBase
{
    public IReadOnlyList<Ingredient> Ingredients { get; private set; } = [];
    public IReadOnlyList<Unit> Units { get; private set; } = [];
    public bool CanEditPurchase => original?.IsImported != true;
    public bool IsNew => original is null;
    [ObservableProperty] private string _name = original?.IngredientName ?? "";
    [ObservableProperty] private Ingredient? _selectedIngredient;
    [ObservableProperty] private Unit? _selectedUnit;
    [ObservableProperty] private decimal _requiredQuantity = original?.ToBuyQty ?? 1;
    [ObservableProperty] private decimal? _purchasedQuantity = original?.PurchasedQuantity ?? original?.ToBuyQty;
    [ObservableProperty] private bool _isPurchased = original?.IsPurchased ?? false;
    [ObservableProperty] private DateTime? _purchasedOn = original?.PurchasedOn;
    [ObservableProperty] private DateTime? _expiresOn = original?.ExpiresOn;
    public event EventHandler? SaveSucceeded;
    private Guid _operation = Guid.NewGuid();
    private ShoppingChange? _submitted;
    partial void OnSelectedIngredientChanged(Ingredient? value) {
        if (value is null) return;
        Name = value.Name;
        SelectedUnit = Units.FirstOrDefault(u => u.UnitID == value.DefaultUnitID);
    }
    public async Task LoadAsync() {
        Ingredients = await ingredients.GetIngredientsAsync(); Units = await lookups.GetUnitsAsync();
        SelectedIngredient = Ingredients.FirstOrDefault(i => i.IngredientID == original?.IngredientID);
        SelectedUnit = Units.FirstOrDefault(u => u.UnitID == (original?.PurchasedUnitID ?? original?.UnitID));
        if (original is not null) Name = original.IngredientName;
        OnPropertyChanged(nameof(Ingredients)); OnPropertyChanged(nameof(Units));
    }
    [RelayCommand] private async Task Save() {
        ClearError();
        if (string.IsNullOrWhiteSpace(Name) || RequiredQuantity <= 0 || PurchasedQuantity is <= 0 || (PurchasedOn.HasValue && ExpiresOn < PurchasedOn)) {
            ErrorContent = L.Message("UX.InvalidPurchase"); return;
        }
        var change = new ShoppingChange(snapshotId, original?.ItemID, original?.Version ?? 0, Name.Trim(),
            SelectedIngredient?.IngredientID, original?.UnitID ?? SelectedUnit?.UnitID, RequiredQuantity,
            IsPurchased, PurchasedQuantity, SelectedUnit?.UnitID, PurchasedOn?.Date, ExpiresOn?.Date);
        if (original?.IsImported == true) change = change with { IngredientID = original.IngredientID, UnitID = original.UnitID,
            PurchasedQuantity = original.PurchasedQuantity, PurchasedUnitID = original.PurchasedUnitID, PurchasedOn = original.PurchasedOn, ExpiresOn = original.ExpiresOn };
        if (_submitted != change) { _submitted = change; _operation = Guid.NewGuid(); }
        IsBusy = true;
        try { await shopping.ChangeItemAsync(session.CurrentUserId, _operation, change); SaveSucceeded?.Invoke(this, EventArgs.Empty); }
        catch (Exception ex) { AppDiagnostics.Record(ex); ErrorContent = ex is AppDbException db ? db.LocalizedMessage : L.Message("UX.CheckRetry"); }
        finally { IsBusy = false; }
    }
}

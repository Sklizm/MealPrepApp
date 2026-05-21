using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
using MealPrepApp.Views.Ingrediente;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.ViewModels.Ingrediente;

/// <summary>
/// The Frigider (pantry) sub-view: lists what the current user has in stock and lets them
/// add / edit / remove rows. Backed by sp_GetPantry + sp_AddPantryItem +
/// sp_UpdatePantryQuantity + sp_RemovePantryItem.
/// </summary>
public sealed partial class FrigiderViewModel : ViewModelBase, IAsyncLoadable
{
    private readonly PantryRepository _pantry;
    private readonly IngredientRepository _ingredients;
    private readonly LookupRepository _lookups;
    private readonly ISessionService _session;
    private readonly IDialogService _dialog;
    private readonly IServiceProvider _services;

    public ObservableCollection<PantryItem> Items { get; } = new();

    public FrigiderViewModel(
        PantryRepository pantry,
        IngredientRepository ingredients,
        LookupRepository lookups,
        ISessionService session,
        IDialogService dialog,
        IServiceProvider services)
    {
        _pantry = pantry;
        _ingredients = ingredients;
        _lookups = lookups;
        _session = session;
        _dialog = dialog;
        _services = services;
    }

    public Task LoadAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        ClearError();
        IsBusy = true;
        try
        {
            var rows = await _pantry.GetPantryAsync(_session.CurrentUserId);
            Items.Clear();
            foreach (var row in rows.OrderBy(r => r.IngredientName))
                Items.Add(row);
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
    private async Task AddItem()
    {
        var vm = _services.GetRequiredService<PantryItemDialogViewModel>();
        await vm.InitForAddAsync();
        if (_dialog.ShowDialog<PantryItemDialog>(vm))
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task EditItem(PantryItem? item)
    {
        if (item is null) return;
        var vm = _services.GetRequiredService<PantryItemDialogViewModel>();
        await vm.InitForEditAsync(item);
        if (_dialog.ShowDialog<PantryItemDialog>(vm))
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task RemoveItem(PantryItem? item)
    {
        if (item is null) return;
        if (!_dialog.Confirm("Sterge din frigider", $"Sterge \"{item.IngredientName}\" din frigider?"))
            return;

        IsBusy = true;
        try
        {
            await _pantry.RemovePantryItemAsync(item.UserPantryID, _session.CurrentUserId);
            Items.Remove(item);
        }
        catch (AppDbException ex)
        {
            _dialog.ShowError(ex.FriendlyMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

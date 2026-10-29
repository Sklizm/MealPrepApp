using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
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

    private bool _populating;
    [ObservableProperty] private bool _useGrouping;
    public IEnumerable<PantryGroup> Groups=>VisibleItems.GroupBy(i=>i.CategoryName).Select(g=>new PantryGroup(g.Key,g));
    [ObservableProperty] private string _searchTerm="";
    [ObservableProperty] private string _sortOrder="Ingredient";
    public IReadOnlyList<string> SortOrders {get;}=new[]{"Ingredient","Cantitate","Adăugat recent"};
    public IEnumerable<PantryItem> VisibleItems => (SortOrder switch {"Cantitate"=>Items.OrderBy(i=>i.Quantity),"Adăugat recent"=>Items.OrderByDescending(i=>i.AddedAt),_=>Items.OrderBy(i=>i.IngredientName)}).Where(i=>i.IngredientName.Contains(SearchTerm,StringComparison.CurrentCultureIgnoreCase));
    partial void OnSearchTermChanged(string value){OnPropertyChanged(nameof(VisibleItems));OnPropertyChanged(nameof(Groups));}
    partial void OnSortOrderChanged(string value){OnPropertyChanged(nameof(VisibleItems));OnPropertyChanged(nameof(Groups));}
    [RelayCommand] private Task Retry()=>RefreshAsync();
    [RelayCommand] private async Task SaveQuantity(PantryItem? item)
    {
        if(item is null || IsBusy)return;
        if(item.Quantity<=0){ErrorMessage="Cantitatea trebuie să fie pozitivă.";return;}
        IsBusy=true;ClearError();
        try {await _pantry.UpdatePantryQuantityAsync(item.UserPantryID,_session.CurrentUserId,item.Quantity);Notification="Cantitatea a fost actualizată.";}
        catch(AppDbException ex){ErrorMessage=ex.FriendlyMessage;}
        catch(Exception ex){AppDiagnostics.Record(ex);ErrorMessage="Cantitatea nu a putut fi actualizată.";}
        finally{IsBusy=false;}
    }
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
        Items.CollectionChanged+=(_,_)=>{if (!_populating) {OnPropertyChanged(nameof(VisibleItems));OnPropertyChanged(nameof(Groups));}};
    }

    public Task LoadAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        ClearError();
        IsBusy = true;
        try
        {
            var rows = await _pantry.GetPantryAsync(_session.CurrentUserId);
            _populating = true;
            try { Items.Clear(); foreach (var row in rows.OrderBy(r => r.IngredientName)) Items.Add(row); }
            finally { _populating = false; }
            OnPropertyChanged(nameof(VisibleItems)); OnPropertyChanged(nameof(Groups));
        }
        catch (AppDbException ex)
        {
            ErrorMessage = ex.FriendlyMessage;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppDiagnostics.Record(ex);
            ErrorMessage = "Operatia nu a putut fi finalizata. Consulta jurnalul aplicatiei.";
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
        if (await _dialog.ShowDialogAsync(vm))
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task EditItem(PantryItem? item)
    {
        if (item is null) return;
        var vm = _services.GetRequiredService<PantryItemDialogViewModel>();
        await vm.InitForEditAsync(item);
        if (await _dialog.ShowDialogAsync(vm))
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task RemoveItem(PantryItem? item)
    {
        if (item is null) return;
        if (!await _dialog.ConfirmAsync("Sterge din frigider", $"Sterge \"{item.IngredientName}\" din frigider?"))
            return;

        IsBusy = true;
        try
        {
            await _pantry.RemovePantryItemAsync(item.UserPantryID, _session.CurrentUserId);
            Items.Remove(item);
        }
        catch (AppDbException ex)
        {
            await _dialog.ShowErrorAsync(ex.FriendlyMessage);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppDiagnostics.Record(ex);
            ErrorMessage = "Operatia nu a putut fi finalizata. Consulta jurnalul aplicatiei.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed class PantryGroup(string name,IEnumerable<PantryItem> items):ObservableCollection<PantryItem>(items){public string Name {get;}=name;}

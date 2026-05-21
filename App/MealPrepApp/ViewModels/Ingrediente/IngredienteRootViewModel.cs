using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.ViewModels.Ingrediente;

/// <summary>
/// Container for the Ingrediente tab. Sidebar entries — Toate / Categorii / Frigider /
/// Lista de cumparaturi — swap <see cref="CurrentSubView"/> between three sub-VMs. The
/// ingredient list VM serves both Toate (flat) and Categorii (grouped) via its
/// <c>UseGrouping</c> flag.
/// </summary>
public sealed partial class IngredienteRootViewModel : ViewModelBase, IAsyncLoadable
{
    private readonly IServiceProvider _services;

    private IngredienteListViewModel? _list;
    private FrigiderViewModel? _frigider;
    private ShoppingListViewModel? _shopping;

    [ObservableProperty]
    private object? _currentSubView;

    [ObservableProperty]
    private string _activeSection = "Toate";

    public IngredienteRootViewModel(IServiceProvider services)
    {
        _services = services;
    }

    public Task LoadAsync() => ShowToateCommand.ExecuteAsync(null);

    private IngredienteListViewModel ListVm
    {
        get
        {
            if (_list is null)
                _list = _services.GetRequiredService<IngredienteListViewModel>();
            return _list;
        }
    }

    [RelayCommand]
    private async Task ShowToate()
    {
        ActiveSection = "Toate";
        var vm = ListVm;
        vm.UseGrouping = false;
        CurrentSubView = vm;
        await vm.LoadAsync();
    }

    [RelayCommand]
    private async Task ShowCategorii()
    {
        ActiveSection = "Categorii";
        var vm = ListVm;
        vm.UseGrouping = true;
        CurrentSubView = vm;
        await vm.LoadAsync();
    }

    [RelayCommand]
    private async Task ShowFrigider()
    {
        ActiveSection = "Frigider";
        _frigider ??= _services.GetRequiredService<FrigiderViewModel>();
        CurrentSubView = _frigider;
        await _frigider.LoadAsync();
    }

    [RelayCommand]
    private async Task ShowCumparaturi()
    {
        ActiveSection = "Cumparaturi";
        _shopping ??= _services.GetRequiredService<ShoppingListViewModel>();
        CurrentSubView = _shopping;
        await _shopping.LoadAsync();
    }
}

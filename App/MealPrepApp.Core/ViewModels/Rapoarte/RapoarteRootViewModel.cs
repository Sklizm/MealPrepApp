using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.ViewModels.Rapoarte;

/// <summary>
/// Container for the Rapoarte tab. A top toggle swaps <see cref="CurrentSubView"/> between the
/// three report views: Statistici lunare / Plan saptamanal pentru tiparire / Lista cumparaturi
/// pentru tiparire. Child VMs are resolved lazily on first use, mirroring
/// <c>PlanificareRootViewModel</c> / <c>IngredienteRootViewModel</c>.
/// </summary>
public sealed partial class RapoarteRootViewModel : ViewModelBase, IAsyncLoadable
{
    private readonly IServiceProvider _services;

    private StatisticiLunareViewModel? _statistici;
    private PlanSaptamanalPrintViewModel? _planPrint;
    private ListaCumparaturiPrintViewModel? _listaPrint;

    [ObservableProperty]
    private object? _currentSubView;

    [ObservableProperty]
    private string _activeView = "Statistici";

    public RapoarteRootViewModel(IServiceProvider services)
    {
        _services = services;
    }

    public Task LoadAsync() => ShowStatisticiCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task ShowStatistici()
    {
        ActiveView = "Statistici";
        _statistici ??= _services.GetRequiredService<StatisticiLunareViewModel>();
        CurrentSubView = _statistici;
        await _statistici.LoadAsync();
    }

    [RelayCommand]
    private async Task ShowPlanPrint()
    {
        ActiveView = "PlanPrint";
        _planPrint ??= _services.GetRequiredService<PlanSaptamanalPrintViewModel>();
        CurrentSubView = _planPrint;
        await _planPrint.LoadAsync();
    }

    [RelayCommand]
    private async Task ShowListaPrint()
    {
        ActiveView = "ListaPrint";
        _listaPrint ??= _services.GetRequiredService<ListaCumparaturiPrintViewModel>();
        CurrentSubView = _listaPrint;
        await _listaPrint.LoadAsync();
    }
}

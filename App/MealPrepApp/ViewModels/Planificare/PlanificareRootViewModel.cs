using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.ViewModels.Planificare;

/// <summary>
/// Container for the Planificare tab. A top toggle swaps <see cref="CurrentSubView"/> between
/// the monthly overview ("Lunar") and the weekly grid ("Saptamanal"). Child VMs are resolved
/// lazily on first use, mirroring <c>IngredienteRootViewModel</c>.
/// </summary>
public sealed partial class PlanificareRootViewModel : ViewModelBase, IAsyncLoadable
{
    private readonly IServiceProvider _services;

    private PlanificareMonthlyViewModel? _monthly;
    private PlanificareWeeklyViewModel? _weekly;

    [ObservableProperty]
    private object? _currentSubView;

    [ObservableProperty]
    private string _activeView = "Lunar";

    public PlanificareRootViewModel(IServiceProvider services)
    {
        _services = services;
    }

    public Task LoadAsync() => ShowLunarCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task ShowLunar()
    {
        ActiveView = "Lunar";
        _monthly ??= _services.GetRequiredService<PlanificareMonthlyViewModel>();
        CurrentSubView = _monthly;
        await _monthly.LoadAsync();
    }

    [RelayCommand]
    private async Task ShowSaptamanal()
    {
        ActiveView = "Saptamanal";
        _weekly ??= _services.GetRequiredService<PlanificareWeeklyViewModel>();
        CurrentSubView = _weekly;
        await _weekly.LoadAsync();
    }
}

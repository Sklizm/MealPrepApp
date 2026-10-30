using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Models;
namespace MealPrepApp.Services;
public sealed partial class CookingIngredient : ObservableObject
{
    public string Name { get; init; } = "";
    public decimal BaseQuantity { get; init; }
    public string Unit { get; init; } = "";
    public string? Notes { get; init; }
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private bool _isChecked;
}
// Scoped to login session. Uses a monotonic clock; the shell owns UI ticking.
public sealed partial class CookingSession : LocalizedObservableObject
{
    private readonly TimeProvider _clock;
    private long _started;
    private TimeSpan _duration;
    public CookingSession(TimeProvider? clock = null) { _clock=clock ?? TimeProvider.System; }
    [ObservableProperty] private RecipeFull? _recipe;
    [ObservableProperty] private decimal _servings=1;
    [ObservableProperty] private decimal _timerMinutes=10;
    [ObservableProperty] private bool _isTimerRunning;
    [ObservableProperty] private bool _timerFinished;
    private LocalizedMessage _timerMessage = L.Message("Text.TimerStopped");
    [LocalizedProperty]
    public string TimerText { get => _timerMessage.ToString(); private set { OnPropertyChanged(); } }
    private void SetTimerMessage(LocalizedMessage message) { _timerMessage = message; OnPropertyChanged(nameof(TimerText)); }
    public bool HasTimer=>IsTimerRunning || TimerFinished;
    partial void OnIsTimerRunningChanged(bool value)=>OnPropertyChanged(nameof(HasTimer));
    partial void OnTimerFinishedChanged(bool value)=>OnPropertyChanged(nameof(HasTimer));
    public ObservableCollection<CookingIngredient> Ingredients { get; } = new();
    public string[] Paragraphs => (Recipe?.Instructions ?? "").Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
    public bool CanScale => Recipe?.Servings is > 0;
    public void StartRecipe(RecipeFull recipe, decimal servings)
    {
        if (Recipe?.RecipeID == recipe.RecipeID) { Servings=servings; return; }
        Recipe=recipe; Ingredients.Clear();
        foreach (var line in recipe.Ingredients) Ingredients.Add(new CookingIngredient { Name=line.IngredientName, BaseQuantity=line.Quantity, Quantity=line.Quantity, Unit=line.UnitAbbreviation, Notes=line.Notes });
        Servings=CanScale ? servings : 1; UpdateQuantities();
        OnPropertyChanged(nameof(Paragraphs)); OnPropertyChanged(nameof(CanScale));
    }
    partial void OnServingsChanged(decimal value) { if (value is <=0 or >1000) { Servings=1; return; } UpdateQuantities(); }
    private void UpdateQuantities() { var factor=CanScale ? ServingCalculator.Factor(Recipe!.Servings, Servings) : 1; foreach(var item in Ingredients) item.Quantity=item.BaseQuantity*factor; }
    [RelayCommand] private void StartTimer()
    {
        if (TimerMinutes is <=0 or >1440) return;
        _duration=TimeSpan.FromMinutes((double)TimerMinutes); _started=_clock.GetTimestamp(); TimerFinished=false; IsTimerRunning=true; Tick();
    }
    [RelayCommand] private void StopTimer() { IsTimerRunning=false; TimerFinished=false; SetTimerMessage(L.Message("Text.TimerStopped")); }
    public void Tick()
    {
        if (!IsTimerRunning) return;
        var left=_duration-_clock.GetElapsedTime(_started);
        if (left<=TimeSpan.Zero) { IsTimerRunning=false; TimerFinished=true; SetTimerMessage(L.Message("Text.TimeIsUp")); }
        else SetTimerMessage(L.Message("Timer.Remaining", (int)left.TotalMinutes, left.Seconds));
    }
}

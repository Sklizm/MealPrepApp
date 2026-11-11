using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
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
public sealed record CookingRecovery(RecipeFull? Recipe, decimal Servings, int Step, bool[] Checks,
    CookingTimerState[] Timers, int? CookingBatchID, Guid PlanOperation, Guid FinishOperation, string? FinishFingerprint = null);
public sealed partial class CookingSession : LocalizedObservableObject
{
    private readonly TimeProvider _clock;
    private readonly LocalJsonStore? _store;
    private bool _restoring;
    public CookingSession(TimeProvider? clock = null, string? recoveryPath = null) {
        _clock = clock ?? TimeProvider.System;
        if (recoveryPath is not null) { _store = new(recoveryPath); Restore(); }
    }
    [ObservableProperty] private RecipeFull? _recipe;
    [ObservableProperty] private decimal _servings = 1;
    [ObservableProperty] private decimal _timerMinutes = 10;
    [ObservableProperty] private string _timerName = "";
    [ObservableProperty] private bool _isTimerRunning;
    [ObservableProperty] private bool _timerFinished;
    [ObservableProperty] private int _currentStep;
    [ObservableProperty] private bool _saveFailed;
    public bool HasRecovered { get; private set; }
    public int? CookingBatchID { get; set; }
    public Guid PlanOperation { get; private set; } = Guid.NewGuid();
    private string? _finishFingerprint;
    public Guid FinishOperation { get; private set; } = Guid.NewGuid();
    public event EventHandler<string>? AlarmCompleted;
    public ObservableCollection<CookingIngredient> Ingredients { get; } = new();
    public ObservableCollection<CookingTimer> Timers { get; } = new();
    public string[] Paragraphs => (Recipe?.Instructions ?? "").Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
    public string[] Steps => Regex.Split((Recipe?.Instructions ?? "").Replace("\r\n", "\n").Trim(), @"\n\s*\n|\n(?=\s*\d+[.)]\s)")
        .Select(s => Regex.Replace(s.Trim(), @"^\d+[.)]\s*", "")).Where(s => s.Length > 0).ToArray();
    public string CurrentInstruction => Steps.ElementAtOrDefault(CurrentStep) ?? "";
    [LocalizedProperty] public string StepLabel => L.Text("UX.CookingStep", Steps.Length == 0 ? 0 : CurrentStep + 1, Steps.Length);
    [LocalizedProperty] public string TimerText => Timers.LastOrDefault()?.Text ?? L.Text("Text.TimerStopped");
    public bool HasTimer => Timers.Any(t => t.IsRunning || t.IsCompleted || t.Remaining > TimeSpan.Zero);
    public bool CanScale => Recipe?.Servings is > 0;
    public bool HasPrevious => CurrentStep > 0;
    public bool HasNext => CurrentStep < Steps.Length - 1;
    partial void OnCurrentStepChanged(int value) { OnPropertyChanged(nameof(CurrentInstruction)); OnPropertyChanged(nameof(StepLabel)); OnPropertyChanged(nameof(HasPrevious)); OnPropertyChanged(nameof(HasNext)); Save(); }
    [RelayCommand] private void PreviousStep() { if (HasPrevious) CurrentStep--; }
    [RelayCommand] private void NextStep() { if (HasNext) CurrentStep++; }
    public void StartRecipe(RecipeFull recipe, decimal servings) {
        if (Recipe?.RecipeID == recipe.RecipeID) { Servings = servings; return; }
        var wasRestoring = _restoring; _restoring = true;
        _finishFingerprint = null; Recipe = recipe; Ingredients.Clear(); CurrentStep = 0; CookingBatchID = null; PlanOperation = Guid.NewGuid(); FinishOperation = Guid.NewGuid();
        foreach (var line in recipe.Ingredients) {
            var ingredient = new CookingIngredient { Name = line.IngredientName, BaseQuantity = line.Quantity, Quantity = line.Quantity, Unit = line.UnitAbbreviation, Notes = line.Notes };
            ingredient.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(CookingIngredient.IsChecked)) Save(); };
            Ingredients.Add(ingredient);
        }
        Servings = CanScale ? servings : 1; UpdateQuantities(); _restoring = wasRestoring;
        foreach (var property in new[] { nameof(Paragraphs), nameof(Steps), nameof(CurrentInstruction), nameof(StepLabel), nameof(CanScale), nameof(HasPrevious), nameof(HasNext) }) OnPropertyChanged(property);
        Save();
    }
    partial void OnServingsChanged(decimal value) { if (value is <= 0 or > 1000) { Servings = 1; return; } UpdateQuantities(); Save(); }
    private void UpdateQuantities() { var factor = CanScale ? ServingCalculator.Factor(Recipe!.Servings, Servings) : 1; foreach (var item in Ingredients) item.Quantity = item.BaseQuantity * factor; }
    [RelayCommand] private void StartTimer() {
        if (TimerMinutes is <= 0 or > 1440 || Timers.Count >= 20) return;
        var timer = new CookingTimer(string.IsNullOrWhiteSpace(TimerName) ? L.Text("UX.Timer") : TimerName.Trim()[..Math.Min(80, TimerName.Trim().Length)], TimeSpan.FromMinutes((double)TimerMinutes), _clock);
        Attach(timer); Timers.Add(timer); UpdateTimerSummary(); Save();
    }
    private void Attach(CookingTimer timer) {
        timer.Changed += (_, _) => { UpdateTimerSummary(); Save(); };
        timer.Completed += (_, _) => AlarmCompleted?.Invoke(this, timer.Name);
    }
    [RelayCommand] private void StopTimer() { foreach (var timer in Timers) timer.StopCommand.Execute(null); Timers.Clear(); UpdateTimerSummary(); Save(); }
    [RelayCommand] private void RemoveTimer(CookingTimer? timer) { if (timer is null) return; Timers.Remove(timer); UpdateTimerSummary(); Save(); }
    private void UpdateTimerSummary() {
        IsTimerRunning = Timers.Any(t => t.IsRunning); TimerFinished = Timers.Any(t => t.IsCompleted);
        OnPropertyChanged(nameof(TimerText)); OnPropertyChanged(nameof(HasTimer));
    }
    public void Tick() { foreach (var timer in Timers.ToArray()) timer.Tick(); UpdateTimerSummary(); }
    public Guid OperationFor(CookingCompletion completion) {
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(completion))));
        if (_finishFingerprint is not null && _finishFingerprint != fingerprint) FinishOperation = Guid.NewGuid();
        _finishFingerprint = fingerprint; Save(); return FinishOperation;
    }
    public void CompleteRecipe() { Recipe = null; Ingredients.Clear(); CookingBatchID = null; CurrentStep = 0; HasRecovered = false; Save(); }
    public void Save() {
        if (_store is null || _restoring) return;
        SaveFailed = !_store.Write(new CookingRecovery(Recipe, Servings, CurrentStep, Ingredients.Select(i => i.IsChecked).ToArray(), Timers.Select(t => t.State).ToArray(), CookingBatchID, PlanOperation, FinishOperation, _finishFingerprint));
    }
    private void Restore() {
        var saved = _store?.Read<CookingRecovery>();
        if (saved is null) return;
        _restoring = true;
        if (saved.Recipe is not null) StartRecipe(saved.Recipe, saved.Servings);
        _restoring = true;
        CurrentStep = Math.Clamp(saved.Step, 0, Math.Max(0, Steps.Length - 1));
        for (var i = 0; i < Math.Min(saved.Checks?.Length ?? 0, Ingredients.Count); i++) Ingredients[i].IsChecked = saved.Checks![i];
        foreach (var state in (saved.Timers ?? []).Take(20)) { var timer = new CookingTimer(state, _clock); Attach(timer); Timers.Add(timer); }
        CookingBatchID = saved.CookingBatchID; PlanOperation = saved.PlanOperation; FinishOperation = saved.FinishOperation; _finishFingerprint = saved.FinishFingerprint; HasRecovered = Recipe is not null || Timers.Count > 0;
        _restoring = false; UpdateTimerSummary();
    }
}

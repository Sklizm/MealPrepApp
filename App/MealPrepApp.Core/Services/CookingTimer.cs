using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace MealPrepApp.Services;
public sealed record CookingTimerState(Guid ID, string Name, DateTimeOffset? Deadline, TimeSpan PausedRemaining, bool IsCompleted);
public sealed partial class CookingTimer : LocalizedObservableObject
{
    private readonly TimeProvider _clock;
    private long _started;
    private TimeSpan _duration;
    public Guid ID { get; }
    public string Name { get; }
    public DateTimeOffset? Deadline { get; private set; }
    [ObservableProperty] private TimeSpan _remaining;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isCompleted;
    [LocalizedProperty] public string Text => IsCompleted ? L.Text("Text.TimeIsUp") : L.Text("Timer.Remaining", (int)Math.Ceiling(Remaining.TotalSeconds) / 60, (int)Math.Ceiling(Remaining.TotalSeconds) % 60);
    [LocalizedProperty] public string PauseLabel => L.Text(IsRunning ? "UX.Pause" : "UX.Resume");
    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(PauseLabel));
    public event EventHandler? Changed;
    public event EventHandler? Completed;
    public CookingTimer(string name, TimeSpan duration, TimeProvider? clock = null) : this(new(Guid.NewGuid(), name, null, duration, false), clock) { Resume(); }
    public CookingTimer(CookingTimerState state, TimeProvider? clock = null) {
        _clock = clock ?? TimeProvider.System; ID = state.ID; Name = state.Name; IsCompleted = state.IsCompleted;
        Remaining = state.Deadline is { } deadline ? deadline - _clock.GetUtcNow() : state.PausedRemaining;
        if (Remaining < TimeSpan.Zero) Remaining = TimeSpan.Zero;
        if (state.Deadline.HasValue && !state.IsCompleted) { _duration = Remaining; _started = _clock.GetTimestamp(); Deadline = state.Deadline; IsRunning = true; }
    }
    partial void OnRemainingChanged(TimeSpan value) => OnPropertyChanged(nameof(Text));
    partial void OnIsCompletedChanged(bool value) => OnPropertyChanged(nameof(Text));
    public CookingTimerState State => new(ID, Name, IsRunning ? Deadline : null, Remaining, IsCompleted);
    public void Tick() {
        if (!IsRunning) return;
        Remaining = _duration - _clock.GetElapsedTime(_started);
        if (Remaining <= TimeSpan.Zero) {
            Remaining = TimeSpan.Zero; IsRunning = false; Deadline = null; IsCompleted = true;
            Changed?.Invoke(this, EventArgs.Empty); Completed?.Invoke(this, EventArgs.Empty);
        }
    }
    private void Resume() {
        if (IsCompleted || Remaining <= TimeSpan.Zero) return;
        _duration = Remaining; _started = _clock.GetTimestamp(); Deadline = _clock.GetUtcNow() + Remaining; IsRunning = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }
    [RelayCommand] private void PauseResume() {
        if (IsCompleted) return;
        if (IsRunning) { Tick(); if (!IsRunning) return; IsRunning = false; Deadline = null; Changed?.Invoke(this, EventArgs.Empty); }
        else Resume();
    }
    [RelayCommand] private void AddMinute() {
        if (IsRunning) Tick();
        Remaining += TimeSpan.FromMinutes(1); IsCompleted = false;
        if (IsRunning) { _duration = Remaining; _started = _clock.GetTimestamp(); Deadline = _clock.GetUtcNow() + Remaining; }
        Changed?.Invoke(this, EventArgs.Empty);
    }
    [RelayCommand] private void Stop() { IsRunning = false; IsCompleted = false; Remaining = TimeSpan.Zero; Deadline = null; Changed?.Invoke(this, EventArgs.Empty); }
}

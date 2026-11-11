using MealPrepApp.Models;
using MealPrepApp.Services;
using Xunit;
namespace MealPrepApp.Tests;
public sealed class CookingRecoveryTests
{
    private sealed class Clock : TimeProvider {
        private DateTimeOffset _utc = new(2026,11,11,12,0,0,TimeSpan.Zero); private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public override DateTimeOffset GetUtcNow() => _utc;
        public void Advance(TimeSpan duration) { _utc += duration; _ticks += duration.Ticks; }
    }
    [Fact] public void RestartRestoresStepChecksServingsPausedAndRunningTimers()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); var path = Path.Combine(directory,"cooking.json");
        try {
            var clock = new Clock(); var first = new CookingSession(clock,path);
            first.StartRecipe(new RecipeFull { RecipeID = 4, Servings = 2, Instructions="1. Prepare\n2. Cook\n3. Serve", Ingredients=[new(){ IngredientName="Rice",Quantity=100 }] },4);
            first.NextStepCommand.Execute(null); first.Ingredients[0].IsChecked = true;
            first.TimerName="Rice"; first.TimerMinutes=3; first.StartTimerCommand.Execute(null);
            first.TimerName="Sauce"; first.TimerMinutes=5; first.StartTimerCommand.Execute(null);
            clock.Advance(TimeSpan.FromSeconds(30)); first.Tick(); first.Timers[1].PauseResumeCommand.Execute(null); first.Save();
            clock.Advance(TimeSpan.FromMinutes(1)); var second = new CookingSession(clock,path);
            Assert.True(second.HasRecovered); Assert.Equal(1,second.CurrentStep); Assert.Equal("Cook",second.CurrentInstruction); Assert.Equal(4,second.Servings);
            Assert.True(second.Ingredients[0].IsChecked); Assert.Equal(200,second.Ingredients[0].Quantity);
            Assert.True(second.Timers[0].IsRunning); Assert.Equal(TimeSpan.FromSeconds(90),second.Timers[0].Remaining);
            Assert.False(second.Timers[1].IsRunning); Assert.Equal(TimeSpan.FromSeconds(270),second.Timers[1].Remaining);
            Assert.Equal(new[]{"Rice","Sauce"},second.Timers.Select(t=>t.Name));
        } finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
    }
    [Fact] public void MissedDeadlineEmitsOneAlarmAfterRecoveryAndPausedTimersResume()
    {
        var clock = new Clock(); var state = new CookingTimerState(Guid.NewGuid(),"Rice",clock.GetUtcNow().AddMinutes(-1),TimeSpan.Zero,false);
        var timer = new CookingTimer(state,clock); var alarms=0; timer.Completed+=(_,_)=>alarms++;
        timer.Tick(); timer.Tick(); Assert.Equal(1,alarms); Assert.True(timer.IsCompleted);
        timer.AddMinuteCommand.Execute(null); timer.PauseResumeCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(30)); timer.Tick(); Assert.Equal(TimeSpan.FromSeconds(30),timer.Remaining);
        timer.PauseResumeCommand.Execute(null); clock.Advance(TimeSpan.FromMinutes(5)); timer.Tick(); Assert.Equal(TimeSpan.FromSeconds(30),timer.Remaining);
    }
    [Fact] public void RecoveryIsIsolatedAndCorruptRecoveryDoesNotStartAnAlarm()
    {
        var dir = Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString()); Directory.CreateDirectory(dir);
        try { var path=Path.Combine(dir,"one.json"); File.WriteAllText(path,"{broken");
            var one=new CookingSession(recoveryPath:path); Assert.Empty(one.Timers); Assert.Null(one.Recipe);
            one.TimerMinutes=1; one.StartTimerCommand.Execute(null);
            var two=new CookingSession(recoveryPath:Path.Combine(dir,"two.json")); Assert.Empty(two.Timers); Assert.False(two.HasRecovered);
        } finally { Directory.Delete(dir,true); }
    }
}

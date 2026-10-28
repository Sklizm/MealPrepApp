namespace MealPrepApp.Services;
public interface ILeaveGuard { Task<bool> CanLeaveAsync(); }
public enum LeaveDecision { Stay, Discard, SaveDraft }

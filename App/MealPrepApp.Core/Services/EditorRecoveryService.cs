using MealPrepApp.Models;
namespace MealPrepApp.Services;
public sealed class EditorRecoveryService(string? path)
{
    private readonly LocalJsonStore? _store = path is null ? null : new(path);
    private readonly object _gate = new();
    private long _revision;
    public long ReserveRevision() => Interlocked.Increment(ref _revision);
    public EditorRecovery? Read() { lock (_gate) return _store?.Read<EditorRecovery>(); }
    public bool Write(EditorRecovery recovery,long revision) { lock (_gate) return revision!=_revision || (_store?.Write(recovery) ?? true); }
    public void Delete() { lock (_gate) { _revision++; _store?.Delete(); } }
}

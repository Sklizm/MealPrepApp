using System.Text.Json;
namespace MealPrepApp.Services;
/// <summary>Atomic, account-scoped local recovery. Never contains database credentials.</summary>
public sealed class LocalJsonStore(string path)
{
    public T? Read<T>() {
        try {
            if (!File.Exists(path) || new FileInfo(path).Length > 24 * 1024 * 1024) return default;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { AppDiagnostics.Record(ex); return default; }
    }
    public bool Write<T>(T value) {
        var temporary = path + ".tmp";
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(value));
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temporary, path, true); return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { AppDiagnostics.Record(ex); return false; }
    }
    public void Delete() { try { File.Delete(path); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { AppDiagnostics.Record(ex); } }
}

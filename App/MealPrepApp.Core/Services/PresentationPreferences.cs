using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
namespace MealPrepApp.Services;
public enum Appearance { System, Light, Dark }
public sealed partial class PresentationPreferences : ObservableObject
{
    private readonly string _path;
    [ObservableProperty] private Appearance _theme;
    [ObservableProperty] private bool _recipeListView;
    [ObservableProperty] private bool _reducedMotion;
    public string? SaveError { get; private set; }
    public PresentationPreferences(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MealPrepApp", "preferences.json");
        try { if (File.Exists(_path)) { var data = JsonSerializer.Deserialize<Data>(File.ReadAllText(_path)); if (data != null) { _theme = Enum.IsDefined(data.Theme) ? data.Theme : Appearance.System; _recipeListView = data.RecipeListView; _reducedMotion = data.ReducedMotion; } } }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { AppDiagnostics.Record(ex); }
        PropertyChanged += (_, _) => Save();
    }
    private void Save()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!); var temporary = _path + ".tmp"; File.WriteAllText(temporary, JsonSerializer.Serialize(new Data(Theme, RecipeListView, ReducedMotion))); File.Move(temporary, _path, true); SaveError = null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { SaveError = "Preferințele nu au putut fi salvate."; AppDiagnostics.Record(ex); }
    }
    private sealed record Data(Appearance Theme, bool RecipeListView, bool ReducedMotion);
}

using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
namespace MealPrepApp.Services;
public enum Appearance { System, Light, Dark }
public sealed partial class PresentationPreferences : ObservableObject
{
    private readonly string _path;
    [ObservableProperty] private string _language = "en";
    partial void OnLanguageChanged(string value) { var normalized = LocalizationService.Normalize(value); if (value != normalized) Language = normalized; }
    [ObservableProperty] private Appearance _theme;
    [ObservableProperty] private bool _recipeListView;
    [ObservableProperty] private bool _reducedMotion;
    private bool _saveFailed;
    [LocalizedProperty]
    public string? SaveError => _saveFailed ? L.Text("Text.PreferencesCouldNotBeSaved") : null;
    public PresentationPreferences(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MealPrepApp", "preferences.json");
        try
        {
            if (File.Exists(_path))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(_path));
                var data = document.RootElement;
                if (data.ValueKind == JsonValueKind.Object)
                {
                    if (data.TryGetProperty("Theme", out var theme) && theme.ValueKind == JsonValueKind.Number && theme.TryGetInt32(out var number) && Enum.IsDefined((Appearance)number)) _theme = (Appearance)number;
                    if (data.TryGetProperty("RecipeListView", out var list)) _recipeListView = list.ValueKind == JsonValueKind.True;
                    if (data.TryGetProperty("ReducedMotion", out var motion)) _reducedMotion = motion.ValueKind == JsonValueKind.True;
                    if (data.TryGetProperty("Language", out var language)) _language = LocalizationService.Normalize(language.ValueKind == JsonValueKind.String ? language.GetString() : null);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { AppDiagnostics.Record(ex); }
        PropertyChanged += (_, _) => Save();
    }
    private void Save()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!); var temporary = _path + ".tmp"; File.WriteAllText(temporary, JsonSerializer.Serialize(new Data(Theme, RecipeListView, ReducedMotion, Language))); File.Move(temporary, _path, true); _saveFailed = false; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _saveFailed = true; AppDiagnostics.Record(ex); }
    }
    private sealed record Data(Appearance Theme, bool RecipeListView, bool ReducedMotion, string? Language = null);
}

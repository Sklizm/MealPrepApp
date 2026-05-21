namespace MealPrepApp.Services;

/// <summary>Lets a page (e.g. the Acasa dashboard tiles) switch the shell to another top-level
/// section, keeping the tab strip highlight in sync. Implemented by the ShellViewModel.</summary>
public interface IShellNavigator
{
    Task ShowSectionAsync(string section);
}

using MealPrepApp.Services;
namespace MealPrepApp.ViewModels.Shell;

/// <summary>Stand-in shown for shell sections that are not built yet. Each later phase
/// swaps its tab over from this to the real ViewModel.</summary>
public sealed class PlaceholderViewModel
{
    public PlaceholderViewModel(string sectionName)
    {
        SectionName = sectionName;
    }

    public string SectionName { get; }

    [LocalizedProperty]
    public string Message => L.Text("Section.Unavailable", SectionName);
}

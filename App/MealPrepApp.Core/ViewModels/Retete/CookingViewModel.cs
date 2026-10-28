using MealPrepApp.Services;
namespace MealPrepApp.ViewModels.Retete;
public sealed class CookingViewModel(CookingSession session)
{
    public CookingSession Session { get; }=session;
}

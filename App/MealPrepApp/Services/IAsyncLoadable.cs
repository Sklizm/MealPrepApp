namespace MealPrepApp.Services;

/// <summary>A ViewModel that loads its data asynchronously when navigated to.
/// The navigation service awaits <see cref="LoadAsync"/> after setting the view.</summary>
public interface IAsyncLoadable
{
    Task LoadAsync();
}

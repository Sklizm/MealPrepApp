using System.Collections.Concurrent;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
namespace MealPrepApp.Services;

[AttributeUsage(AttributeTargets.Property)]
public sealed class LocalizedPropertyAttribute : Attribute;

/// <summary>Refresh only translated presentation, preserving input buffers, selections and collection bindings.</summary>
public abstract class LocalizedObservableObject : ObservableObject, IDisposable
{
    private static readonly ConcurrentDictionary<Type, string[]> Properties = new();
    private readonly IDisposable _languageSubscription;
    protected LocalizedObservableObject() => _languageSubscription = L.Subscribe(this, static model => model.OnLanguageChanged());
    protected virtual void OnLanguageChanged()
    {
        foreach (var name in Properties.GetOrAdd(GetType(), static type => type.GetProperties()
            .Where(property => property.IsDefined(typeof(LocalizedPropertyAttribute), true)).Select(property => property.Name).ToArray()))
            OnPropertyChanged(name);
    }
    public virtual void Dispose() => _languageSubscription.Dispose();
    protected void ReleaseLocalization() => _languageSubscription.Dispose();
}

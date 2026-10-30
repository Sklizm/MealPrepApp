using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using MealPrepApp.Services;

namespace MealPrepApp.Desktop.Localization;

internal sealed class MessageSource(LocalizedMessage message) : LocalizedObservableObject, IDisposable
{
    [LocalizedProperty]
    public string Value => message.ToString();
    public override void Dispose() => ReleaseLocalization();
}

internal static class MessageBinding
{
    public static IDisposable Set(AvaloniaObject target, AvaloniaProperty property, LocalizedMessage message)
    {
        var source = new MessageSource(message);
        var binding = target.Bind(property, new Binding(nameof(MessageSource.Value)) { Source = source });
        return new Lifetime(binding, source);
    }
    public static void Set(Window owner, AvaloniaObject target, AvaloniaProperty property, LocalizedMessage message)
    {
        var lifetime = Set(target, property, message);
        owner.Closed += (_, _) => lifetime.Dispose();
    }
    private sealed class Lifetime(IDisposable binding, IDisposable source) : IDisposable
    {
        public void Dispose() { binding.Dispose(); source.Dispose(); }
    }
}

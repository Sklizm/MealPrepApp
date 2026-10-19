using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using MealPrepApp.Services;

namespace MealPrepApp.Desktop.Services;

public sealed class DesktopFilePickerService : IFilePickerService
{
    public async Task<string?> SaveFileDialogAsync(string title, string defaultFileName, string filter)
    {
        var topLevel = GetTopLevel();
        if (topLevel?.StorageProvider is not { CanSave: true } sp)
            return null;

        var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName
        });

        return file?.Path.LocalPath;
    }

    public async Task<string?> OpenFileDialogAsync(string title, string filter)
    {
        var topLevel = GetTopLevel();
        if (topLevel?.StorageProvider is not { CanOpen: true } sp)
            return null;

        var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return files.FirstOrDefault()?.Path.LocalPath;
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var active = desktop.Windows.FirstOrDefault(w => w.IsActive);
            return active ?? desktop.MainWindow;
        }
        return null;
    }
}

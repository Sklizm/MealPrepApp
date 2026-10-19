using Microsoft.Win32;

namespace MealPrepApp.Services;

public sealed class WpfFilePickerService : IFilePickerService
{
    public Task<string?> SaveFileDialogAsync(string title, string defaultFileName, string filter)
    {
        var save = new SaveFileDialog
        {
            Title = title,
            FileName = defaultFileName,
            Filter = filter
        };

        var result = save.ShowDialog() == true ? save.FileName : null;
        return Task.FromResult(result);
    }

    public Task<string?> OpenFileDialogAsync(string title, string filter)
    {
        var open = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };

        var result = open.ShowDialog() == true ? open.FileName : null;
        return Task.FromResult(result);
    }
}

namespace MealPrepApp.Services;

/// <summary>
/// Cross-platform file open/save dialog service.
/// Implemented via Microsoft.Win32 on WPF, and TopLevel.StorageProvider on Avalonia (macOS/Linux).
/// </summary>
public interface IFilePickerService
{
    /// <summary>
    /// Displays a save file dialog.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="defaultFileName">Default proposed file name.</param>
    /// <param name="filter">File filter (e.g. "Excel Workbook (*.xlsx)|*.xlsx" or extension).</param>
    /// <returns>Selected file path, or null if canceled.</returns>
    Task<string?> SaveFileDialogAsync(string title, string defaultFileName, string filter);

    /// <summary>
    /// Displays an open file dialog for choosing a single file.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="filter">File filter (e.g. "Images (*.jpg;*.png)|*.jpg;*.png").</param>
    /// <returns>Selected file path, or null if canceled.</returns>
    Task<string?> OpenFileDialogAsync(string title, string filter);
}

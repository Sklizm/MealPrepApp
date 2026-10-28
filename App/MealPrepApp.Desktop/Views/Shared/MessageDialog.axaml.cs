using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;


namespace MealPrepApp.Desktop.Views.Shared;

public enum MessageDialogKind { Info, Confirm, Error }

public partial class MessageDialog : Window
{
    private bool _dialogResult;

    public MessageDialog()
    {
        InitializeComponent();
    }

    public static async Task<bool> ShowModalAsync(MessageDialogKind kind, string title, string message,
        string? okText = null, string? cancelText = null)
    {
        var dlg = new MessageDialog();
        dlg.HeaderText.Text = title;
        dlg.MessageBody.Text = message;
        dlg.Configure(kind, okText, cancelText);

        var owner = ActiveOwner();
        return owner is not null && await dlg.ShowDialog<bool>(owner);
    }

    private void Configure(MessageDialogKind kind, string? okText, string? cancelText)
    {
        switch (kind)
        {
            case MessageDialogKind.Error:
                if (Application.Current?.TryFindResource("Danger", out var danger) == true && danger is IBrush db)
                    HeaderBorder.Background = db;
                HeaderGlyph.IsVisible = true;
                OkButton.Content = okText ?? "OK";
                CancelButton.IsVisible = false;
                break;

            case MessageDialogKind.Confirm:
                OkButton.Content = okText ?? "Da";
                CancelButton.Content = cancelText ?? "Nu";
                CancelButton.IsVisible = true;
                break;

            case MessageDialogKind.Info:
            default:
                OkButton.Content = okText ?? "OK";
                CancelButton.IsVisible = false;
                break;
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        _dialogResult = true;
        Close(_dialogResult);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        _dialogResult = false;
        Close(_dialogResult);
    }

    private static Window? ActiveOwner()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
        }
        return null;
    }
}

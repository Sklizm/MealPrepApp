using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace MealPrepApp.Views.Shared;

public enum MessageDialogKind { Info, Confirm, Error }

/// <summary>
/// The single styled replacement for <see cref="MessageBox"/>. Three variants:
/// <list type="bullet">
/// <item><b>Info</b> — dark header, one OK button.</item>
/// <item><b>Confirm</b> — dark header, Yes + No buttons (returns true on Yes).</item>
/// <item><b>Error</b> — danger-red header + warning glyph, one OK button.</item>
/// </list>
/// </summary>
public partial class MessageDialog : Window
{
    private MessageDialog() => InitializeComponent();

    /// <summary>Opens a modal dialog and returns true on OK/Yes, false on Cancel/No/close.</summary>
    public static bool Show(MessageDialogKind kind, string title, string message,
        string? okText = null, string? cancelText = null)
    {
        var dlg = new MessageDialog { Owner = ActiveOwner() };
        dlg.HeaderText.Text = title;
        dlg.MessageBody.Text = message;
        dlg.Configure(kind, okText, cancelText);
        return dlg.ShowDialog() == true;
    }

    private void Configure(MessageDialogKind kind, string? okText, string? cancelText)
    {
        switch (kind)
        {
            case MessageDialogKind.Error:
                HeaderBorder.Background = (Brush)FindResource("DangerBrush");
                HeaderGlyph.Text = "⚠";
                HeaderGlyph.Visibility = Visibility.Visible;
                OkButton.Content = okText ?? "OK";
                CancelButton.Visibility = Visibility.Collapsed;
                break;

            case MessageDialogKind.Confirm:
                OkButton.Content = okText ?? "Da";
                CancelButton.Content = cancelText ?? "Nu";
                CancelButton.Visibility = Visibility.Visible;
                break;

            case MessageDialogKind.Info:
            default:
                OkButton.Content = okText ?? "OK";
                CancelButton.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>Picks the active app window so the dialog can center over the right owner.</summary>
    private static Window? ActiveOwner()
    {
        var active = Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive);
        return active ?? Application.Current?.MainWindow;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            return;
        }
        base.OnKeyDown(e);
    }
}

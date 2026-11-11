using MealPrepApp.Desktop.Localization;
using MealPrepApp.Services;
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

    public static async Task<bool> ShowModalAsync(MessageDialogKind kind, LocalizedMessage title, LocalizedMessage message, LocalizedMessage? actionLabel = null, bool destructive = false)
    {
        var dlg = new MessageDialog();
        MessageBinding.Set(dlg, dlg.HeaderText, TextBlock.TextProperty, title);
        MessageBinding.Set(dlg, dlg, Window.TitleProperty, title);
        MessageBinding.Set(dlg, dlg.MessageBody, TextBlock.TextProperty, message);
        dlg.Configure(kind, null, null, destructive);
        if (kind == MessageDialogKind.Confirm)
        {
            MessageBinding.Set(dlg, dlg.OkButton, Button.ContentProperty, actionLabel ?? L.Message("Text.Continue"));
            MessageBinding.Set(dlg, dlg.CancelButton, Button.ContentProperty, L.Message("Text.Cancel"));
        }
        var owner = ActiveOwner();
        return owner is not null && await dlg.ShowDialog<bool>(owner);
    }

    internal void Configure(MessageDialogKind kind, string? okText, string? cancelText, bool destructive = false)
    {
        OkButton.Classes.Set("DialogCancel", kind != MessageDialogKind.Confirm);
        OkButton.Classes.Set("Primary", !destructive);
        OkButton.Classes.Set("Danger", destructive);
        switch (kind)
        {
            case MessageDialogKind.Error:
                HeaderGlyph.Bind(TextBlock.ForegroundProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Danger"));
                HeaderGlyph.IsVisible = true;
                OkButton.Content = okText ?? "OK";
                CancelButton.IsVisible = false;
                break;

            case MessageDialogKind.Confirm:
                OkButton.Content = okText ?? L.Text("Text.Continue");
                CancelButton.Content = cancelText ?? L.Text("Text.Cancel");
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

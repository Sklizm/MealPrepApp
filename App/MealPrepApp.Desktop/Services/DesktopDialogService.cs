using MealPrepApp.Desktop.Localization;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

using MealPrepApp.Desktop.Views.Auth;
using MealPrepApp.Desktop.Views.Ingrediente;
using MealPrepApp.Desktop.Views.Planificare;
using MealPrepApp.Desktop.Views.Shared;
using MealPrepApp.Services;
using MealPrepApp.ViewModels.Auth;
using MealPrepApp.ViewModels.Ingrediente;
using MealPrepApp.ViewModels.Planificare;

namespace MealPrepApp.Desktop.Services;

public sealed class DesktopDialogService : IDialogService
{
    public Task<bool> ConfirmAsync(LocalizedMessage title, LocalizedMessage message, LocalizedMessage actionLabel, bool destructive = false) =>
        MessageDialog.ShowModalAsync(MessageDialogKind.Confirm, title, message, actionLabel, destructive);
    public Task<bool> ConfirmAsync(LocalizedMessage title, LocalizedMessage message) =>
        MessageDialog.ShowModalAsync(MessageDialogKind.Confirm, title, message);
    public Task ShowErrorAsync(LocalizedMessage message)
    { App.Services.GetRequiredService<NotificationCenter>().Post(message, true); return Task.CompletedTask; }
    public Task ShowInfoAsync(LocalizedMessage title, LocalizedMessage message)
    { App.Services.GetRequiredService<NotificationCenter>().Post(L.Message("Notification.Combined", title, message)); return Task.CompletedTask; }

    public Task<bool> ConfirmAsync(string title, string message) =>
        MessageDialog.ShowModalAsync(MessageDialogKind.Confirm, title, message, okText: L.Text("Text.Continue"), cancelText: L.Text("Text.Cancel"));

    public Task ShowErrorAsync(string message)
    { App.Services.GetRequiredService<NotificationCenter>().Post(message,true);return Task.CompletedTask; }
    public Task ShowInfoAsync(string title,string message)
    { App.Services.GetRequiredService<NotificationCenter>().Post(title+" • "+message);return Task.CompletedTask; }
    public async Task<LeaveDecision> ChooseLeaveAsync()
    {
        var owner=ActiveOwner();if(owner is null)return LeaveDecision.Stay;
        return await CreateLeaveDialog().ShowDialog<LeaveDecision>(owner);
    }

    internal static Window CreateLeaveDialog()
    {
        var dialog=new Window {Title=L.Text("Text.UnsavedChanges"),Width=560,Height=300,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        dialog.Bind(Window.BackgroundProperty,new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("AppBackground"));
        var save=new Button {Content=L.Text("Text.SaveDraft")};save.Classes.Add("Primary");var discard=new Button {Content=L.Text("Text.DiscardChanges")};discard.Classes.Add("DestructiveLink");
        var stay=new Button {Content=L.Text("Text.StayHere")};stay.Classes.Add("Secondary");stay.Classes.Add("DialogCancel");
        save.Click+=(_,_)=>dialog.Close(LeaveDecision.SaveDraft);discard.Click+=(_,_)=>dialog.Close(LeaveDecision.Discard);stay.Click+=(_,_)=>dialog.Close(LeaveDecision.Stay);
        var heading = new TextBlock { Text=L.Text("Text.UnsavedChanges"), FontSize=22, FontWeight=Avalonia.Media.FontWeight.SemiBold, Margin=new Thickness(0,0,0,20) };
        var body = new TextBlock { Text=L.Text("Text.YouHaveUnsavedChangesHowWouldYouLikeToContinue"), TextWrapping=Avalonia.Media.TextWrapping.Wrap };
        var actions = new Grid { ColumnDefinitions=new ColumnDefinitions("Auto,*,Auto"), Margin=new Thickness(0,20,0,0) };
        actions.Children.Add(discard);
        var right = new StackPanel { Orientation=Avalonia.Layout.Orientation.Horizontal, Spacing=8, Children={stay,save} };
        Grid.SetColumn(right,2); actions.Children.Add(right);
        Grid.SetRow(body,1); Grid.SetRow(actions,2);
        dialog.Content = new MealPrepApp.Desktop.Controls.DialogLayout { Children={heading,body,actions} };
        MessageBinding.Set(dialog, heading, TextBlock.TextProperty, L.Message("Text.UnsavedChanges"));
        MessageBinding.Set(dialog, dialog, Window.TitleProperty, L.Message("Text.UnsavedChanges"));
        MessageBinding.Set(dialog, save, Button.ContentProperty, L.Message("Text.SaveDraft"));
        MessageBinding.Set(dialog, discard, Button.ContentProperty, L.Message("Text.DiscardChanges"));
        MessageBinding.Set(dialog, stay, Button.ContentProperty, L.Message("Text.StayHere"));
        MessageBinding.Set(dialog, body, TextBlock.TextProperty, L.Message("Text.YouHaveUnsavedChangesHowWouldYouLikeToContinue"));
        return dialog;
    }

    public async Task<bool> ShowDialogAsync(object viewModel)
    {
        Window? window = viewModel switch
        {
            PlanMealDialogViewModel => new PlanMealDialog(),
            MealPrepApp.ViewModels.Retete.FinishCookingDialogViewModel => new MealPrepApp.Desktop.Views.Retete.FinishCookingDialog(),
            MealPrepApp.ViewModels.Retete.PortionEventDialogViewModel => new MealPrepApp.Desktop.Views.Retete.PortionEventDialog(),
            InventoryBatchDialogViewModel => new InventoryBatchDialog(),
            ShoppingItemDialogViewModel => new ShoppingItemDialog(),
            PantryItemDialogViewModel => new PantryItemDialog(),
            IngredientAddDialogViewModel => new IngredientAddDialog(),
            IngredientNutritionDialogViewModel => new IngredientNutritionDialog(),
            ChangePasswordViewModel => new ChangePasswordDialog(),
            ForgotPasswordViewModel => new ForgotPasswordDialog(),
            _ => null
        };

        if (window is null)
            throw new NotSupportedException($"No desktop dialog window registered for '{viewModel.GetType().Name}'.");

        window.DataContext = viewModel;
        var owner = ActiveOwner();
        try { return owner is not null && await window.ShowDialog<bool>(owner); }
        finally { if (viewModel is IDisposable disposable) disposable.Dispose(); }
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

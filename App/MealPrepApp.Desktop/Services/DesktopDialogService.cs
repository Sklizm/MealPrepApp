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
    public Task<bool> ConfirmAsync(string title, string message) =>
        MessageDialog.ShowModalAsync(MessageDialogKind.Confirm, title, message, okText: "Da", cancelText: "Nu");

    public Task ShowErrorAsync(string message)
    { App.Services.GetRequiredService<NotificationCenter>().Post(message,true);return Task.CompletedTask; }
    public Task ShowInfoAsync(string title,string message)
    { App.Services.GetRequiredService<NotificationCenter>().Post(title+" • "+message);return Task.CompletedTask; }
    public async Task<LeaveDecision> ChooseLeaveAsync()
    {
        var owner=ActiveOwner();if(owner is null)return LeaveDecision.Stay;
        var dialog=new Window {Title="Modificări nesalvate",Width=520,Height=240,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        dialog.Bind(Window.BackgroundProperty,new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("AppBackground"));
        var save=new Button {Content="Salvează ciorna"};save.Classes.Add("Primary");var discard=new Button {Content="Renunță la modificări"};discard.Classes.Add("Secondary");
        var stay=new Button {Content="Rămâi aici"};stay.Classes.Add("Secondary");
        save.Click+=(_,_)=>dialog.Close(LeaveDecision.SaveDraft);discard.Click+=(_,_)=>dialog.Close(LeaveDecision.Discard);stay.Click+=(_,_)=>dialog.Close(LeaveDecision.Stay);
        dialog.Content=new StackPanel {Margin=new Thickness(24),Spacing=24,Children={new TextBlock {Text="Ai modificări nesalvate. Cum vrei să continui?",TextWrapping=Avalonia.Media.TextWrapping.Wrap},new WrapPanel {Children={save,discard,stay}}}};
        return await dialog.ShowDialog<LeaveDecision>(owner);
    }

    public async Task<bool> ShowDialogAsync(object viewModel)
    {
        Window? window = viewModel switch
        {
            PlanMealDialogViewModel => new PlanMealDialog(),
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

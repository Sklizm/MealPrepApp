using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MealPrepApp.Desktop.Services;
using MealPrepApp.Services;
using MealPrepApp.ViewModels;
using MealPrepApp.ViewModels.Retete;
using MealPrepApp.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.Desktop.Views;

public partial class AssistantWindow : Window
{
    public AssistantWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            if (DataContext is not AssistantViewModel vm) return;
            vm.IngredientFocusRequested += FocusIngredient;
            vm.SelectedTask = vm.Tasks.First(t => t.Id == vm.Kind);
            await vm.InitializeAsync();
        };
        Closed += (_, _) =>
        {
            if (DataContext is not AssistantViewModel vm) return;
            vm.IngredientFocusRequested -= FocusIngredient;
            vm.Dispose();
        };
    }

    private void FocusIngredient(AssistantIngredientRow row) => Dispatcher.UIThread.Post(() =>
    {
        var container = this.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(border => border.Classes.Contains("assistant-ingredient") && ReferenceEquals(border.DataContext, row));
        if (container is null) return;
        container.BringIntoView();
        if (row.HasInvalidQuantity)
            container.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(c => c.Classes.Contains("assistant-quantity"))?.Focus();
        else
            container.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault(c => c.Classes.Contains("assistant-mapping"))?.Focus();
    }, DispatcherPriority.Loaded);
    private async void Connect(object? sender,RoutedEventArgs e){if(DataContext is AssistantViewModel vm){var password=this.FindControl<TextBox>("Password")!;var value=password.Text??"";password.Text="";await vm.ConnectAsync(value);}}
    private async void ChooseImage(object? sender,RoutedEventArgs e)
    {
        if(DataContext is not AssistantViewModel vm)return;
        try{
            var file=await App.Services.GetRequiredService<IFilePickerService>().OpenFileDialogAsync(L.Text("Ai.Photo"),"Images|*.jpg;*.jpeg;*.png;*.webp");if(file is null)return;
            var processed=await RecipeImageProcessor.LoadAsync(file);
            var review=new AssistantImageWindow(processed.Image);var bytes=await review.ShowDialog<byte[]?>(this);if(bytes is null)return;
            vm.SetUpload(await App.Services.GetRequiredService<AiClient>().UploadAsync(bytes));vm.StatusContent=L.Message("Ai.ImageReady");
        }catch{vm.ErrorContent=L.Message("Ai.Error.UploadFailed");}
    }
    public static void Open(Control source, string kind)
    {
        if (TopLevel.GetTopLevel(source) is not Window owner)
            return;

        _ = UiOperation.RunAsync(async () =>
        {
            var vm = App.Services.GetRequiredService<AssistantViewModel>();
            vm.Kind = kind;
            await new AssistantWindow { DataContext = vm }.ShowDialog(owner);

            if (owner.DataContext is ShellViewModel { CurrentView: ReteteListViewModel recipes })
                await recipes.LoadAsync();
        });
    }
}

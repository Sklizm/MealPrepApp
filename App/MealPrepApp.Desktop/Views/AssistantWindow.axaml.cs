using Avalonia.Controls;
using Avalonia.Interactivity;
using MealPrepApp.Desktop.Services;
using MealPrepApp.Services;
using MealPrepApp.ViewModels;
using MealPrepApp.ViewModels.Retete;
using MealPrepApp.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.Desktop.Views;

public partial class AssistantWindow : Window
{
    public AssistantWindow(){InitializeComponent();Opened+=async(_,_)=>{try{if(DataContext is AssistantViewModel vm){vm.SelectedTask=vm.Tasks.First(t=>t.Id==vm.Kind);await vm.LoadAsync();}}catch{if(DataContext is AssistantViewModel vm)vm.ErrorContent=L.Message("Ai.Error.Unavailable");}};Closed+=(_,_)=>{if(DataContext is AssistantViewModel vm)vm.Dispose();};}
    private async void Connect(object? sender,RoutedEventArgs e){if(DataContext is AssistantViewModel vm){var password=this.FindControl<TextBox>("Password")!;var value=password.Text??"";password.Text="";await vm.ConnectAsync(value);}}
    private async void ChooseImage(object? sender,RoutedEventArgs e)
    {
        if(DataContext is not AssistantViewModel vm)return;
        try{
            var file=await App.Services.GetRequiredService<IFilePickerService>().OpenFileDialogAsync(L.Text("Ai.Photo"),"Images|*.jpg;*.jpeg;*.png;*.webp");if(file is null)return;
            var processed=await RecipeImageProcessor.LoadAsync(file);
            var review=new AssistantImageWindow(processed.Image);var bytes=await review.ShowDialog<byte[]?>(this);if(bytes is null)return;
            vm.UploadId=await App.Services.GetRequiredService<AiClient>().UploadAsync(bytes);vm.StatusContent=L.Message("Ai.ImageReady");
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

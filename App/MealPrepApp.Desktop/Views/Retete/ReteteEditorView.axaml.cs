using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MealPrepApp.Services;
using MealPrepApp.ViewModels.Retete;

namespace MealPrepApp.Desktop.Views.Retete;

public partial class ReteteEditorView : UserControl
{
    private ReteteEditorViewModel? _viewModel;

    public ReteteEditorView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => TrackEditor();
        DetachedFromVisualTree += (_, _) => UntrackEditor();
        DataContextChanged += (_, _) => { if (this.IsAttachedToVisualTree()) TrackEditor(); };
    }

    private void TrackEditor()
    {
        UntrackEditor();
        if (DataContext is not ReteteEditorViewModel editor) return;
        _viewModel = editor;
        editor.IngredientFocusRequested += FocusIngredient;
    }

    private void UntrackEditor()
    {
        if (_viewModel is not null) _viewModel.IngredientFocusRequested -= FocusIngredient;
        _viewModel = null;
    }

    private void FocusIngredient(RecipeIngredientRowViewModel row) => Dispatcher.UIThread.Post(() =>
    {
        var container = this.GetVisualDescendants().OfType<StackPanel>()
            .FirstOrDefault(panel => panel.Classes.Contains("review-ingredient") && ReferenceEquals(panel.DataContext, row));
        if (container is null) return;
        container.BringIntoView();
        if (row.SelectedIngredient is not null && row.SelectedUnit is not null && !AiValidation.Quantity(row.Quantity))
            container.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(c => c.Classes.Contains("review-quantity"))?.Focus();
        else
            container.GetVisualDescendants().OfType<AutoCompleteBox>().FirstOrDefault(c => c.Classes.Contains("review-mapping"))?.Focus();
    }, DispatcherPriority.Loaded);
    private void OpenAssistant(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => MealPrepApp.Desktop.Views.AssistantWindow.Open(this,"import");
}

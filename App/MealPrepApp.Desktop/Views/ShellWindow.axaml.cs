using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MealPrepApp.Desktop.Services;
using MealPrepApp.Desktop.Views.Auth;
using MealPrepApp.ViewModels.Auth;
using MealPrepApp.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.Desktop.Views;

public partial class ShellWindow : Window
{
    private ShellViewModel? _viewModel;
    private readonly Avalonia.Threading.DispatcherTimer _timer=new() { Interval=TimeSpan.FromSeconds(1) };
    private bool _allowClose;
    private bool _checkingClose;

    public ShellWindow()
    {
        InitializeComponent();

        _timer.Tick += (_,_) => { if(DataContext is ShellViewModel vm) vm.Cooking.Tick(); };
        Closing += async (_,e) =>
        {
            if(_allowClose || DataContext is not ShellViewModel vm || vm.CurrentView is not MealPrepApp.Services.ILeaveGuard guard) return;
            e.Cancel=true;
            if(_checkingClose) return;
            _checkingClose=true;
            try { if(await guard.CanLeaveAsync()) { _allowClose=true; Close(); } } finally { _checkingClose=false; }
        };
        Opened += (_, _) => _ = UiOperation.RunAsync(async () =>
        {
            if (DataContext is ShellViewModel vm)
            {
                _viewModel = vm;
                _timer.Start();
                vm.SettingsRequested += OnSettingsRequested;
                vm.SignOutRequested += OnSignOutRequested;
                vm.ChangePasswordRequested += OnChangePasswordRequested;
                await vm.InitializeAsync();
            }
        });
        Closed += (_, _) =>
        {
            _timer.Stop();
            if (_viewModel is null) return;
            _viewModel.SettingsRequested -= OnSettingsRequested;
            _viewModel.SignOutRequested -= OnSignOutRequested;
            _viewModel.ChangePasswordRequested -= OnChangePasswordRequested;
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyModifiers == KeyModifiers.None && e.Key is Key.Up or Key.Down or Key.Left or Key.Right &&
            e.Source is Avalonia.Visual source && (ReferenceEquals(source, NavigationPanel) || source.GetVisualAncestors().Contains(NavigationPanel)))
        {
            var targets = NavigationPanel.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled).ToArray();
            var index = Array.FindIndex(targets, b => b.IsKeyboardFocusWithin);
            if (index >= 0)
            {
                var next = targets[(index + (e.Key is Key.Up or Key.Left ? -1 : 1) + targets.Length) % targets.Length];
                next.Focus(Avalonia.Input.NavigationMethod.Directional); next.BringIntoView(); e.Handled = true; return;
            }
        }
        if(DataContext is not ShellViewModel vm)return;
        var primary=e.KeyModifiers.HasFlag(KeyModifiers.Control)||e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if(primary && e.Key==Key.F)
        {
            var search=PageContent.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t=>t.Name=="SearchBox" || t.Watermark?.StartsWith("Caut") == true);
            if(search!=null){search.Focus();search.SelectAll();e.Handled=true;}
        }
        if(primary && e.Key==Key.S && vm.CurrentView is MealPrepApp.ViewModels.Retete.ReteteEditorViewModel editor)
        {editor.SaveCommand.Execute(null);e.Handled=true;}
        if(e.Key==Key.Left && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if(vm.CurrentView is MealPrepApp.ViewModels.Retete.ReteteDetailViewModel detail)detail.BackCommand.Execute(null);
            else if(vm.CurrentView is MealPrepApp.ViewModels.Retete.ReteteEditorViewModel editing)editing.CancelCommand.Execute(null);
            e.Handled=true;
        }
    }
    private void OnSettingsRequested(object? sender,EventArgs e)
    {
        var dialog=new SettingsWindow();
        _ = UiOperation.RunAsync(() => dialog.ShowDialog(this));
    }
    private void OnSignOutRequested(object? sender, EventArgs e)
    {
        _allowClose=true;
        WindowCoordinator.ShowLogin();
    }

    private void OnChangePasswordRequested(object? sender, EventArgs e)
    {
        var dialog = new ChangePasswordDialog
        {
            DataContext = App.Services.GetRequiredService<ChangePasswordViewModel>()
        };
        _ = UiOperation.RunAsync(() => dialog.ShowDialog(this));
    }
}

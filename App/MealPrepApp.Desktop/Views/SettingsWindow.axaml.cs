using Avalonia.Controls;
using Avalonia.Interactivity;
using MealPrepApp.Services;
namespace MealPrepApp.Desktop.Views;
public partial class SettingsWindow:Window
{
    private readonly IDisposable _languageSubscription;
    private readonly PresentationPreferences _preferences;
    public SettingsWindow() : this((Avalonia.Application.Current as App)?.Preferences ?? new PresentationPreferences()) { }
    public SettingsWindow(PresentationPreferences preferences)
    {
        _preferences = preferences;
        InitializeComponent();
        AppVersion.Text="MealPrepApp " + typeof(App).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute),false).Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion.Split('+')[0];
        _languageSubscription = L.Subscribe(this, static window => window.RefreshThemeLabels());
        Closed += (_, _) => _languageSubscription.Dispose();
        LanguagePicker.ItemsSource = new[] { "English", "Русский", "Română" };
        LanguagePicker.SelectedIndex = Array.IndexOf(new[] { "en", "ru", "ro" }, L.Current.Language);
        RefreshThemeLabels();
        ThemePicker.SelectedIndex=0;
        ThemePicker.SelectedIndex=(int)_preferences.Theme;
        ListViewCheck.IsChecked=_preferences.RecipeListView;
        MotionCheck.IsChecked=_preferences.ReducedMotion;
        LanguagePicker.SelectionChanged += (_, _) => { if (!_refreshing && LanguagePicker.SelectedIndex >= 0) { _preferences.Language = new[] { "en", "ru", "ro" }[LanguagePicker.SelectedIndex]; L.Current.Language = _preferences.Language; } };
        ThemePicker.SelectionChanged+=(_,_)=> { if (_refreshing) return; _preferences.Theme=(Appearance)Math.Max(0,ThemePicker.SelectedIndex); Status.Text=_preferences.SaveError; };
        ListViewCheck.IsCheckedChanged+=(_,_)=>_preferences.RecipeListView=ListViewCheck.IsChecked==true;
        MotionCheck.IsCheckedChanged+=(_,_)=>_preferences.ReducedMotion=MotionCheck.IsChecked==true;
    }

    private bool _refreshing;
    private void RefreshThemeLabels()
    {
        _refreshing = true;
        var selected = ThemePicker.SelectedIndex;
        ThemePicker.ItemsSource = new[] { L.Text("Theme.System"), L.Text("Theme.Light"), L.Text("Theme.Dark") };
        ThemePicker.SelectedIndex = selected;
        LanguagePicker.SelectedIndex = Array.IndexOf(new[] { "en", "ru", "ro" }, L.Current.Language);
        _refreshing = false;
        Status.Text = _preferences.SaveError;
    }

    private async void EditConnection(object? sender, RoutedEventArgs e)
    {
        if (Avalonia.Application.Current is App app)
            await new ConnectionSetupWindow(app.ConnectionSettings).ShowDialog(this);
    }
}

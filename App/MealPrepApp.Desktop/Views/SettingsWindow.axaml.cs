using Avalonia.Controls;
using Avalonia.Interactivity;
using MealPrepApp.Services;
namespace MealPrepApp.Desktop.Views;
public partial class SettingsWindow:Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        AppVersion.Text="MealPrepApp " + typeof(App).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute),false).Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion.Split('+')[0];
        if(Avalonia.Application.Current is not App app) return;
        ThemePicker.ItemsSource=new[]{"Sistem","Luminos","Întunecat"};
        ThemePicker.SelectedIndex=(int)app.Preferences.Theme;
        ListViewCheck.IsChecked=app.Preferences.RecipeListView;
        MotionCheck.IsChecked=app.Preferences.ReducedMotion;
        ThemePicker.SelectionChanged+=(_,_)=> { app.Preferences.Theme=(Appearance)Math.Max(0,ThemePicker.SelectedIndex); Status.Text=app.Preferences.SaveError; };
        ListViewCheck.IsCheckedChanged+=(_,_)=>app.Preferences.RecipeListView=ListViewCheck.IsChecked==true;
        MotionCheck.IsCheckedChanged+=(_,_)=>app.Preferences.ReducedMotion=MotionCheck.IsChecked==true;
    }

    private async void EditConnection(object? sender, RoutedEventArgs e)
    {
        if (Avalonia.Application.Current is App app)
            await new ConnectionSetupWindow(app.ConnectionSettings).ShowDialog(this);
    }
}

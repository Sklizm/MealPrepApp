using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MealPrepApp.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace MealPrepApp.Desktop.Views;
public partial class SettingsWindow:Window
{
    private SqlConnectionStringBuilder _connection=new();
    private readonly string _path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MealPrepApp","appsettings.Local.json");
    public SettingsWindow()
    {
        InitializeComponent();
        if(Avalonia.Application.Current is not App app) return;
        ThemePicker.ItemsSource=new[]{"Sistem","Luminos","Întunecat"};
        ThemePicker.SelectedIndex=(int)app.Preferences.Theme;
        ListViewCheck.IsChecked=app.Preferences.RecipeListView;
        MotionCheck.IsChecked=app.Preferences.ReducedMotion;
        ThemePicker.SelectionChanged+=(_,_)=> { app.Preferences.Theme=(Appearance)Math.Max(0,ThemePicker.SelectedIndex); Status.Text=app.Preferences.SaveError; };
        ListViewCheck.IsCheckedChanged+=(_,_)=>app.Preferences.RecipeListView=ListViewCheck.IsChecked==true;
        MotionCheck.IsCheckedChanged+=(_,_)=>app.Preferences.ReducedMotion=MotionCheck.IsChecked==true;
        var config=App.Services.GetRequiredService<IConfiguration>();
        _connection=new SqlConnectionStringBuilder(config.GetConnectionString("MealPrepDB") ?? "");
        ServerBox.Text=_connection.DataSource; DatabaseBox.Text=_connection.InitialCatalog; UserBox.Text=_connection.UserID;
    }
    private string BuildConnection()
    {
        var builder=new SqlConnectionStringBuilder(_connection.ConnectionString) { DataSource=ServerBox.Text?.Trim() ?? "", InitialCatalog=DatabaseBox.Text?.Trim() ?? "",UserID=UserBox.Text?.Trim() ?? "",ConnectTimeout=5 };
        if(!string.IsNullOrEmpty(PasswordBox.Text)) builder.Password=PasswordBox.Text;
        if(string.IsNullOrWhiteSpace(builder.DataSource)||string.IsNullOrWhiteSpace(builder.InitialCatalog)) throw new ArgumentException();
        return builder.ConnectionString;
    }
    private async void TestConnection(object? sender,RoutedEventArgs e)
    {
        if(sender is Button b) b.IsEnabled=false;
        Status.Text="Se verifică conexiunea…";
        try { await using var connection=new SqlConnection(BuildConnection()); await connection.OpenAsync(); Status.Text="Conexiune disponibilă."; }
        catch(Exception ex) { AppDiagnostics.Record(ex); Status.Text="Conexiunea nu este disponibilă. Verifică serverul, rețeaua și datele de acces."; }
        finally { if(sender is Button button) button.IsEnabled=true; }
    }
    private async void SaveConnection(object? sender,RoutedEventArgs e)
    {
        try
        {
            var connection=BuildConnection();
            var json=File.Exists(_path) ? System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(_path))!.AsObject() : new System.Text.Json.Nodes.JsonObject();
            json["ConnectionStrings"] ??= new System.Text.Json.Nodes.JsonObject(); json["ConnectionStrings"]!["MealPrepDB"]=connection;
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary=_path+".tmp";
            await File.WriteAllTextAsync(temporary,json.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
            if(!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary,UnixFileMode.UserRead|UnixFileMode.UserWrite);
            File.Move(temporary,_path,true);
            _connection=new SqlConnectionStringBuilder(connection); PasswordBox.Text="";
            Status.Text="Conexiunea a fost salvată. Repornește aplicația.";
        }
        catch(Exception ex) { AppDiagnostics.Record(ex); Status.Text="Setările nu au putut fi salvate. Verifică valorile și accesul la fișier."; }
    }
}

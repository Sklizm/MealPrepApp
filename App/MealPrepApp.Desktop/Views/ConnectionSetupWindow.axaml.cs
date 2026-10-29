using Avalonia.Controls;
using Avalonia.Interactivity;
using MealPrepApp.Desktop.Services;
using MealPrepApp.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MealPrepApp.Desktop.Views;

public partial class ConnectionSetupWindow : Window
{
    private readonly ConnectionSettingsStore _store;
    private readonly SqlConnectionStringBuilder _existing;
    private readonly Func<string, Task>? _saved;
    private readonly Func<string, CancellationToken, Task> _testConnection;
    private readonly CancellationTokenSource _closing = new();

    public ConnectionSetupWindow() : this(new ConnectionSettingsStore()) { }

    public ConnectionSetupWindow(ConnectionSettingsStore store, Func<string, Task>? saved = null,
        Func<string, CancellationToken, Task>? testConnection = null)
    {
        InitializeComponent();
        _store = store;
        _saved = saved;
        _testConnection = testConnection ?? TestSqlConnection;
        var configuration = store.Load().Configuration;
        try { _existing = new(configuration.GetConnectionString("MealPrepDB") ?? ""); }
        catch (ArgumentException) { _existing = new(); }
        ServerBox.Text = _existing.DataSource;
        DatabaseBox.Text = string.IsNullOrEmpty(_existing.InitialCatalog) ? "MealPrepDB" : _existing.InitialCatalog;
        UserBox.Text = string.IsNullOrEmpty(_existing.UserID) ? "mealprep_app" : _existing.UserID;
        TrustCertificate.IsChecked = _existing.TrustServerCertificate;
        Closed += (_, _) => _closing.Cancel();
    }

    private static async Task TestSqlConnection(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
    }

    private string BuildConnection()
    {
        var connection = new SqlConnectionStringBuilder(_existing.ConnectionString)
        {
            DataSource = ServerBox.Text?.Trim() ?? "",
            InitialCatalog = DatabaseBox.Text?.Trim() ?? "",
            UserID = UserBox.Text?.Trim() ?? "",
            IntegratedSecurity = false,
            ConnectTimeout = 5,
            Encrypt = _existing.Encrypt == SqlConnectionEncryptOption.Strict
                ? SqlConnectionEncryptOption.Strict : SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = TrustCertificate.IsChecked == true
        };
        if (!string.IsNullOrEmpty(PasswordBox.Text)) connection.Password = PasswordBox.Text;
        if (!ConnectionSettingsStore.IsConfigured(connection.ConnectionString))
            throw new ArgumentException("Incomplete connection settings.");
        return connection.ConnectionString;
    }

    private void SetBusy(bool busy)
    {
        Fields.IsEnabled = TestButton.IsEnabled = SaveButton.IsEnabled = !busy;
    }

    private async void TestConnection(object? sender, RoutedEventArgs e)
    {
        SetBusy(true);
        Status.Text = "Se verifică conexiunea…";
        try
        {
            await _testConnection(BuildConnection(), _closing.Token);
            Status.Text = "Conexiune disponibilă.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppDiagnostics.Record(ex);
            Status.Text = "Conexiunea nu este disponibilă. Verifică adresa, Tailscale, certificatul și datele de acces.";
        }
        finally { SetBusy(false); }
    }

    private async void SaveConnection(object? sender, RoutedEventArgs e)
    {
        SetBusy(true);
        try
        {
            var connection = BuildConnection();
            await _store.SaveAsync(connection);
            if (_closing.IsCancellationRequested) return;
            PasswordBox.Text = "";
            if (_saved != null) await _saved(connection);
            else Close();
        }
        catch (Exception ex)
        {
            AppDiagnostics.Record(ex);
            Status.Text = "Setările nu au putut fi salvate. Verifică toate câmpurile și accesul la fișier.";
        }
        finally { SetBusy(false); }
    }

    private void Cancel(object? sender, RoutedEventArgs e) => Close();
}

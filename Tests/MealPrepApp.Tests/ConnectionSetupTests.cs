using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using MealPrepApp.Desktop.Services;
using MealPrepApp.Desktop.Views;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MealPrepApp.Tests;

public sealed partial class RegressionTests
{
    private const string FixtureConnection = "Server=example.invalid,1433;Database=MealPrepDB;User Id=fixture;Password=fixture-secret;Encrypt=True;TrustServerCertificate=True";

    [Fact]
    public void ConnectionConfigurationRejectsMissingMalformedAndPlaceholderValues()
    {
        foreach (var value in new[] { null, "", "bad connection", "Server=host;Database=db", FixtureConnection.Replace("fixture-secret", "SET_AT_DEPLOYMENT"), FixtureConnection.Replace("example.invalid", "<server>") })
            Assert.False(ConnectionSettingsStore.IsConfigured(value));
        Assert.True(ConnectionSettingsStore.IsConfigured(FixtureConnection));
        Assert.True(ConnectionSettingsStore.IsConfigured(new SqlConnectionStringBuilder(FixtureConnection)
            { Password = "a<secret>;&" }.ConnectionString));
        Assert.True(ConnectionSettingsStore.IsConfigured("Server=host;Database=db;Integrated Security=True"));
    }

    [Fact]
    public async Task ConnectionOverridesKeepPrecedenceAndSavePrivatelyWithoutChangingPreferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var basePath = Path.Combine(directory, "base.json");
            var local = Path.Combine(directory, "development.json");
            var user = Path.Combine(directory, "user.json");
            static string Settings(string connection) => new JsonObject { ["ConnectionStrings"] = new JsonObject { ["MealPrepDB"] = connection } }.ToJsonString();
            File.WriteAllText(basePath, Settings(FixtureConnection.Replace("example.invalid", "base.invalid")));
            File.WriteAllText(local, Settings(FixtureConnection.Replace("example.invalid", "local.invalid")));
            var store = new ConnectionSettingsStore(user, basePath, [local]);
            Assert.Contains("local.invalid", store.Load().Configuration.GetConnectionString("MealPrepDB"));
            File.WriteAllText(user, "{\"unrelated\":\"preserve\"}");
            var preferences = Path.Combine(directory, "preferences.json");
            File.WriteAllText(preferences, "unchanged");
            await store.SaveAsync(FixtureConnection);
            var loaded = store.Load();
            Assert.False(loaded.NeedsSetup);
            Assert.Equal(FixtureConnection, loaded.Configuration.GetConnectionString("MealPrepDB"));
            Assert.Equal("preserve", JsonNode.Parse(File.ReadAllText(user))!["unrelated"]!.GetValue<string>());
            Assert.Equal("unchanged", File.ReadAllText(preferences));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
            if (!OperatingSystem.IsWindows())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(user));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task MalformedUserConfigurationOpensSetupAndCanBeRepaired()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var user = Path.Combine(directory, "user.json");
            var store = new ConnectionSettingsStore(user, Path.Combine(directory, "absent.json"), []);
            Assert.True(store.Load().NeedsSetup);
            File.WriteAllText(user, "{broken");
            Assert.True(store.Load().NeedsSetup);
            await store.SaveAsync(FixtureConnection);
            Assert.False(store.Load().NeedsSetup);
            Assert.Equal(FixtureConnection, store.Load().Configuration.GetConnectionString("MealPrepDB"));
            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync("broken"));
            Assert.False(store.Load().NeedsSetup);
            File.WriteAllText(Path.Combine(directory, "absent.json"), "{malformed base");
            Assert.False(store.Load().NeedsSetup); // a repaired user override is authoritative
        }
        finally { Directory.Delete(directory, true); }
    }

    [AvaloniaFact]
    public void ConnectionSetupMasksCredentialsAndCancellationLeavesSettingsUntouched()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var path = Path.Combine(directory, "user.json");
        var store = new ConnectionSettingsStore(path, path, []);
        var saved = false;
        var window = new ConnectionSetupWindow(store, _ => { saved = true; return Task.CompletedTask; });
        window.Show();
        Assert.Equal('●', window.FindControl<TextBox>("PasswordBox")!.PasswordChar);
        Assert.Equal("MealPrepDB", window.FindControl<TextBox>("DatabaseBox")!.Text);
        window.FindControl<Button>("SaveButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(saved);
        Assert.Contains("Verifică", window.FindControl<TextBlock>("Status")!.Text);
        Assert.True(window.FindControl<Button>("SaveButton")!.IsEnabled);
        window.FindControl<Button>("CancelButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(window.IsVisible);
        Assert.False(File.Exists(path));
        Assert.False(saved);
    }

    [AvaloniaFact]
    public void ConnectionTestCanFailRetryAndCancelWithoutExposingCredentials()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "user.json");
        var store = new ConnectionSettingsStore(path, path, []);
        var attempts = 0;
        CancellationToken pending = default;
        var window = new ConnectionSetupWindow(store, testConnection: (_, token) =>
        {
            attempts++;
            if (attempts == 1) return Task.FromException(new InvalidOperationException("fixture-secret"));
            if (attempts == 2) return Task.CompletedTask;
            pending = token;
            return Task.Delay(Timeout.Infinite, token);
        });
        window.Show();
        window.FindControl<TextBox>("ServerBox")!.Text = "example.invalid,1433";
        window.FindControl<TextBox>("PasswordBox")!.Text = "fixture-secret";
        var test = window.FindControl<Button>("TestButton")!;
        var status = window.FindControl<TextBlock>("Status")!;
        test.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Contains("nu este disponibilă", status.Text);
        Assert.DoesNotContain("fixture-secret", status.Text);
        Assert.True(test.IsEnabled);
        test.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("Conexiune disponibilă.", status.Text);
        test.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(test.IsEnabled);
        Assert.False(window.FindControl<Button>("SaveButton")!.IsEnabled);
        window.FindControl<Button>("CancelButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(pending.IsCancellationRequested);
        Assert.False(File.Exists(path));
    }

    [AvaloniaFact]
    public void ConnectionSetupSavesAndContinuesWithoutRestartOrNetworkAccess()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var path = Path.Combine(directory, "user.json");
        var store = new ConnectionSettingsStore(path, path, []);
        string? saved = null;
        var window = new ConnectionSetupWindow(store, connection => { saved = connection; return Task.CompletedTask; });
        try
        {
            window.Show();
            window.FindControl<TextBox>("ServerBox")!.Text = "example.invalid,1433";
            window.FindControl<TextBox>("PasswordBox")!.Text = "a<secret>;&";
            window.FindControl<Button>("SaveButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(SpinWait.SpinUntil(() =>
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                return saved != null;
            }, TimeSpan.FromSeconds(5)));
            Assert.Equal(saved, store.Load().Configuration.GetConnectionString("MealPrepDB"));
            var connection = new SqlConnectionStringBuilder(saved);
            Assert.Equal("a<secret>;&", connection.Password);
            Assert.Equal(SqlConnectionEncryptOption.Mandatory, connection.Encrypt);
            Assert.Empty(window.FindControl<TextBox>("PasswordBox")!.Text ?? "");
        }
        finally
        {
            window.Close();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}

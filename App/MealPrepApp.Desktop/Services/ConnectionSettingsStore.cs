using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MealPrepApp.Desktop.Services;

public sealed record ConnectionConfiguration(IConfigurationRoot Configuration, bool NeedsSetup);

/// <summary>Loads legacy overrides and writes credentials only to the per-user directory.</summary>
public sealed class ConnectionSettingsStore
{
    public string UserPath { get; }
    private readonly string _basePath;
    private readonly string[] _localPaths;

    public ConnectionSettingsStore(string? userPath = null, string? basePath = null, string[]? localPaths = null)
    {
        UserPath = userPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MealPrepApp", "appsettings.Local.json");
        _basePath = basePath ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        _localPaths = localPaths ??
        [
            Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Local.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "App", "MealPrepApp.Desktop", "appsettings.Local.json")
        ];
    }

    public ConnectionConfiguration Load()
    {
        var invalid = false;
        var builder = new ConfigurationBuilder();
        foreach (var path in new[] { _basePath, _localPaths.FirstOrDefault(File.Exists), UserPath })
        {
            if (path == null || !File.Exists(path)) continue;
            // Ignore unreadable JSON so a broken user file can be repaired in the UI.
            var fileInvalid = false;
            var file = new ConfigurationBuilder().AddJsonFile(source =>
            {
                source.Path = Path.GetFullPath(path);
                source.Optional = true;
                source.OnLoadException = context => { fileInvalid = true; context.Ignore = true; };
                source.ResolveFileProvider();
            }).Build();
            using var sourceLifetime = file as IDisposable;
            if (fileInvalid) invalid = true;
            else if (file.GetConnectionString("MealPrepDB") != null) invalid = false;
            builder.AddInMemoryCollection(file.AsEnumerable());
        }
        var configuration = builder.Build();
        return new(configuration, invalid || !IsConfigured(configuration.GetConnectionString("MealPrepDB")));
    }

    public static bool IsConfigured(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;
        try
        {
            var connection = new SqlConnectionStringBuilder(connectionString);
            static bool Present(string value) => !string.IsNullOrWhiteSpace(value)
                && !value.Contains('<') && !value.Contains('>')
                && !value.Contains("YOUR_HOME_SERVER_IP", StringComparison.OrdinalIgnoreCase);
            return Present(connection.DataSource) && Present(connection.InitialCatalog)
                && (connection.IntegratedSecurity || (Present(connection.UserID)
                    && !string.IsNullOrWhiteSpace(connection.Password)
                    && !new[] { "SET_AT_DEPLOYMENT", "<deployment-password>", "<password>" }
                        .Contains(connection.Password, StringComparer.OrdinalIgnoreCase)));
        }
        catch (ArgumentException) { return false; }
    }

    public async Task SaveAsync(string connectionString)
    {
        if (!IsConfigured(connectionString)) throw new ArgumentException("Incomplete connection settings.");
        JsonObject json;
        try { json = JsonNode.Parse(await File.ReadAllTextAsync(UserPath)) as JsonObject ?? new(); }
        catch (FileNotFoundException) { json = new(); }
        catch (DirectoryNotFoundException) { json = new(); }
        catch (JsonException) { json = new(); }
        if (json["ConnectionStrings"] is not JsonObject) json["ConnectionStrings"] = new JsonObject();
        json["ConnectionStrings"]!["MealPrepDB"] = connectionString;
        Directory.CreateDirectory(Path.GetDirectoryName(UserPath)!);
        var temporary = UserPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var stream = new FileStream(temporary, options))
                await JsonSerializer.SerializeAsync(stream, json, new JsonSerializerOptions { WriteIndented = true });
            File.Move(temporary, UserPath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

using System.ComponentModel;
using Microsoft.Data.SqlClient;

namespace MealPrepApp.Services;

/// <summary>Local diagnostics contain exception types and stack frames, never messages or settings.</summary>
public static class AppDiagnostics
{
    public static void Record(Exception exception)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MealPrepApp", "logs");
            Directory.CreateDirectory(directory);
            var details = exception is SqlException sql
                ? $" SQL errors=[{string.Join(",", sql.Errors.Cast<SqlError>().Select(e => $"{e.Number}/{e.State}/{e.Class}"))}] ConnectionId={sql.ClientConnectionId}"
                : "";
            if (exception.InnerException is Win32Exception native)
                details += $" NativeError={native.NativeErrorCode}";
            File.AppendAllText(Path.Combine(directory, "errors.log"),
                $"{DateTime.UtcNow:O} {exception.GetType().FullName}{details}\n{exception.StackTrace}\n");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

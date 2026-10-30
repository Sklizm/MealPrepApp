using Microsoft.Data.SqlClient;
using MealPrepApp.Services;

namespace MealPrepApp.Data;

/// <summary>
/// Wraps a <see cref="SqlException"/> with a localized, user-facing message.
/// Repositories throw this; ViewModels catch it and hand it to the dialog service.
/// </summary>
public sealed class AppDbException : Exception
{
    /// <summary>The SQL Server error number. The DB raises 50001-50004 for known business errors.</summary>
    public int ErrorNumber { get; }

    /// <summary>True when <see cref="ErrorNumber"/> is one of the known 50001-50004 business errors.</summary>
    public bool IsKnown { get; }

    /// <summary>Localized message safe to show directly to the user.</summary>
    public LocalizedMessage LocalizedMessage => DbExceptionMapper.ToMessage(ErrorNumber);
    public string FriendlyMessage => LocalizedMessage.ToString();

    public AppDbException(SqlException inner)
        : base(inner.Message, inner)
    {
        AppDiagnostics.Record(inner);
        ErrorNumber = inner.Number;
        IsKnown = DbExceptionMapper.IsKnown(inner.Number);

    }
}

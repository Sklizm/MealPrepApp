using Microsoft.Data.SqlClient;

namespace MealPrepApp.Data;

/// <summary>
/// Wraps a <see cref="SqlException"/> with a Romanian, user-facing message.
/// Repositories throw this; ViewModels catch it and hand it to the dialog service.
/// </summary>
public sealed class AppDbException : Exception
{
    /// <summary>The SQL Server error number. The DB raises 50001-50004 for known business errors.</summary>
    public int ErrorNumber { get; }

    /// <summary>True when <see cref="ErrorNumber"/> is one of the known 50001-50004 business errors.</summary>
    public bool IsKnown { get; }

    /// <summary>Romanian message safe to show directly to the user.</summary>
    public string FriendlyMessage { get; }

    public AppDbException(SqlException inner)
        : base(inner.Message, inner)
    {
        ErrorNumber = inner.Number;
        IsKnown = DbExceptionMapper.IsKnown(inner.Number);
        FriendlyMessage = DbExceptionMapper.ToFriendlyMessage(inner.Number);
    }
}

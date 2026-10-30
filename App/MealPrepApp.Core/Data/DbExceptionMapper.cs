using MealPrepApp.Services;
namespace MealPrepApp.Data;

/// <summary>
/// Maps SQL Server error numbers to localized messages.
/// The DB raises these custom codes from its stored procedures:
///   50000 — invalid input (e.g. pantry quantity must be &gt; 0)
///   50001 — password reused (sp_ChangePassword)
///   50002 — not authorized (ownership check failed)
///   50003 — entity not found
///   50004 — stale row / optimistic concurrency conflict (sp_UpdateRecipe)
///   50005 — password reset account not found (sp_ResetForgottenPassword)
/// It also translates the common *native* SQL errors so a constraint violation never reaches
/// the user as an opaque "unexpected" message (e.g. 2627 when the same ingredient is added
/// twice to one recipe).
/// </summary>
public static class DbExceptionMapper
{
    public static bool IsKnown(int errorNumber) =>
        (errorNumber is >= 50000 and <= 50005)
        || errorNumber is 2627 or 2601 or 547 or 515 or 2628 or 8152
            or -2 or 258 or 53 or 11001 or 10060 or 10061 or 18456 or 4060 or 2812;

    public static string ToFriendlyMessage(int errorNumber) => ToMessage(errorNumber).ToString();

    public static LocalizedMessage ToMessage(int errorNumber) => errorNumber switch
    {
        50000 => L.Message("Text.InvalidDataCheckTheValuesEntered"),
        50001 => L.Message("Text.ThisPasswordWasUsedRecentlyChooseANewPassword"),
        50002 => L.Message("Text.YouDoNotHavePermissionForThisAction"),
        50003 => L.Message("Text.TheItemWasNotFound"),
        50004 => L.Message("Text.TheRecipeChangedInAnotherSessionReloadAndTryAgain"),
        50005 => L.Message("Text.NoAccountMatchesTheseDetails"),

        // Connection failures may occur before SQL Server receives the operation.
        -2 or 258 => L.Message("Text.TheDatabaseConnectionOrOperationTimedOutCheckYourNetworkAndServerAvailability"),
        53 or 11001 or 10060 or 10061 => L.Message("Text.CouldNotContactTheDatabaseServerCheckYourNetworkAndServerAddress"),
        18456 => L.Message("Text.DatabaseServerAuthenticationFailedCheckTheAppConfiguration"),
        4060 => L.Message("Text.TheConfiguredDatabaseIsUnavailableOrAccessIsDenied"),

        2812 => L.Message("Text.TheDatabaseNeedsAnUpdateForThisAppVersionContactTheAdministrator"),

        // Native SQL Server errors
        2627 or 2601 => L.Message("Text.ThisRecordAlreadyExistsDuplicateValue"),
        547 => L.Message("Text.InvalidDataAValueViolatesADatabaseRule"),
        515 => L.Message("Text.ARequiredValueIsMissing"),
        2628 or 8152 => L.Message("Text.AnEnteredTextIsTooLong"),

        _ => L.Message("Text.UnexpectedDatabaseError")
    };
}

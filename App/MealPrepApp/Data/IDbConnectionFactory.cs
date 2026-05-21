using Microsoft.Data.SqlClient;

namespace MealPrepApp.Data;

/// <summary>Creates fresh connections to MealPrepDB. The app always connects as the
/// proc-only <c>mealprep_app</c> login — it can EXECUTE procedures but cannot touch tables directly.</summary>
public interface IDbConnectionFactory
{
    SqlConnection Create();
}

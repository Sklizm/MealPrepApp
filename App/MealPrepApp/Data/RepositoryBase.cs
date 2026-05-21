using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace MealPrepApp.Data;

/// <summary>
/// Shared plumbing for every repository: opens a fresh connection per call, executes a
/// stored procedure through Dapper, and translates any <see cref="SqlException"/> into an
/// <see cref="AppDbException"/> carrying a friendly Romanian message.
/// The app only ever calls procedures — never touches tables directly.
/// </summary>
public abstract class RepositoryBase
{
    private readonly IDbConnectionFactory _factory;

    protected RepositoryBase(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Runs a proc that returns a result set; maps every row to <typeparamref name="T"/>.</summary>
    protected async Task<IReadOnlyList<T>> QueryProcAsync<T>(string proc, object? param = null)
    {
        try
        {
            await using var conn = _factory.Create();
            var rows = await conn.QueryAsync<T>(proc, param, commandType: CommandType.StoredProcedure);
            return rows.AsList();
        }
        catch (SqlException ex)
        {
            throw new AppDbException(ex);
        }
    }

    /// <summary>Runs a proc expected to return zero or one row.</summary>
    protected async Task<T?> QuerySingleOrDefaultProcAsync<T>(string proc, object? param = null)
    {
        try
        {
            await using var conn = _factory.Create();
            return await conn.QuerySingleOrDefaultAsync<T>(proc, param, commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex)
        {
            throw new AppDbException(ex);
        }
    }

    /// <summary>Runs a proc that always returns exactly one row (e.g. dashboard / stats).</summary>
    protected async Task<T> QuerySingleProcAsync<T>(string proc, object? param = null)
    {
        try
        {
            await using var conn = _factory.Create();
            return await conn.QuerySingleAsync<T>(proc, param, commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex)
        {
            throw new AppDbException(ex);
        }
    }

    /// <summary>Runs a proc that ends with a <c>SELECT @scalar</c> (e.g. a new identity id).</summary>
    protected async Task<T> ExecuteScalarProcAsync<T>(string proc, object? param = null)
    {
        try
        {
            await using var conn = _factory.Create();
            return (await conn.ExecuteScalarAsync<T>(proc, param, commandType: CommandType.StoredProcedure))!;
        }
        catch (SqlException ex)
        {
            throw new AppDbException(ex);
        }
    }

    /// <summary>Runs a proc with no result set (insert / update / delete).</summary>
    protected async Task ExecuteProcAsync(string proc, object? param = null)
    {
        try
        {
            await using var conn = _factory.Create();
            await conn.ExecuteAsync(proc, param, commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex)
        {
            throw new AppDbException(ex);
        }
    }

    /// <summary>Runs a proc that returns multiple result sets; <paramref name="readResults"/>
    /// reads them in order off the grid reader.</summary>
    protected async Task<TResult> QueryMultipleProcAsync<TResult>(
        string proc,
        object? param,
        Func<SqlMapper.GridReader, Task<TResult>> readResults)
    {
        try
        {
            await using var conn = _factory.Create();
            using var grid = await conn.QueryMultipleAsync(proc, param, commandType: CommandType.StoredProcedure);
            return await readResults(grid);
        }
        catch (SqlException ex)
        {
            throw new AppDbException(ex);
        }
    }
}

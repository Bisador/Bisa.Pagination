namespace Bisa.Pagination.Core;

/// <summary>
/// Database-independent logic for classic Offset/Limit pagination.
/// </summary>
public static class OffsetPaginator
{
    /// <summary>
    /// applies Skip/Take to the query without executing it (Deferred Execution),
    /// so that the user can continue the query with Select/Include and ...
    /// Best Practice Note: Be sure to set a constant OrderBy before calling this method
    /// (contains a unique key) applied to the query, otherwise page order in some
    /// Databases are not guaranteed.
    /// </summary>
    public static IQueryable<T> BuildQuery<T>(IQueryable<T> source, OffsetPageRequest request)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);
        return source.Skip(request.Skip).Take(request.PageSize);
    }
}

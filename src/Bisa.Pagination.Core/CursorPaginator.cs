namespace Bisa.Pagination.Core;

/// <summary>
/// Output of the "build query" phase of cursor pagination: an IQueryable<T> ready to run
/// (not yet implemented - user can continue with Select/Include before ToList/ToListAsync;
/// Requirement "to be part of the Expression"), along with the information after materialization
/// Required to create PageResult.
/// </summary>
public sealed class CursorQueryPlan<T>
{
    public IQueryable<T> Query { get; }
    internal IReadOnlyList<SortSpecification<T>> SortSpecs { get; }
    internal CursorPageRequest Request { get; }

    internal CursorQueryPlan(IQueryable<T> query, IReadOnlyList<SortSpecification<T>> sortSpecs,
        CursorPageRequest request)
    {
        Query = query;
        SortSpecs = sortSpecs;
        Request = request;
    }
}

/// <summary>
/// Database-independent logic for keyset pagination on any IQueryable<T>.
/// Support for:
/// - Composite Key to any number of fields.
/// - forward/backward movement (Forward/Backward = Backforwarding),
/// - Lazy execution so that the user can continue the query (Select and ...).
/// </summary>
public static class CursorPaginator
{
    /// <summary>
    /// Constructs the paginated query (filter + sort + take) but does not execute it.
    /// An extra row (PageSize + 1) is taken so that later HasNext/HasPrevious
    /// detected without an extra Count/Query (standard "look-ahead row" pattern).
    /// </summary>
    public static CursorQueryPlan<T> BuildQuery<T>(
        IQueryable<T> source,
        IReadOnlyList<SortSpecification<T>> sortSpecs,
        CursorPageRequest request,
        ICursorCodec cursorCodec)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sortSpecs);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(cursorCodec);

        if (sortSpecs.Count == 0)
            throw new ArgumentException("At least one SortSpecification (preferably with a unique key at the end) is required.",
                nameof(sortSpecs));

        if (request.Direction == PaginationDirection.Backward && request.Cursor is null)
            throw new ArgumentException(
                "Backward paging makes no sense without a Cursor (use Forward with Cursor=null for the first page).");

        var query = source;

        if (request.Cursor is not null)
        {
            var decoded = cursorCodec.TryDecode(request.Cursor);
            if (!decoded.IsSuccess)
            {
                throw decoded.Status switch
                {
                    CursorDecodeStatus.Tampered => new TamperedCursorException(),
                    CursorDecodeStatus.Expired => new ExpiredCursorException(),
                    _ => new InvalidCursorException()
                };
            }

            query = KeysetPredicateBuilder.ApplyFilter(query, sortSpecs, decoded.Position!, request.Direction);
        }

        // For Backward, the query order is reversed to get the closest rows "before the cursor";
        // After Materialize, we return to normal order (CursorResultBuilder).
        var effectiveSpecs = request.Direction == PaginationDirection.Backward
            ? sortSpecs.Select(s => s.WithDirection(Flip(s.Direction))).ToList()
            : sortSpecs;

        query = KeysetPredicateBuilder.ApplyOrder(query, effectiveSpecs);
        query = query.Take(request.PageSize + 1);

        return new CursorQueryPlan<T>(query, sortSpecs, request);
    }

    private static SortDirection Flip(SortDirection direction) =>
        direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
}
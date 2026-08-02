namespace Bisa.Pagination.Core;

/// <summary>
/// Creates the final PageResult from the materialized list.
/// This part is intentionally separated from IQueryable to work for both EF (Async) and Dapper (which itself
/// reads the result) to be usable.
/// </summary>
public static class PageResultBuilder
{
    public static PageResult<T> FromOffset<T>(IReadOnlyList<T> items, OffsetPageRequest request, long? totalCount)
    {
        var hasNext = totalCount.HasValue
            ? (long)request.PageNumber * request.PageSize < totalCount.Value
            : items.Count == request.PageSize; // Without Count, best guess possible.

        return new PageResult<T>(items)
        {
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            HasPreviousPage = request.PageNumber > 1,
            HasNextPage = hasNext
        };
    }

    /// <summary>
    /// <paramref name="fetchedRows"/> should be exactly the same as CursorQueryPlan.Query
    /// (read with Take(PageSize+1) and possibly reverse order for Backward).
    /// </summary>
    public static PageResult<T> FromCursor<T>(
        List<T> fetchedRows,
        CursorQueryPlan<T> plan,
        ICursorCodec cursorCodec,
        long? totalCount)
    {
        ArgumentNullException.ThrowIfNull(fetchedRows);
        var request = plan.Request;

        var hasExtraRow = fetchedRows.Count > request.PageSize;
        if (hasExtraRow)
            fetchedRows.RemoveAt(fetchedRows.Count - 1);

        var isBackward = request.Direction == PaginationDirection.Backward;
        if (isBackward)
            fetchedRows.Reverse(); // Return in natural order (same order as the original SortSpecs).

        bool hasNext = isBackward ? true : hasExtraRow;
        bool hasPrevious = isBackward ? hasExtraRow : request.Cursor is not null;

        string? nextCursor = null;
        string? previousCursor = null;

        if (fetchedRows.Count > 0)
        {
            var extractor = CursorPositionExtractor.CreateExtractor(plan.SortSpecs);

            if (hasNext)
                nextCursor = cursorCodec.Encode(extractor(fetchedRows[^1]));

            if (hasPrevious)
                previousCursor = cursorCodec.Encode(extractor(fetchedRows[0]));
        }
        else
        {
            // Empty Result: Neither the next cursor nor the previous one is meaningful.
            hasNext = false;
            hasPrevious = false;
        }

        return new PageResult<T>(fetchedRows)
        {
            TotalCount = totalCount,
            PageSize = request.PageSize,
            HasNextPage = hasNext,
            HasPreviousPage = hasPrevious,
            NextCursor = nextCursor,
            PreviousCursor = previousCursor
        };
    }
}
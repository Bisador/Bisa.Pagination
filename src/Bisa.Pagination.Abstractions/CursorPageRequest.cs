using Bisa.Pagination.Abstractions.Enums;

namespace Bisa.Pagination.Abstractions;

/// <summary>
/// Keyset Pagination request. Scalable for large datasets
/// (Unlike Offset, its performance does not decrease with increasing page number).
/// </summary>
public sealed class CursorPageRequest
{
    /// <summary>
    /// Encrypted/hashed cursor token indicating the position of the last item on the previous page.
    /// null means this is the first page (First Page).
    /// </summary>
    public string? Cursor { get; }

    /// <summary>The number of requested items.</summary>
    public int PageSize { get; }

    /// <summary>Movement direction (forward/backward).</summary>
    public PaginationDirection Direction { get; }

    public CountMode CountMode { get; }

    public long? ProvidedTotalCount { get; }

    public CursorPageRequest(string? cursor, int pageSize, PaginationDirection direction = PaginationDirection.Forward,
        CountMode countMode = CountMode.None, long? providedTotalCount = null)
    {
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be at least 1.");
        if (countMode == CountMode.Provided && providedTotalCount is null)
            throw new ArgumentException("You must provide providedTotalCount when CountMode is Provided.", nameof(providedTotalCount));

        Cursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor;
        PageSize = pageSize;
        Direction = direction;
        CountMode = countMode;
        ProvidedTotalCount = providedTotalCount;
    }

    public static CursorPageRequest Create(string? cursor, int pageSize, PaginationDirection direction = PaginationDirection.Forward,
        int maxPageSize = 200, int defaultPageSize = 20, CountMode countMode = CountMode.None, long? providedTotalCount = null)
    {
        if (pageSize < 1) pageSize = defaultPageSize;
        if (pageSize > maxPageSize) pageSize = maxPageSize;
        return new CursorPageRequest(cursor, pageSize, direction, countMode, providedTotalCount);
    }
}

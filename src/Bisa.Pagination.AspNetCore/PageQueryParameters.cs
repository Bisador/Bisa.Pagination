using Bisa.Pagination.Abstractions;
using Bisa.Pagination.Abstractions.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Bisa.Pagination.AspNetCore;

/// <summary>
/// pagination parameters Offset from QueryString; Example: ?pageNumber=2&pageSize=20
/// be used in the signature of the action as [FromQuery].
/// </summary>
public sealed class OffsetPageQueryParameters
{
    [FromQuery(Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    [FromQuery(Name = "pageSize")]
    public int PageSize { get; set; } = 0; // 0 means to use DefaultPageSize

    public OffsetPageRequest ToRequest(PaginationOptions options) =>
        OffsetPageRequest.Create(PageNumber, PageSize == 0 ? options.DefaultPageSize : PageSize,
            options.MaxPageSize, options.DefaultPageSize);
}

/// <summary>
/// Cursor paging parameters from QueryString; Example: ?cursor=xyz&pageSize=20&direction=backward
/// </summary>
public sealed class CursorPageQueryParameters
{
    [FromQuery(Name = "cursor")]
    public string? Cursor { get; set; }

    [FromQuery(Name = "pageSize")]
    public int PageSize { get; set; } = 0;

    [FromQuery(Name = "direction")]
    public string Direction { get; set; } = "forward";

    public CursorPageRequest ToRequest(PaginationOptions options)
    {
        var direction = Direction.Equals("backward", StringComparison.OrdinalIgnoreCase)
            ? PaginationDirection.Backward
            : PaginationDirection.Forward;

        return CursorPageRequest.Create(Cursor, PageSize == 0 ? options.DefaultPageSize : PageSize,
            direction, options.MaxPageSize, options.DefaultPageSize);
    }
}

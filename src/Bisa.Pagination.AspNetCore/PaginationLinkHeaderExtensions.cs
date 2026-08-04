using Bisa.Pagination.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Bisa.Pagination.AspNetCore;

/// <summary>
/// A common Best Practice: return paging information as a standard HTTP header
/// "Link" (RFC 5988) instead of putting it inside the response body (which could include the body with the result
/// main mix). Suitable for APIs that want the response body to contain only the data array.
/// </summary>
public static class PaginationLinkHeaderExtensions
{
    extension(HttpResponse response)
    {
        public void AppendCursorLinkHeader<T>(string baseUrl, PageResult<T> result)
        {
            var links = new List<string>();
            if (result.NextCursor is not null)
                links.Add($"<{baseUrl}?cursor={Uri.EscapeDataString(result.NextCursor)}&direction=forward>; rel=\"next\"");
            if (result.PreviousCursor is not null)
                links.Add($"<{baseUrl}?cursor={Uri.EscapeDataString(result.PreviousCursor)}&direction=backward>; rel=\"prev\"");

            if (links.Count > 0)
                response.Headers.Append("Link", string.Join(", ", links));

            AppendCommonHeaders(response, result);
        }

        public void AppendOffsetLinkHeader<T>(string baseUrl, PageResult<T> result)
        {
            var links = new List<string>();
            var pageNumber = result.PageNumber ?? 1;

            links.Add($"<{baseUrl}?pageNumber=1&pageSize={result.PageSize}>; rel=\"first\"");
            if (result.HasPreviousPage)
                links.Add($"<{baseUrl}?pageNumber={pageNumber - 1}&pageSize={result.PageSize}>; rel=\"prev\"");
            if (result.HasNextPage)
                links.Add($"<{baseUrl}?pageNumber={pageNumber + 1}&pageSize={result.PageSize}>; rel=\"next\"");
            if (result.TotalPages is { } totalPages)
                links.Add($"<{baseUrl}?pageNumber={totalPages}&pageSize={result.PageSize}>; rel=\"last\"");

            if (links.Count > 0)
                response.Headers.Append("Link", string.Join(", ", links));

            AppendCommonHeaders(response, result);
        }
    }

    private static void AppendCommonHeaders<T>(HttpResponse response, PageResult<T> result)
    {
        if (result.TotalCount is { } total)
            response.Headers.Append("X-Total-Count", total.ToString());
        response.Headers.Append("X-Page-Size", result.PageSize.ToString());
    }
}

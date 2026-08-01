namespace Bisa.Pagination.Abstractions;

/// <summary>
/// Marker contract implemented by every pagination request (offset or cursor based).
/// Providers (EFCore/Dapper) pattern-match on the concrete type to decide the strategy,
/// which is what enables combining both strategies behind one endpoint.
/// </summary>
public interface IPaginationRequest
{
    /// <summary>Number of items requested per page. Must be validated/clamped against <see cref="PaginationOptions"/>.</summary>
    int PageSize { get; }
}

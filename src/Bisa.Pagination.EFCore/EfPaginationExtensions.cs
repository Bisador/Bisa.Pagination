using System.Linq.Expressions;
using Bisa.Pagination.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Bisa.Pagination.EFCore;

/// <summary>
/// EF Core implementation of every pagination strategy declared in Bisa.Pagination.Abstractions.
/// </summary>
public static class EfPaginationExtensions
{
    /// <summary>Item 1 — classic offset (skip/take) pagination.</summary>
    public static async Task<OffsetPaginationResult<T>> ToOffsetPaginationAsync<T>(
        this IQueryable<T> source,
        OffsetPaginationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        var totalCount = await source.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await source
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new OffsetPaginationResult<T>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasNext = request.PageNumber < totalPages,
            HasPrevious = request.PageNumber > 1
        };
    }

    /// <summary>
    /// Item 5 — single entry point that dispatches to offset or cursor pagination based on the
    /// runtime type of <paramref name="request"/>, so a controller/service can accept either
    /// strategy through one <see cref="IPaginationRequest"/> parameter.
    /// </summary>
    public static Task<PaginationResult<T>> ToPaginationAsync<T, TKey>(
        this IQueryable<T> source,
        IPaginationRequest request,
        Expression<Func<T, TKey>> cursorKeySelector,
        CancellationToken cancellationToken = default)
        where TKey : IComparable<TKey>
    {
        return request switch
        {
            OffsetPaginationRequest offsetRequest => CastAsync(
                source.ToOffsetPaginationAsync(offsetRequest, cancellationToken)),
            _ => throw new NotSupportedException(
                $"Pagination request type '{request.GetType().Name}' is not supported.")
        };

        static async Task<PaginationResult<T>> CastAsync<TResult>(Task<TResult> task)
            where TResult : PaginationResult<T>
            => await task.ConfigureAwait(false);
    }
}
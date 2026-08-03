using System.Data;
using Bisa.Pagination.Abstractions;
using Bisa.Pagination.Abstractions.Enums;
using Bisa.Pagination.Abstractions.Exceptions;
using Bisa.Pagination.Core;
using Dapper;

namespace Bisa.Pagination.Dapper;

public static class DapperPaginationExtensions
{
    extension(IDbConnection connection)
    {
        /// <summary>
        /// Implement Offset pagination on Dapper.
        /// <paramref name="selectAndOrderBySql"/> must contain SELECT/FROM/WHERE (optional)/ORDER BY
        /// but without OFFSET/FETCH or LIMIT (this method adds that).
        /// <paramref name="countSql"/> is mandatory if request.CountMode == Compute.
        /// </summary>
        public async Task<PageResult<T>> QueryOffsetPageAsync<T>(string selectAndOrderBySql,
            OffsetPageRequest request,
            SqlDialect dialect,
            object? param = null,
            string? countSql = null,
            IDbTransaction? transaction = null,
            int? commandTimeout = null)
        {
            var dynamicParams = new DynamicParameters(param);
            dynamicParams.Add("bp_offset", request.Skip);
            dynamicParams.Add("bp_take", request.PageSize);

            var pagingClause = dialect switch
            {
                SqlDialect.SqlServer => "OFFSET @bp_offset ROWS FETCH NEXT @bp_take ROWS ONLY",
                SqlDialect.PostgreSql or SqlDialect.MySql or SqlDialect.Sqlite => "LIMIT @bp_take OFFSET @bp_offset",
                _ => throw new NotSupportedException($"{dialect} is not supported.")
            };

            var finalSql = $"{selectAndOrderBySql}\n{pagingClause}";
            var items = (await connection.QueryAsync<T>(new CommandDefinition(
                finalSql, dynamicParams, transaction, commandTimeout)).ConfigureAwait(false)).AsList();

            var totalCount = await ResolveCountAsync(connection, request.CountMode, request.ProvidedTotalCount,
                countSql, param, transaction, commandTimeout).ConfigureAwait(false);

            return PageResultBuilder.FromOffset(items, request, totalCount);
        }

        /// <summary>
        /// Implementation of cursor pagination (Keyset) on Dapper with full support of Composite Key and Backforwarding.
        /// <paramref name="selectSql"/> should only be SELECT/FROM/WHERE(optional, no ORDER BY/LIMIT);
        /// ORDER BY and Keyset condition and pagination are added by this method.
        /// </summary>
        public async Task<PageResult<T>> QueryCursorPageAsync<T>(string selectSql,
            IReadOnlyList<SortField> sortFields,
            CursorPageRequest request,
            ICursorCodec cursorCodec,
            SqlDialect dialect,
            object? param = null,
            string? tableAlias = null,
            string? countSql = null,
            IDbTransaction? transaction = null,
            int? commandTimeout = null)
        {
            if (request is { Direction: PaginationDirection.Backward, Cursor: null })
                throw new ArgumentException("Backward pagination doesn't make sense without a cursor.");

            CursorPosition? position = null;
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
                position = decoded.Position;
            }

            var fragment = KeysetSqlBuilder.Build(sortFields, position, request.Direction, tableAlias);

            var dynamicParams = new DynamicParameters(param);
            foreach (var kv in fragment.Parameters)
                dynamicParams.Add(kv.Key, kv.Value);
            dynamicParams.Add("bp_take", request.PageSize + 1);

            var hasExistingWhere = selectSql.Contains(" WHERE ", StringComparison.OrdinalIgnoreCase);
            var whereAddition = fragment.WhereClause is null
                ? ""
                : hasExistingWhere ? $" AND ({fragment.WhereClause})" : $" WHERE ({fragment.WhereClause})";

            var pagingClause = dialect switch
            {
                SqlDialect.SqlServer => $"ORDER BY {fragment.OrderByClause} OFFSET 0 ROWS FETCH NEXT @bp_take ROWS ONLY",
                SqlDialect.PostgreSql or SqlDialect.MySql or SqlDialect.Sqlite => $"ORDER BY {fragment.OrderByClause} LIMIT @bp_take",
                _ => throw new NotSupportedException($" {dialect} not supported.")
            };

            var finalSql = $"{selectSql}{whereAddition}\n{pagingClause}";
            var fetched = (await connection.QueryAsync<T>(new CommandDefinition(
                finalSql, dynamicParams, transaction, commandTimeout)).ConfigureAwait(false)).AsList();

            long? totalCount = await ResolveCountAsync(connection, request.CountMode, request.ProvidedTotalCount,
                countSql, param, transaction, commandTimeout).ConfigureAwait(false);

            return BuildCursorResult(fetched, sortFields, request, cursorCodec, totalCount);
        }
    }

    private static async Task<long?> ResolveCountAsync(
        IDbConnection connection, CountMode mode, long? provided, string? countSql,
        object? param, IDbTransaction? transaction, int? commandTimeout) => mode switch
    {
        CountMode.None => null,
        CountMode.Provided => provided,
        CountMode.Compute => await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                countSql ?? throw new InvalidOperationException("countSql is mandatory when CountMode=Compute."),
                param, transaction, commandTimeout)).ConfigureAwait(false),
        _ => null
    };

    private static PageResult<T> BuildCursorResult<T>(
        List<T> fetchedRows, IReadOnlyList<SortField> sortFields, CursorPageRequest request,
        ICursorCodec cursorCodec, long? totalCount)
    {
        var hasExtraRow = fetchedRows.Count > request.PageSize;
        if (hasExtraRow)
            fetchedRows.RemoveAt(fetchedRows.Count - 1);

        var isBackward = request.Direction == PaginationDirection.Backward;
        if (isBackward)
            fetchedRows.Reverse();

        var hasNext = isBackward || hasExtraRow;
        var hasPrevious = isBackward ? hasExtraRow : request.Cursor is not null;

        string? nextCursor = null;
        string? previousCursor = null;

        if (fetchedRows.Count > 0)
        {
            if (hasNext)
                nextCursor = cursorCodec.Encode(DapperCursorPositionExtractor.Extract(fetchedRows[^1], sortFields));
            if (hasPrevious)
                previousCursor = cursorCodec.Encode(DapperCursorPositionExtractor.Extract(fetchedRows[0], sortFields));
        }
        else
        {
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

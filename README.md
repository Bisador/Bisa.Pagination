# Bisa.Pagination

A layered pagination library for **.NET 8** that supports both classic **Offset Pagination** and scalable **Cursor/Keyset Pagination**, including **Composite Keys**, **Backward Pagination**, and **cursor signing/protection**.

The library works with both **EF Core** and **Dapper**.

---

## 1. Architecture

The library is split into several projects, each with a clearly defined responsibility:

| Project                        | Responsibility                                                                                               | Dependencies  |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------ | ------------- |
| `Bisa.Pagination.Abstractions` | Contracts and models only (`Request` / `Result` / `Exception` / `Interface`)                                 | None          |
| `Bisa.Pagination.Core`         | Core pagination algorithms (Offset, Keyset with Expression Trees, default cursor codec) over `IQueryable<T>` | Abstractions  |
| `Bisa.Pagination.EF`           | EF Core async implementations using real `ToListAsync` / `CountAsync`                                        | Core, EF Core |
| `Bisa.Pagination.Dapper`       | SQL-based pagination implementation for Dapper                                                               | Core, Dapper  |
| `Bisa.Pagination.AspNetCore`   | Query-string binding, DI registration, and standard `Link` headers                                           | Core          |

### Why this separation matters

* A pure Domain/Application layer following **Clean Architecture** can reference only `Abstractions` and `Core` without knowing anything about EF Core or Dapper.
* If you use **EF Core for writes** and **Dapper for read-heavy workloads or reporting**, both providers share the same `PageResult<T>`, `SortField` / `CursorPosition` models, and cursor security contract.
* This provides consistent pagination behavior across different data-access technologies.
* Each layer can be tested independently. For example, `Core.Tests` runs against **LINQ-to-Objects** without requiring a real database.

---

## 2. Offset Pagination

Offset pagination is the traditional page-number-based approach using `Skip` and `Take`.

```csharp
using Bisa.Pagination.Abstractions;
using Bisa.Pagination.EF;

var request = OffsetPageRequest.Create(
    pageNumber: 2,
    pageSize: 20,
    maxPageSize: 100);

var result = await db.Products
    .Where(p => p.IsActive)
    .OrderBy(p => p.CreatedAt)
    .ThenBy(p => p.Id) // Best practice: always use a stable ordering
    .ToOffsetPageResultAsync(request);

// result.Items
// result.TotalCount
// result.TotalPages
// result.HasNextPage
// ...
```

### Deferred execution

`ApplyOffsetPagination` only adds `Skip` / `Take` to the `IQueryable`. It does **not** execute the query.

This means you can continue composing the query afterward:

```csharp
var dtoQuery = db.Products
    .OrderBy(p => p.Id)
    .ApplyOffsetPagination(request) // Still IQueryable; nothing executed yet
    .Select(p => new ProductDto(p.Id, p.Name));

var items = await dtoQuery.ToListAsync();
```

This is important when pagination needs to remain part of the final query expression.

---

## 3. Cursor / Keyset Pagination with Composite Keys

Cursor pagination is based on the position of the last item rather than a page number.

```csharp
using Bisa.Pagination.Core;

var sortSpecs = new List<SortSpecification<Product>>
{
    new(p => p.CreatedAt, SortDirection.Descending),
    new(p => p.Id, SortDirection.Ascending) // Unique tie-breaker should be the final key
};

var request = new CursorPageRequest(
    cursor: incomingCursorFromClient,
    pageSize: 20);

var result = await db.Products
    .ToCursorPageResultAsync(sortSpecs, request, cursorCodec);

// Return result.NextCursor / result.PreviousCursor to the client
```

### Why is a Composite Key necessary?

Suppose you sort only by `CreatedAt`, and multiple rows have the same timestamp.

This is known as **duplicate sort values**.

Without a unique tie-breaker, records may be skipped or returned more than once when navigating between pages.

Adding a unique key such as `Id` as the final sort key solves this problem.

The library builds the keyset predicate according to the standard lexicographical comparison pattern:

```text
(CreatedAt < v1)
OR (CreatedAt = v1 AND Id > v2)
```

The exact comparison operators depend on the configured sort directions.

### Backward Pagination

You can navigate backward using the previous cursor:

```csharp
var backRequest = new CursorPageRequest(
    previousCursor,
    pageSize: 20,
    PaginationDirection.Backward);

var previousPage = await db.Products
    .ToCursorPageResultAsync(
        sortSpecs,
        backRequest,
        cursorCodec);
```

Internally, backward pagination reverses the sorting direction and comparison operators to retrieve the closest records before the cursor.

After execution, the result is reversed back to the natural ordering so that the client receives results in a consistent order regardless of navigation direction.

### Null values in sort keys

The library follows a fixed and documented contract:

> **NULL is treated as the smallest value**, similar to `NULLS FIRST`.

If a sort column can contain `NULL`, consider the implications carefully when designing the sort order. This behavior should also be documented for consumers of your API.

---

## 4. Cursor Protection and Signing

`PaginationOptions.CursorProtection` supports two modes:

* `EncodingOnly`: Base64Url encoding only. The cursor is not directly readable, but it can still be modified by the client.
* `HashSigned` **(recommended)**: Base64Url encoding combined with an **HMAC-SHA256** signature using a secret key.

If the client modifies a signed cursor, `TryDecode` returns a `Tampered` result.

```csharp
services.AddBisaPagination(options =>
{
    options.CursorProtection = CursorProtection.HashSigned;

    options.CursorSigningKey =
        Convert.FromBase64String(
            configuration["Pagination:CursorKey"]!);

    options.CursorTimeToLive = TimeSpan.FromHours(6);
});
```

### Cursor validation exceptions

The library provides dedicated exceptions for different cursor validation failures:

| Scenario                            | Exception                 | Recommended HTTP Status |
| ----------------------------------- | ------------------------- | ----------------------: |
| Malformed or invalid cursor         | `InvalidCursorException`  |                     400 |
| Invalid signature / tampered cursor | `TamperedCursorException` |                     400 |
| Cursor has expired                  | `ExpiredCursorException`  |                     400 |

`Bisa.Pagination.AspNetCore` provides a helper:

```csharp
PaginationExceptionResults.ToProblemResult()
```

which converts these exceptions into standard ASP.NET Core `ProblemDetails` responses with HTTP 400.

---

## 5. Controlling Count Queries

All three request models:

* `OffsetPageRequest`
* `CursorPageRequest`
* `HybridPageRequest`

support a `CountMode`:

```csharp
public enum CountMode
{
    None,
    Compute,
    Provided
}
```

### `None`

No count query is executed.

This is the fastest option when the client does not need total-count information.

### `Compute`

The library calculates the total count using `CountAsync` / `SELECT COUNT(*)`.

### `Provided`

The caller supplies a previously calculated count, for example from Redis.

No additional count query is executed.

```csharp
var request = new OffsetPageRequest(
    pageNumber,
    pageSize,
    CountMode.Provided,
    providedTotalCount: cachedCount);
```

This can be particularly useful when the total count is expensive to calculate and can be cached separately.

---

## 6. Hybrid Pagination

`HybridPageRequest` is useful when the UI needs to display a page number while the backend still uses **Keyset Pagination** for efficient data retrieval.

It carries a display-only `PageNumber`, while the cursor remains the source of truth for positioning.

```csharp
var hybrid = new HybridPageRequest(
    cursor,
    pageSize: 20,
    clientAssumedPageNumber: 3);

var cursorResult = await db.Products
    .ToCursorPageResultAsync(
        sortSpecs,
        hybrid.ToCursorPageRequest(),
        codec);
```

The page number should therefore be treated as **informational rather than authoritative**.

---

## 7. Dapper Support

Dapper does not operate on `IQueryable` or Expression Trees, so the Dapper implementation works directly with SQL.

```csharp
using Bisa.Pagination.Dapper;
using Bisa.Pagination.Abstractions;

var sortFields = new[]
{
    new SortField("CreatedAt", SortDirection.Descending),
    new SortField("Id", SortDirection.Ascending)
};

var result = await connection.QueryCursorPageAsync<ProductRow>(
    selectSql:
        "SELECT Id, Name, CreatedAt FROM Products WHERE IsActive = 1",

    sortFields: sortFields,

    request: new CursorPageRequest(
        cursor,
        pageSize: 20),

    cursorCodec: codec,

    dialect: SqlDialect.SqlServer,

    countSql:
        "SELECT COUNT(*) FROM Products WHERE IsActive = 1"
    // Used only when CountMode = Compute
);
```

The library automatically builds and appends the required parameterized:

* `WHERE` predicates
* `ORDER BY`
* pagination clauses such as `OFFSET/FETCH` or `LIMIT`

based on the selected `SqlDialect`.

You only need to provide the base `SELECT` query.

---

## 8. ASP.NET Core Integration

Register the pagination services in `Program.cs`:

```csharp
builder.Services.AddBisaPagination(o =>
{
    o.CursorSigningKey = ...;
});
```

Then use the pagination models directly in a Controller or Minimal API:

```csharp
app.MapGet("/products", async (
    [AsParameters] CursorPageQueryParameters query,
    PaginationOptions options,
    ICursorCodec codec,
    AppDbContext db) =>
{
    try
    {
        var request = query.ToRequest(options);

        var sortSpecs = new List<SortSpecification<Product>>
        {
            new(p => p.Id)
        };

        var result = await db.Products
            .ToCursorPageResultAsync(
                sortSpecs,
                request,
                codec);

        return Results.Ok(result);
    }
    catch (PaginationException ex)
    {
        return ex.ToProblemResult();
    }
});
```

---

## 9. Implemented Best Practices

The library incorporates several pagination best practices:

* **Look-ahead row**
  To determine `HasNextPage` without an additional query, the library requests `PageSize + 1` rows and removes the extra row before returning the result.

* **Maximum page size (`MaxPageSize`)**
  Prevents clients from requesting excessively large pages such as `pageSize=1000000`, which could put significant pressure on the database.

* **Deferred execution**
  Build/Apply methods do not execute the query immediately. This allows callers to continue composing the query or combine it with operations such as `.AsNoTracking()`.

* **Unique final key in composite ordering**
  A unique tie-breaker at the end of the sort specification prevents missing or duplicated records when multiple rows have identical values for earlier sort keys.

* **Safe cursor deserialization**
  `DefaultCursorCodec` uses a closed allow-list of supported primitive types such as strings, numbers, dates, and `Guid` rather than allowing arbitrary type deserialization. This helps prevent unsafe deserialization scenarios.

* **Constant-time signature comparison**
  `FixedTimeEquals` is used when comparing signatures to reduce the risk of timing attacks.

* **Framework-independent Core layer**
  The core pagination algorithms are isolated from EF Core, Dapper, and ASP.NET Core, making them easier to test and reuse.

---

## 10. Known Limitations

### Guid ordering

`Guid` does not have native SQL `>` / `<` operators in the same way numeric or date types do.

The library uses `CompareTo` where necessary, but SQL translation ultimately depends on the underlying database provider.

For primary pagination keys, **numeric, date/time, or string columns are generally preferred**.

### NULL ordering

NULL handling follows a fixed contract:

> **NULL is treated as the smallest value (`NULLS FIRST`).**

This behavior is currently not configurable per query.

---

## 11. Tests

### `Bisa.Pagination.Core.Tests`

Runs against **LINQ-to-Objects**, without requiring a database.

The test suite covers:

* Forward pagination
* Backward pagination
* First page
* Last page
* Last page with one extra item
* Empty results
* Duplicate sort values
* Null values
* Invalid cursors
* Tampered cursors
* Expired cursors
* Backward pagination without a cursor

### `Bisa.Pagination.EF.Tests`

Runs against **EF Core InMemory** and covers:

* Offset pagination with and without count
* Forward → Backward navigation
* Last page scenarios

Run the complete test suite with:

```bash
dotnet test
```

> This solution was originally developed in an environment without the .NET SDK available for local verification. Before using it in a production project, please run `dotnet build` and `dotnet test` to verify the solution against your target .NET environment.

namespace Bisa.Pagination.Dapper;

/// <summary>
/// لهجه SQL هدف، چون سینتکس Paging بین دیتابیس‌ها متفاوت است
/// (SQL Server از OFFSET/FETCH و بقیه معمولاً از LIMIT/OFFSET استفاده می‌کنند).
/// </summary>
public enum SqlDialect
{
    SqlServer,
    PostgreSql,
    MySql,
    Sqlite
}

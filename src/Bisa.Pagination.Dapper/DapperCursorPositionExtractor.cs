using System.Reflection;
using Bisa.Pagination.Abstractions;

namespace Bisa.Pagination.Dapper;

internal static class DapperCursorPositionExtractor
{
    public static CursorPosition Extract<T>(T item, IReadOnlyList<SortField> sortFields)
    {
        var type = typeof(T);
        var keys = new List<CursorKeyValue>(sortFields.Count);

        foreach (var field in sortFields)
        {
            var prop = type.GetProperty(field.Name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    $"Property '{field.Name}' was not found on type '{type.Name}'. The SortField name must be exactly the same as the Property name of the DTO model.");

            var value = prop.GetValue(item);
            keys.Add(new CursorKeyValue(field.Name, value, prop.PropertyType.FullName ?? prop.PropertyType.Name));
        }

        return new CursorPosition(keys, DateTimeOffset.UtcNow);
    }
}

namespace Bisa.Pagination.Abstractions;

/// <summary>
/// یک مقدار از کلید مرتب‌سازی composite که در کرسر ذخیره می‌شود.
/// </summary>
/// <param name="Name">Property name (Should be match with SortField.Name).</param>
/// <param name="Value"> value in the last record of the page (boxed).</param>
/// <param name="TypeName">fullname of CLR for safe serialize/deserialize  (like System.DateTime).</param>
public sealed record CursorKeyValue(string Name, object? Value, string TypeName);
 
public sealed class CursorPosition
{
    public IReadOnlyList<CursorKeyValue> Keys { get; }

    public DateTimeOffset IssuedAtUtc { get; }

    public CursorPosition(IReadOnlyList<CursorKeyValue> keys, DateTimeOffset issuedAtUtc)
    {
        if (keys is null || keys.Count == 0)
            throw new ArgumentException("The cursor position must have at least one key.", nameof(keys));
        Keys = keys;
        IssuedAtUtc = issuedAtUtc;
    }
}

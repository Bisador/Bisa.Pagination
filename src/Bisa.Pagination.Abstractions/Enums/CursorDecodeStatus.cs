namespace Bisa.Pagination.Abstractions.Enums;

/// <summary>
/// The result of trying to decode a cursor.
/// </summary>
public enum CursorDecodeStatus
{
    Success = 0,
    Invalid = 1,
    Tampered = 2,
    Expired = 3
}
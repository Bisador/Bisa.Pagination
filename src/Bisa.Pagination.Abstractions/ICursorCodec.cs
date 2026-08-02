using Bisa.Pagination.Abstractions.Enums;

namespace Bisa.Pagination.Abstractions;

public readonly struct CursorDecodeResult
{
    public CursorDecodeStatus Status { get; }
    public CursorPosition? Position { get; }

    private CursorDecodeResult(CursorDecodeStatus status, CursorPosition? position)
    {
        Status = status;
        Position = position;
    }

    public static CursorDecodeResult Success(CursorPosition position) => new(CursorDecodeStatus.Success, position);
    public static CursorDecodeResult Invalid() => new(CursorDecodeStatus.Invalid, null);
    public static CursorDecodeResult Tampered() => new(CursorDecodeStatus.Tampered, null);
    public static CursorDecodeResult Expired() => new(CursorDecodeStatus.Expired, null);

    public bool IsSuccess => Status == CursorDecodeStatus.Success;
}

/// <summary>
/// Responsible for converting CursorPosition into a string that can be transferred in URL/Endpoint (Encode)
/// and vice versa (Decode), along with hash/signature support to prevent tampering
/// and expiration if needed.
/// This abstraction allows different implementations (plain Base64, Base64+HMAC, JWE, etc.)
/// be replaced without changes in Core/EF/Dapper.
/// </summary>
public interface ICursorCodec
{
    /// <summary>Converts the cursor position to a safe string token for use in a URL</summary>
    string Encode(CursorPosition position);

    /// <summary>
    /// Attempt to decode a token. never throws an exception; Always a result
    /// returns with the specified status (Success/Invalid/Tampered/Expired) to the higher layer
    /// can return a proper HTTP response (eg 400).
    /// </summary>
    CursorDecodeResult TryDecode(string token);
}

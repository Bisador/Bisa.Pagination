namespace Bisa.Pagination.Core;

/// <summary>
/// Simple factory to make valid PaginationOptions (generate a random signature key if not set
/// in the development environment). In Production it is recommended to inject the CursorSigningKey from the secure configuration
/// (See Bisa.Pagination.AspNetCore template for registration in DI).
/// </summary>
public static class PaginationOptionsFactory
{
    public static PaginationOptions CreateDefault(byte[]? cursorSigningKey = null)
    {
        var options = new PaginationOptions();
        options.CursorSigningKey = cursorSigningKey ?? GenerateRandomKey();
        return options;
    }

    /// <summary> Generates a 32-byte (256-bit) random secure key for HMAC-SHA256.</summary>
    public static byte[] GenerateRandomKey(int sizeInBytes = 32)
    {
        var bytes = new byte[sizeInBytes];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return bytes;
    }
}

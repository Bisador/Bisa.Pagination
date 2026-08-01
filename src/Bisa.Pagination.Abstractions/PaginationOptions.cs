namespace Bisa.Pagination.Abstractions;

/// <summary>
/// Global configuration for the library. Register via
/// <c>services.Configure&lt;PaginationOptions&gt;(...)</c> or the AddBisaPagination extension.
/// </summary>
public sealed class PaginationOptions
{
    public const string SectionName = "BisaPagination";

    public int DefaultPageSize { get; set; } = 20;

    public int MaxPageSize { get; set; } = 100;

    /// <summary>
    /// Secret key (min 32 bytes recommended) used by <see cref="Encoding.SignedCursorEncoder"/> (HMAC)
    /// and <see cref="Encoding.EncryptedCursorEncoder"/> (AES-256-GCM) to sign/encrypt cursor tokens.
    /// Never commit real secrets — load from user-secrets/Key Vault/environment variables.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;
}

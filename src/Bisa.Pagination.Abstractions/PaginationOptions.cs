using Bisa.Pagination.Abstractions.Enums;

namespace Bisa.Pagination.Abstractions;

/// <summary>
/// Library global settings that are usually registered once in DI.
/// </summary>
public sealed class PaginationOptions
{ 

    public int DefaultPageSize { get; set; } = 20;

    /// <summary>Page size cap to prevent abuse/DoS with very large pageSize.</summary> 
    public int MaxPageSize { get; set; } = 100;

    public CursorProtection CursorProtection { get; set; } = CursorProtection.HashSigned;
    
    /// <summary>
    /// Secret key for HMAC signature of cursors. In the Production environment, it must be done through
    /// User Secrets / Key Vault / environment variable to be injected, not hardcoded.
    /// </summary>
    public byte[] CursorSigningKey { get; set; } = Array.Empty<byte>();
    
    /// <summary>
    /// Cursor validity time. null means no expiration. Recommended for public endpoints
    /// Set a reasonable interval (eg a few hours) to discard very old cursors.
    /// </summary>
    public TimeSpan? CursorTimeToLive { get; set; } = TimeSpan.FromHours(6); 
}
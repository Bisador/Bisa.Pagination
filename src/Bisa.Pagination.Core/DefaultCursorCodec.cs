using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bisa.Pagination.Core;

/// <summary>
/// default implementation of <see cref="ICursorCodec"/>:
/// - Encoding: Base64Url (URL-safe, no padding).
/// - Hashing/Signing: A signature if CursorProtection.HashSigned is enabled
/// HMAC-SHA256 is added to the cursor with the PaginationOptions.CursorSigningKey secret key
/// to detect any tampering on the client side.
/// - Expiration: older cursors will be reported Expired if CursorTimeToLive is set.
///
/// Security: Type values (TypeName) are only recreated from an allow-list of base types
/// To prevent deserialization attacks with arbitrary types.
/// </summary>
public sealed class DefaultCursorCodec : ICursorCodec
{
    private static readonly HashSet<string> AllowedTypeNames = new(StringComparer.Ordinal)
    {
        typeof(string).FullName!, typeof(bool).FullName!,
        typeof(byte).FullName!, typeof(short).FullName!, typeof(int).FullName!, typeof(long).FullName!,
        typeof(float).FullName!, typeof(double).FullName!, typeof(decimal).FullName!,
        typeof(Guid).FullName!, typeof(DateTime).FullName!, typeof(DateTimeOffset).FullName!,
        typeof(DateOnly).FullName!, typeof(TimeOnly).FullName!
    };

    private readonly PaginationOptions _options;

    public DefaultCursorCodec(PaginationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (_options.CursorProtection == CursorProtection.HashSigned && _options.CursorSigningKey.Length == 0)
            throw new InvalidOperationException(
                "PaginationOptions.CursorSigningKey must be set for CursorProtection.HashSigned (eg a random 32-byte key).");
    }

    public string Encode(CursorPosition position)
    {
        var dto = new CursorDto
        {
            IssuedAtUnix = position.IssuedAtUtc.ToUnixTimeSeconds(),
            Keys = position.Keys.Select(k => new CursorKeyDto
            {
                Name = k.Name,
                TypeName = k.TypeName,
                ValueJson = k.Value is null
                    ? null
                    : JsonSerializer.Serialize(k.Value, ResolveType(k.TypeName) ?? k.Value.GetType())
            }).ToList()
        };

        var payload = JsonSerializer.SerializeToUtf8Bytes(dto);

        if (_options.CursorProtection == CursorProtection.EncodingOnly)
            return Base64UrlEncode(payload);

        var signature = ComputeHmac(payload);
        return $"{Base64UrlEncode(payload)}.{Base64UrlEncode(signature)}";
    }

    public CursorDecodeResult TryDecode(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return CursorDecodeResult.Invalid();

        try
        {
            byte[] payload;

            if (_options.CursorProtection == CursorProtection.HashSigned)
            {
                var parts = token.Split('.');
                if (parts.Length != 2)
                    return CursorDecodeResult.Invalid();

                payload = Base64UrlDecode(parts[0]);
                var providedSignature = Base64UrlDecode(parts[1]);
                var expectedSignature = ComputeHmac(payload);

                if (!CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
                    return CursorDecodeResult.Tampered();
            }
            else
            {
                payload = Base64UrlDecode(token);
            }

            var dto = JsonSerializer.Deserialize<CursorDto>(payload);
            if (dto?.Keys is null || dto.Keys.Count == 0)
                return CursorDecodeResult.Invalid();

            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(dto.IssuedAtUnix);
            if (_options.CursorTimeToLive is { } ttl && DateTimeOffset.UtcNow - issuedAt > ttl)
                return CursorDecodeResult.Expired();

            var keys = new List<CursorKeyValue>(dto.Keys.Count);
            foreach (var k in dto.Keys)
            {
                if (k.Name is null || k.TypeName is null)
                    return CursorDecodeResult.Invalid();

                var type = ResolveType(k.TypeName);
                if (type is null)
                    return CursorDecodeResult.Invalid();

                var value = k.ValueJson is null ? null : JsonSerializer.Deserialize(k.ValueJson, type);
                keys.Add(new CursorKeyValue(k.Name, value, k.TypeName));
            }

            return CursorDecodeResult.Success(new CursorPosition(keys, issuedAt));
        }
        catch
        {
            // Any parse/format error is treated as an invalid cursor, not an exception thrown.
            return CursorDecodeResult.Invalid();
        }
    }

    private static Type? ResolveType(string typeName) =>
        AllowedTypeNames.Contains(typeName) ? Type.GetType(typeName) : null;

    private byte[] ComputeHmac(byte[] payload)
    {
        using var hmac = new HMACSHA256(_options.CursorSigningKey);
        return hmac.ComputeHash(payload);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }

    private sealed class CursorDto
    {
        [JsonPropertyName("t")] public long IssuedAtUnix { get; set; }
        [JsonPropertyName("k")] public List<CursorKeyDto>? Keys { get; set; }
    }

    private sealed class CursorKeyDto
    {
        [JsonPropertyName("n")] public string? Name { get; set; }
        [JsonPropertyName("y")] public string? TypeName { get; set; }
        [JsonPropertyName("v")] public string? ValueJson { get; set; }
    }
}
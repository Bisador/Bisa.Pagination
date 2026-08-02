namespace Bisa.Pagination.Core;

/// <summary>
/// کارخانه ساده برای ساخت PaginationOptions معتبر (تولید کلید امضای تصادفی در صورت عدم تنظیم
/// در محیط توسعه). در Production توصیه می‌شود CursorSigningKey را از پیکربندی امن تزریق کنید
/// (به Bisa.Pagination.AspNetCore برای الگوی ثبت در DI مراجعه کنید).
/// </summary>
public static class PaginationOptionsFactory
{
    public static PaginationOptions CreateDefault(byte[]? cursorSigningKey = null)
    {
        var options = new PaginationOptions();
        options.CursorSigningKey = cursorSigningKey ?? GenerateRandomKey();
        return options;
    }

    /// <summary>یک کلید ۳۲ بایتی (۲۵۶ بیتی) تصادفی امن برای HMAC-SHA256 تولید می‌کند.</summary>
    public static byte[] GenerateRandomKey(int sizeInBytes = 32)
    {
        var bytes = new byte[sizeInBytes];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return bytes;
    }
}

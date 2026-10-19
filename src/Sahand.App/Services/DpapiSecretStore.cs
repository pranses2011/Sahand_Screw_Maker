using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Sahand.App.Services;

/// <summary>
/// مخزن راز SMTP با DPAPI (CurrentUser) — فقط روی Windows کار می‌کند (سند ۵-۳).
/// </summary>
/// <remarks>
/// راز با DPAPI در فایل محلی رمزنگاری می‌شود؛ هرگز در تنظیمات متن ساده/پروژه/گزارش نمی‌نشیند.
/// خطای پلتفرم با پیام فارسی و بدون افشای راز گزارش می‌شود. این کلاس Windows-only است؛
/// برای آزمون روی Linux از پیاده‌سازی آزمایشی (InMemory) استفاده می‌شود.
/// </remarks>
public sealed class DpapiSecretStore : Sahand.Reporting.ISecretProvider
{
    private readonly string _secretPath;

    /// <summary>ساخت مخزن با مسیر فایل راز رمزنگاری‌شده.</summary>
    public DpapiSecretStore(string secretPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(secretPath);
        _secretPath = secretPath;
    }

    /// <inheritdoc/>
    public string? GetSmtpPassword()
    {
        try
        {
            if (!File.Exists(_secretPath)) return null;
            var encrypted = File.ReadAllBytes(_secretPath);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (PlatformNotSupportedException)
        {
            // DPAPI فقط روی Windows در دسترس است؛ در این محیط پیکربندی ارسال ممکن نیست.
            return null;
        }
        catch (CryptographicException)
        {
            // فایل راز قابل رمزگشایی نیست (مثلاً کاربر/ماشین متفاوت) — مثل نبود راز رفتار می‌کنیم.
            return null;
        }
    }

    /// <inheritdoc/>
    public void StoreSmtpPassword(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser);
        var directory = Path.GetDirectoryName(_secretPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllBytes(_secretPath, encrypted);
    }

    /// <summary>حذف راز ذخیره‌شده (مثلاً هنگام پاک‌کردن تنظیمات SMTP).</summary>
    public void Clear()
    {
        try { File.Delete(_secretPath); } catch (IOException) { /* نادیده. */ }
    }
}

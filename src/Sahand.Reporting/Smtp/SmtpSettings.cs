namespace Sahand.Reporting.Smtp;

/// <summary>
/// تنظیمات SMTP برای ارسال گزارش — راز (رمز) هرگز در این ساختار ذخیره نمی‌شود.
/// </summary>
/// <remarks>
/// ذخیره‌سازی رمز فقط از طریق ISecretStore لایهٔ App (DPAPI/Credential Manager) مجاز است
/// (سند ۵-۳). TLS/STARTTLS الزامی و بدون امکان خاموش‌کردن است. «آزمون تنظیمات» واقعی
/// با اتصال EHLO انجام می‌شود و موفقیت کاذب ندارد (سند ۲-۷).
/// </remarks>
public sealed record SmtpSettings
{
    /// <summary>میزبان SMTP (مثل smtp.example.com).</summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>پورت — ۴۶۵ (Implicit TLS) یا ۵۸۷ (STARTTLS).</summary>
    public int Port { get; init; } = 587;

    /// <summary>نام نمایشی فرستنده.</summary>
    public string FromDisplayName { get; init; } = "کاربر طراح سهند";

    /// <summary>آدرس فرستنده (حساب مجاز کاربر).</summary>
    public string FromAddress { get; init; } = string.Empty;

    /// <summary>نام کاربری احراز (معمولاً همان ایمیل).</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>آدرس مقصد پیش‌فرض گزارش — نویسندهٔ برنامه (سند ۵-۷).</summary>
    public string DefaultToAddress { get; init; } = "Pranses2011@GMail.com";

    /// <summary>استفاده از STARTTLS روی پورت متن (۵۸۷) در برابر Implicit TLS (۴۶۵).</summary>
    public bool UseStartTls { get; init; } = true;

    /// <summary>آیا تنظیمات برای ارسال کامل است؟ (رمز جداگانه از SecretStore بررسی می‌شود.)</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        Port is > 0 and < 65536 &&
        !string.IsNullOrWhiteSpace(FromAddress) &&
        !string.IsNullOrWhiteSpace(Username) &&
        MailAddressValidator.IsValid(DefaultToAddress);
}

/// <summary>اعتبارسنجی سبک نشانی ایمیل — فقط شکل؛ بدون ارسال تأیید.</summary>
public static class MailAddressValidator
{
    /// <summary>شکل نشانی ایمیل معتبر است؟</summary>
    public static bool IsValid(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;
        if (address.Length > 254) return false;

        var at = address.IndexOf('@');
        return at > 0 && at < address.Length - 1 && address.LastIndexOf('@') == at &&
               !address.StartsWith('@') && !address.EndsWith('@') &&
               !address.Contains("..") && !address.Contains(' ');
    }
}

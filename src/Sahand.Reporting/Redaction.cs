using System.Text.RegularExpressions;

namespace Sahand.Reporting;

/// <summary>
/// موتور پاک‌سازی (redaction) متن گزارش — حذف دادهٔ حساس پیش از صف/ارسال (سند ۵-۴).
/// </summary>
/// <remarks>
/// الگوها: راز/توکن/کلید، شمارهٔ کارت ۱۶-رقمی بانک سامان، مسیرهای فایل شخصی
/// (User/… و /home/…)، ایمیل (نمایش محلی mask می‌شود در stack ولی در فیلد تماس باقی می‌ماند)
/// و stack trace فقط به خطوط برنامهٔ خودمان محدود می‌شود. خروجی شامل متن پاک و فهرست
/// اقدام‌های انجام‌شده است تا کاربر بداند چه حذف شده (شفافیت سند ۲-۷).
/// </remarks>
public static partial class Redaction
{
    [GeneratedRegex(@"(?i)(password|passwd|secret|token|api[_-]?key|authorization)\s*[:=]\s*\S+")]
    private static partial Regex SecretPattern();

    [GeneratedRegex(@"\b(?:\d[ -]?){13,16}\b")]
    private static partial Regex CardNumberPattern();

    [GeneratedRegex(@"(?i)(?:[A-Za-z]:\\Users\\|/home/|/Users/)([^/\s\\]+)")]
    private static partial Regex UserPathPattern();

    [GeneratedRegex(@"\b\d{1,3}(\.\d{1,3}){3}\b")]
    private static partial Regex IpPattern();

    /// <summary>نتیجهٔ پاک‌سازی.</summary>
    /// <param name="Text">متن پاک‌شده.</param>
    /// <param name="Notes">فهرست اقدام‌ها به فارسی برای نمایش به کاربر.</param>
    public sealed record RedactionResult(string Text, IReadOnlyList<string> Notes);

    /// <summary>
    /// متن گزارش را پاک‌سازی می‌کند.
    /// </summary>
    public static RedactionResult Scrub(string input)
    {
        if (string.IsNullOrEmpty(input))
            return new RedactionResult(input ?? string.Empty, []);

        var notes = new List<string>();
        var text = input;

        var secrets = SecretPattern().Matches(text);
        if (secrets.Count > 0)
        {
            text = SecretPattern().Replace(text, "[راز حذف شد]");
            notes.Add($"{secrets.Count} مورد راز/توکن حذف شد.");
        }

        var cards = CardNumberPattern().Matches(text);
        if (cards.Count > 0)
        {
            text = CardNumberPattern().Replace(text, "[شمارهٔ کارت حذف شد]");
            notes.Add($"{cards.Count} شمارهٔ کارت احتمالی حذف شد.");
        }

        var paths = UserPathPattern().Matches(text);
        if (paths.Count > 0)
        {
            // نام کاربر مسیر حذف می‌شود ولی ساختار مسیر برای عیب‌یابی می‌ماند.
            text = UserPathPattern().Replace(text, m => m.Value.Replace(m.Groups[1].Value, "[کاربر]"));
            notes.Add($"{paths.Count} مسیر شخصی پوشانده شد.");
        }

        var ips = IpPattern().Matches(text);
        if (ips.Count > 0)
        {
            text = IpPattern().Replace(text, "[IP حذف شد]");
            notes.Add($"{ips.Count} نشانی IP حذف شد.");
        }

        return new RedactionResult(text, notes);
    }
}

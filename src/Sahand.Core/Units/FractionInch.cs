using System.Globalization;

namespace Sahand.Core.Units;

/// <summary>
/// تجزیهٔ ورودی اینچی به‌صورت اعشاری یا کسر (مثل «1/2» یا «1 1/4»).
/// </summary>
/// <remarks>
/// سند ۱-۶ امکان ورود کسر اینچ را خواسته است؛ این کلاس فقط لایهٔ ورودی است و
/// خروجی همیشه به میلی‌متر (واحد پایه) تبدیل می‌شود تا مقدار واقعی حفظ شود.
/// جداکنندهٔ اعشار، نقطه است؛ ممیز فارسی «٫» هم پذیرفته می‌شود تا تجربهٔ فارسی کامل باشد.
/// </remarks>
public static class FractionInch
{
    /// <summary>
    /// رشتهٔ ورودی اینچی را به میلی‌متر تبدیل می‌کند.
    /// </summary>
    /// <param name="text">ورودی کاربر: «.75»، «0.75»، «3/4»، «1 1/2» یا «۱٫۲۵».</param>
    /// <returns>مقدار برحسب میلی‌متر با ضریب دقیق ۲۵٫۴.</returns>
    /// <exception cref="FormatException">اگر الگوی ورودی قابل تفسیر نباشد.</exception>
    public static double ParseToMillimeters(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // نرمال‌سازی: ارقام فارسی/عربی و ممیز فارسی به معادل لاتین (GEO-08).
        var normalized = PersianDigits.NormalizeDigits(text).Trim().Replace('٫', '.');

        // حالت ۱: کسر خالص مثل «3/4»
        if (normalized.Contains('/'))
        {
            var parts = normalized.Split('/', StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                throw new FormatException("الگوی کسر معتبر نیست. نمونهٔ درست: «3/4».");

            var wholePart = 0.0;
            var numeratorText = parts[0];

            // حالت ۲: عدد کامل + کسر مثل «1 1/2»
            var spaceIndex = parts[0].LastIndexOf(' ');
            if (spaceIndex >= 0)
            {
                wholePart = ParseDecimal(parts[0][..spaceIndex]);
                numeratorText = parts[0][(spaceIndex + 1)..];
            }

            var numerator = ParseDecimal(numeratorText);
            var denominator = ParseDecimal(parts[1]);
            if (denominator == 0)
                throw new FormatException("مخرج کسر نمی‌تواند صفر باشد.");

            return (wholePart + numerator / denominator) * LengthUnitConverter.MillimetersPerInch;
        }

        // حالت ۳: عدد اعشاری ساده
        return ParseDecimal(normalized) * LengthUnitConverter.MillimetersPerInch;
    }

    /// <summary>
    /// عدد اعشاری را با نقطهٔ اعشار لاتین تجزیه می‌کند.
    /// </summary>
    /// <exception cref="FormatException">اگر رشته عدد معتبر نباشد.</exception>
    private static double ParseDecimal(string text)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"«{text}» عدد معتبری نیست.");

        // رد مقادیر غیرمجاز ریاضی پیش از ورود به موتور هندسه (GEO-07).
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new FormatException("مقدار عددی نامعتبر است (NaN یا بی‌نهایت).");

        return value;
    }
}

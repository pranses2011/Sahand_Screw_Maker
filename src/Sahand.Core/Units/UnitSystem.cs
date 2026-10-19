namespace Sahand.Core.Units;

/// <summary>
/// واحدهای طول پشتیبانی‌شده در ورودی/نمایش کاربر.
/// </summary>
/// <remarks>
/// واحد پایهٔ تمام محاسبات هندسی و ذخیره‌سازی «میلی‌متر» است؛ اینچ فقط در لایهٔ ورودی/نمایش
/// معنا دارد و تبدیل باید در مرز ورودی انجام و نتیجهٔ آن به کاربر اعلام شود (سند ۳-۱).
/// </remarks>
public enum LengthUnit
{
    /// <summary>میلی‌متر — واحد پایهٔ مدل.</summary>
    Millimeter,

    /// <summary>اینچ — فقط لایهٔ نمایش/ورودی.</summary>
    Inch,
}

/// <summary>
/// ابزار تبدیل واحد طول با ضریب دقیق و بدون گردکردن بی‌صدا.
/// </summary>
/// <remarks>
/// ضریب اینچ به میلی‌متر مطابق تعریف بین‌المللی (۱ اینچ = ۲۵٫۴ میلی‌متر دقیق) است؛
/// گردکردن فقط در لایهٔ نمایش با شمارش رقم اعشارِ درخواستی کاربر انجام می‌شود.
/// </remarks>
public static class LengthUnitConverter
{
    /// <summary>ضریب دقیق تبدیل اینچ به میلی‌متر (۲۵٫۴).</summary>
    public const double MillimetersPerInch = 25.4;

    /// <summary>
    /// مقدار را از واحد ورودی به میلی‌متر (واحد پایه) تبدیل می‌کند.
    /// </summary>
    /// <param name="value">مقدار عددی در واحد <paramref name="unit"/>.</param>
    /// <param name="unit">واحد مقدار ورودی.</param>
    /// <returns>مقدار معادل برحسب میلی‌متر؛ بدون هیچ گردکردن.</returns>
    public static double ToMillimeters(double value, LengthUnit unit) => unit switch
    {
        LengthUnit.Millimeter => value,
        LengthUnit.Inch => value * MillimetersPerInch,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "واحد طول پشتیبانی نمی‌شود."),
    };

    /// <summary>
    /// مقدار میلی‌متری را به واحد مقصد تبدیل می‌کند.
    /// </summary>
    /// <param name="valueMm">مقدار برحسب میلی‌متر.</param>
    /// <param name="unit">واحد مقصد.</param>
    /// <returns>مقدار معادل در واحد مقصد؛ بدون گردکردن.</returns>
    public static double FromMillimeters(double valueMm, LengthUnit unit) => unit switch
    {
        LengthUnit.Millimeter => valueMm,
        LengthUnit.Inch => valueMm / MillimetersPerInch,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "واحد طول پشتیبانی نمی‌شود."),
    };

    /// <summary>
    /// قالب نمایش کوتاه واحد را برمی‌گرداند (برای برچسب کنار فیلد).
    /// </summary>
    public static string ShortLabel(LengthUnit unit) => unit switch
    {
        LengthUnit.Millimeter => "mm",
        LengthUnit.Inch => "in",
        _ => "?",
    };
}

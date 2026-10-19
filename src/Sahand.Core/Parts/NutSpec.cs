using Sahand.Core.Threads;

namespace Sahand.Core.Parts;

/// <summary>
/// مشخصات پارامتریک مهره — تمام ابعاد برحسب میلی‌متر.
/// </summary>
/// <remarks>
/// قرارداد محور: مهره روی محور Z با z=۰ روی سطح اتکای پایین و ارتفاع به سمت بالا.
/// رزوهٔ داخلی جدا از رزوهٔ قطعهٔ جفت تعریف می‌شود (سند ۳-۴)؛ حفرهٔ مدل‌شده با لقی چاپ یکی نیست.
/// </remarks>
public sealed record NutSpec
{
    /// <summary>نام قطعه برای نمایش/فایل پروژه.</summary>
    public string Name { get; init; } = "مهره";

    /// <summary>توضیح اختیاری کاربر.</summary>
    public string? Description { get; init; }

    /// <summary>سبک مهره.</summary>
    public NutStyle Style { get; init; } = NutStyle.Hex;

    /// <summary>رزوهٔ داخلی — باید با قطعهٔ جفت سازگار باشد (سازگارسنج جداگانه).</summary>
    public ThreadSpec Thread { get; init; } = new() { Kind = ThreadKind.Internal };

    /// <summary>عرض آچارخور (فاصلهٔ بین دو تخت مقابل) برحسب میلی‌متر.</summary>
    public double AcrossFlatsMm { get; init; }

    /// <summary>ارتفاع کل مهره برحسب میلی‌متر.</summary>
    public double HeightMm { get; init; }

    /// <summary>پخ ورودی حفرهٔ دو طرف برحسب میلی‌متر — کمک به شروع پیچ شدن و چاپ.</summary>
    public double ChamferMm { get; init; }
}

namespace Sahand.Core.Parts;

/// <summary>
/// مشخصات پارامتریک واشر تخت — بدون رزوه؛ تمام ابعاد برحسب میلی‌متر.
/// </summary>
/// <remarks>
/// مرز محصول (سند ۱-۴): واشر در UI به‌روشنی از «پیچ/مهره» تفکیک می‌شود و
/// ادعای انطباق استاندارد یا قابلیت باربری ندارد. قطر داخلی باید از قطر اسمی پیچ بزرگ‌تر باشد.
/// </remarks>
public sealed record WasherSpec
{
    /// <summary>نام قطعه برای نمایش/فایل پروژه.</summary>
    public string Name { get; init; } = "واشر";

    /// <summary>توضیح اختیاری کاربر.</summary>
    public string? Description { get; init; }

    /// <summary>سبک واشر.</summary>
    public WasherStyle Style { get; init; } = WasherStyle.FlatPlain;

    /// <summary>قطر داخلی (سوراخ) برحسب میلی‌متر — باید کمی بزرگ‌تر از قطر پیچ باشد.</summary>
    public double InnerDiameterMm { get; init; }

    /// <summary>قطر خارجی برحسب میلی‌متر.</summary>
    public double OuterDiameterMm { get; init; }

    /// <summary>ضخامت برحسب میلی‌متر.</summary>
    public double ThicknessMm { get; init; }

    /// <summary>پخ اختیاری لبه‌ها برحسب میلی‌متر (۰ = بدون پخ).</summary>
    public double EdgeChamferMm { get; init; }
}

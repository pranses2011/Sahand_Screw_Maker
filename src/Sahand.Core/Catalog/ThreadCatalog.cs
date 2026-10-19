using Sahand.Core.Threads;

namespace Sahand.Core.Catalog;

/// <summary>
/// یک مدخل کاتالوگ رزوه: اندازهٔ اسمی + گام در یک سری مشخص.
/// </summary>
/// <param name="SizeLabel">برچسب اندازه (مثل «M6» یا «1/4-20»).</param>
/// <param name="NominalDiameterMm">قطر اسمی برحسب میلی‌متر (برای اینچی، تبدیل دقیق).</param>
/// <param name="PitchMm">گام برحسب میلی‌متر.</param>
/// <param name="Tpi">تعداد رزوه در اینچ — فقط برای نمایش خانوادهٔ اینچی؛ null برای متریک.</param>
/// <param name="Series">نام سری (مثل «درشت» یا «Fine» یا «UNC»).</param>
/// <param name="SourceNote">یادداشت منبع داده — الزام ردیابی سند ۳-۶.</param>
public sealed record CatalogEntry(
    string SizeLabel,
    double NominalDiameterMm,
    double PitchMm,
    int? Tpi,
    string Series,
    string SourceNote);

/// <summary>
/// کاتالوگ رزوه‌های داخلی برنامه — دادهٔ عمومی و شناخته‌شدهٔ مهندسی با یادداشت منبع.
/// </summary>
/// <remarks>
/// مرز صداقت (سند ۳-۶): این مقادیر «بر پایهٔ» سری‌های شناخته‌شدهٔ ISO 261 و ASME B1.1 هستند و
/// جایگزین جدول رسمی استاندارد نیستند؛ برچسب منبع هر مدخل در UI و فایل پروژه حمل می‌شود.
/// افزودن مدخل جدید فقط از مسیر کاتالوگ کاربر (UserCatalog) با اعتبارسنجی schema انجام می‌شود.
/// </remarks>
public static class ThreadCatalog
{
    private const string MetricSource =
        "بر پایهٔ سری گام ISO 261 — دادهٔ عمومی مهندسی؛ برای انطباق رسمی به متن استاندارد مراجعه شود.";

    private const string UnifiedSource =
        "بر پایهٔ سری UNC (ASME B1.1) — دادهٔ عمومی مهندسی؛ برای انطباق رسمی به متن استاندارد مراجعه شود.";

    /// <summary>سری گام درشت متریک (پرکاربردترین).</summary>
    private static readonly List<CatalogEntry> MetricCoarse = new()
    {
        new("M1", 1.0, 0.25, null, "درشت", MetricSource),
        new("M1.2", 1.2, 0.25, null, "درشت", MetricSource),
        new("M1.6", 1.6, 0.35, null, "درشت", MetricSource),
        new("M2", 2.0, 0.4, null, "درشت", MetricSource),
        new("M2.5", 2.5, 0.45, null, "درشت", MetricSource),
        new("M3", 3.0, 0.5, null, "درشت", MetricSource),
        new("M4", 4.0, 0.7, null, "درشت", MetricSource),
        new("M5", 5.0, 0.8, null, "درشت", MetricSource),
        new("M6", 6.0, 1.0, null, "درشت", MetricSource),
        new("M8", 8.0, 1.25, null, "درشت", MetricSource),
        new("M10", 10.0, 1.5, null, "درشت", MetricSource),
        new("M12", 12.0, 1.75, null, "درشت", MetricSource),
        new("M14", 14.0, 2.0, null, "درشت", MetricSource),
        new("M16", 16.0, 2.0, null, "درشت", MetricSource),
        new("M18", 18.0, 2.5, null, "درشت", MetricSource),
        new("M20", 20.0, 2.5, null, "درشت", MetricSource),
        new("M22", 22.0, 2.5, null, "درشت", MetricSource),
        new("M24", 24.0, 3.0, null, "درشت", MetricSource),
        new("M27", 27.0, 3.0, null, "درشت", MetricSource),
        new("M30", 30.0, 3.5, null, "درشت", MetricSource),
        new("M33", 33.0, 3.5, null, "درشت", MetricSource),
        new("M36", 36.0, 4.0, null, "درشت", MetricSource),
        new("M39", 39.0, 4.0, null, "درشت", MetricSource),
        new("M42", 42.0, 4.5, null, "درشت", MetricSource),
        new("M45", 45.0, 4.5, null, "درشت", MetricSource),
        new("M48", 48.0, 5.0, null, "درشت", MetricSource),
    };

    /// <summary>سری گام ریز متریک — زیرمجموعهٔ پرکاربرد.</summary>
    private static readonly List<CatalogEntry> MetricFine = new()
    {
        new("M6", 6.0, 0.75, null, "ریز", MetricSource),
        new("M8", 8.0, 1.0, null, "ریز", MetricSource),
        new("M8", 8.0, 0.75, null, "ریز", MetricSource),
        new("M10", 10.0, 1.25, null, "ریز", MetricSource),
        new("M10", 10.0, 1.0, null, "ریز", MetricSource),
        new("M12", 12.0, 1.5, null, "ریز", MetricSource),
        new("M12", 12.0, 1.25, null, "ریز", MetricSource),
        new("M16", 16.0, 1.5, null, "ریز", MetricSource),
        new("M20", 20.0, 2.0, null, "ریز", MetricSource),
        new("M20", 20.0, 1.5, null, "ریز", MetricSource),
        new("M24", 24.0, 2.0, null, "ریز", MetricSource),
        new("M30", 30.0, 2.0, null, "ریز", MetricSource),
        new("M36", 36.0, 3.0, null, "ریز", MetricSource),
    };

    /// <summary>سری UNC درشت اینچی — تبدیل قطر با ضریب دقیق ۲۵٫۴.</summary>
    private static readonly List<CatalogEntry> UnifiedCoarse = new()
    {
        new("#2-56", 0.086 * 25.4, 25.4 / 56, 56, "UNC", UnifiedSource),
        new("#4-40", 0.112 * 25.4, 25.4 / 40, 40, "UNC", UnifiedSource),
        new("#6-32", 0.138 * 25.4, 25.4 / 32, 32, "UNC", UnifiedSource),
        new("#8-32", 0.164 * 25.4, 25.4 / 32, 32, "UNC", UnifiedSource),
        new("#10-24", 0.190 * 25.4, 25.4 / 24, 24, "UNC", UnifiedSource),
        new("#12-24", 0.216 * 25.4, 25.4 / 24, 24, "UNC", UnifiedSource),
        new("1/4-20", 0.250 * 25.4, 25.4 / 20, 20, "UNC", UnifiedSource),
        new("5/16-18", 0.3125 * 25.4, 25.4 / 18, 18, "UNC", UnifiedSource),
        new("3/8-16", 0.375 * 25.4, 25.4 / 16, 16, "UNC", UnifiedSource),
        new("7/16-14", 0.4375 * 25.4, 25.4 / 14, 14, "UNC", UnifiedSource),
        new("1/2-13", 0.500 * 25.4, 25.4 / 13, 13, "UNC", UnifiedSource),
        new("5/8-11", 0.625 * 25.4, 25.4 / 11, 11, "UNC", UnifiedSource),
        new("3/4-10", 0.750 * 25.4, 25.4 / 10, 10, "UNC", UnifiedSource),
        new("7/8-9", 0.875 * 25.4, 25.4 / 9, 9, "UNC", UnifiedSource),
        new("1-8", 1.000 * 25.4, 25.4 / 8, 8, "UNC", UnifiedSource),
    };

    /// <summary>
    /// مدخل‌های یک خانواده را برمی‌گرداند (کپی دفاعی تا کاتالوگ داخلی تغییرناپذیر بماند).
    /// </summary>
    public static IReadOnlyList<CatalogEntry> GetEntries(ThreadFamily family) => family switch
    {
        ThreadFamily.IsoMetric => MetricCoarse.Concat(MetricFine).ToList(),
        ThreadFamily.Unified => UnifiedCoarse.ToList(),
        _ => [], // خانواده‌های دیگر: «دادهٔ کاتالوگ در برنامهٔ توسعه» — پروفیل هندسی پشتیبانی می‌شود.
    };

    /// <summary>
    /// از روی برچسب اندازه و خانواده، مشخصات رزوهٔ خارجی می‌سازد.
    /// </summary>
    /// <returns>مشخصات آمادهٔ ساخت؛ null اگر مدخل یافت نشد.</returns>
    public static ThreadSpec? BuildSpec(ThreadFamily family, string sizeLabel, ThreadKind kind)
    {
        var entry = GetEntries(family).FirstOrDefault(
            e => string.Equals(e.SizeLabel, sizeLabel, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return null;

        return new ThreadSpec
        {
            Family = family,
            Kind = kind,
            NominalDiameterMm = entry.NominalDiameterMm,
            PitchMm = entry.PitchMm,
            Starts = 1,
            Handedness = ThreadHandedness.Right,
            HalfAngleDegrees = family is ThreadFamily.IsoMetric or ThreadFamily.Unified ? 30.0 : 0,
            StandardId = $"CAT-{entry.SizeLabel}-{entry.Series}",
            DataSourceNote = entry.SourceNote,
        };
    }
}

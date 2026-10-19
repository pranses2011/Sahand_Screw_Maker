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
/// مرز صداقت (سند ۳-۶): این مقادیر «بر پایهٔ» سری‌های شناخته‌شدهٔ ISO 261، ASME B1.1،
/// ISO 262 (BSW/BSF)، ASME B1.20.1 (NPT)، ISO 228 (BSPP)، ISO 2901 (Tr) و ASME B1.5 (ACME)
/// هستند و جایگزین جدول رسمی استاندارد نیستند؛ برچسب منبع هر مدخل در UI و فایل پروژه حمل می‌شود.
/// افزودن مدخل جدید فقط از مسیر کاتالوگ کاربر (UserCatalog) با اعتبارسنجی schema انجام می‌شود.
/// گسترش ۰٫۳٫۰: هفت خانوادهٔ کاتالوگی (متریک، یونیفاید UNC/UNF، ویتوورث BSW/BSF،
/// لوله‌ای NPT مخروطی و BSPP موازی، ذوزنقه‌ای Tr و ACME) از پروفیل‌های از پیش پشتیبانی‌شده
/// موتور هندسه پر شدند — NPT با مخروط ۱٫۷۸۹۹ درجه (۱:۱۶).
/// </remarks>
public static class ThreadCatalog
{
    private const string MetricSource =
        "بر پایهٔ سری گام ISO 261 — دادهٔ عمومی مهندسی؛ برای انطباق رسمی به متن استاندارد مراجعه شود.";

    private const string UnifiedSource =
        "بر پایهٔ سری UNC (ASME B1.1) — دادهٔ عمومی مهندسی؛ برای انطباق رسمی به متن استاندارد مراجعه شود.";

    private const string UnifiedFineSource =
        "بر پایهٔ سری UNF (ASME B1.1) — دادهٔ عمومی مهندسی؛ برای انطباق رسمی به متن استاندارد مراجعه شود.";

    private const string WhitworthSource =
        "بر پایهٔ سری BSW/BSF (ISO 262 / BS 84) — دادهٔ عمومی مهندسی؛ گردی تاج/ریشهٔ ۵۵ درجه با تقریب پخ نمایش داده می‌شود.";

    private const string NptSource =
        "بر پایهٔ NPT (ASME B1.20.1) — مخروط ۱:۱۶ (۱٫۷۸۹۹ درجه)؛ قطر، OD لولهٔ نامی است. برای آب‌بندی واقعی، مهرهٔ مخروطی/درزچسب لازم است.";

    private const string BspSource =
        "بر پایهٔ BSPP موازی (ISO 228 / DIN 259) — قطر، OD لولهٔ نامی است؛ برای آب‌بندی، واشر/رزوهٔ مخروطی BSPT لازم است.";

    private const string TrapezoidalSource =
        "بر پایهٔ سری Tr (ISO 2901 / DIN 103) — دادهٔ عمومی مهندسی؛ برای انطباق رسمی به متن استاندارد مراجعه شود.";

    private const string AcmeSource =
        "بر پایهٔ ACME عمومی (ASME B1.5) — دادهٔ عمومی مهندسی؛ برای انطباق رسمی به متن استاندارد مراجعه شود.";

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

    /// <summary>سری UNF ریز اینچی (گسترش ۰٫۳٫۰).</summary>
    private static readonly List<CatalogEntry> UnifiedFine = new()
    {
        new("#0-80", 0.060 * 25.4, 25.4 / 80, 80, "UNF", UnifiedFineSource),
        new("#2-64", 0.086 * 25.4, 25.4 / 64, 64, "UNF", UnifiedFineSource),
        new("#3-48", 0.099 * 25.4, 25.4 / 48, 48, "UNF", UnifiedFineSource),
        new("#4-48", 0.112 * 25.4, 25.4 / 48, 48, "UNF", UnifiedFineSource),
        new("#5-44", 0.125 * 25.4, 25.4 / 44, 44, "UNF", UnifiedFineSource),
        new("#6-40", 0.138 * 25.4, 25.4 / 40, 40, "UNF", UnifiedFineSource),
        new("#8-36", 0.164 * 25.4, 25.4 / 36, 36, "UNF", UnifiedFineSource),
        new("#10-32", 0.190 * 25.4, 25.4 / 32, 32, "UNF", UnifiedFineSource),
        new("#12-28", 0.216 * 25.4, 25.4 / 28, 28, "UNF", UnifiedFineSource),
        new("1/4-28", 0.250 * 25.4, 25.4 / 28, 28, "UNF", UnifiedFineSource),
        new("5/16-24", 0.3125 * 25.4, 25.4 / 24, 24, "UNF", UnifiedFineSource),
        new("3/8-24", 0.375 * 25.4, 25.4 / 24, 24, "UNF", UnifiedFineSource),
        new("7/16-20", 0.4375 * 25.4, 25.4 / 20, 20, "UNF", UnifiedFineSource),
        new("1/2-20", 0.500 * 25.4, 25.4 / 20, 20, "UNF", UnifiedFineSource),
        new("9/16-18", 0.5625 * 25.4, 25.4 / 18, 18, "UNF", UnifiedFineSource),
        new("5/8-18", 0.625 * 25.4, 25.4 / 18, 18, "UNF", UnifiedFineSource),
        new("3/4-16", 0.750 * 25.4, 25.4 / 16, 16, "UNF", UnifiedFineSource),
        new("7/8-14", 0.875 * 25.4, 25.4 / 14, 14, "UNF", UnifiedFineSource),
        new("1-12", 1.000 * 25.4, 25.4 / 12, 12, "UNF", UnifiedFineSource),
        new("1-1/8-12", 1.125 * 25.4, 25.4 / 12, 12, "UNF", UnifiedFineSource),
        new("1-1/4-12", 1.250 * 25.4, 25.4 / 12, 12, "UNF", UnifiedFineSource),
        new("1-3/8-12", 1.375 * 25.4, 25.4 / 12, 12, "UNF", UnifiedFineSource),
        new("1-1/2-12", 1.500 * 25.4, 25.4 / 12, 12, "UNF", UnifiedFineSource),
    };

    /// <summary>سری ویتوورث درشت BSW (گسترش ۰٫۳٫۰) — قطر OD دقیق اینچی.</summary>
    private static readonly List<CatalogEntry> WhitworthCoarse = new()
    {
        new("1/8-40 BSW", 0.125 * 25.4, 25.4 / 40, 40, "BSW", WhitworthSource),
        new("3/16-24 BSW", 0.1875 * 25.4, 25.4 / 24, 24, "BSW", WhitworthSource),
        new("1/4-20 BSW", 0.250 * 25.4, 25.4 / 20, 20, "BSW", WhitworthSource),
        new("5/16-18 BSW", 0.3125 * 25.4, 25.4 / 18, 18, "BSW", WhitworthSource),
        new("3/8-16 BSW", 0.375 * 25.4, 25.4 / 16, 16, "BSW", WhitworthSource),
        new("7/16-14 BSW", 0.4375 * 25.4, 25.4 / 14, 14, "BSW", WhitworthSource),
        new("1/2-12 BSW", 0.500 * 25.4, 25.4 / 12, 12, "BSW", WhitworthSource),
        new("9/16-12 BSW", 0.5625 * 25.4, 25.4 / 12, 12, "BSW", WhitworthSource),
        new("5/8-11 BSW", 0.625 * 25.4, 25.4 / 11, 11, "BSW", WhitworthSource),
        new("3/4-10 BSW", 0.750 * 25.4, 25.4 / 10, 10, "BSW", WhitworthSource),
        new("7/8-9 BSW", 0.875 * 25.4, 25.4 / 9, 9, "BSW", WhitworthSource),
        new("1-8 BSW", 1.000 * 25.4, 25.4 / 8, 8, "BSW", WhitworthSource),
        new("1-1/8-7 BSW", 1.125 * 25.4, 25.4 / 7, 7, "BSW", WhitworthSource),
        new("1-1/4-7 BSW", 1.250 * 25.4, 25.4 / 7, 7, "BSW", WhitworthSource),
        new("1-1/2-6 BSW", 1.500 * 25.4, 25.4 / 6, 6, "BSW", WhitworthSource),
        new("1-3/4-5 BSW", 1.750 * 25.4, 25.4 / 5, 5, "BSW", WhitworthSource),
        new("2-4.5 BSW", 2.000 * 25.4, 25.4 / 4.5, null, "BSW", WhitworthSource),
        new("2-1/4-4 BSW", 2.250 * 25.4, 25.4 / 4, 4, "BSW", WhitworthSource),
        new("2-1/2-4 BSW", 2.500 * 25.4, 25.4 / 4, 4, "BSW", WhitworthSource),
        new("3-3.5 BSW", 3.000 * 25.4, 25.4 / 3.5, null, "BSW", WhitworthSource),
    };

    /// <summary>سری ویتوورث ریز BSF (گسترش ۰٫۳٫۰).</summary>
    private static readonly List<CatalogEntry> WhitworthFine = new()
    {
        new("3/16-32 BSF", 0.1875 * 25.4, 25.4 / 32, 32, "BSF", WhitworthSource),
        new("1/4-26 BSF", 0.250 * 25.4, 25.4 / 26, 26, "BSF", WhitworthSource),
        new("5/16-22 BSF", 0.3125 * 25.4, 25.4 / 22, 22, "BSF", WhitworthSource),
        new("3/8-20 BSF", 0.375 * 25.4, 25.4 / 20, 20, "BSF", WhitworthSource),
        new("7/16-18 BSF", 0.4375 * 25.4, 25.4 / 18, 18, "BSF", WhitworthSource),
        new("1/2-16 BSF", 0.500 * 25.4, 25.4 / 16, 16, "BSF", WhitworthSource),
        new("9/16-16 BSF", 0.5625 * 25.4, 25.4 / 16, 16, "BSF", WhitworthSource),
        new("5/8-14 BSF", 0.625 * 25.4, 25.4 / 14, 14, "BSF", WhitworthSource),
        new("3/4-12 BSF", 0.750 * 25.4, 25.4 / 12, 12, "BSF", WhitworthSource),
        new("7/8-11 BSF", 0.875 * 25.4, 25.4 / 11, 11, "BSF", WhitworthSource),
        new("1-10 BSF", 1.000 * 25.4, 25.4 / 10, 10, "BSF", WhitworthSource),
        new("1-1/8-9 BSF", 1.125 * 25.4, 25.4 / 9, 9, "BSF", WhitworthSource),
        new("1-1/4-8 BSF", 1.250 * 25.4, 25.4 / 8, 8, "BSF", WhitworthSource),
        new("1-1/2-6 BSF", 1.500 * 25.4, 25.4 / 6, 6, "BSF", WhitworthSource),
        new("1-3/4-5 BSF", 1.750 * 25.4, 25.4 / 5, 5, "BSF", WhitworthSource),
        new("2-4.5 BSF", 2.000 * 25.4, 25.4 / 4.5, null, "BSF", WhitworthSource),
    };

    /// <summary>لوله‌ای مخروطی NPT (گسترش ۰٫۳٫۰) — قطر = OD لوله؛ مخروط در BuildSpec اعمال می‌شود.</summary>
    private static readonly List<CatalogEntry> NptTapered = new()
    {
        new("1/16-27 NPT", 0.3125 * 25.4, 25.4 / 27, 27, "NPT", NptSource),
        new("1/8-27 NPT", 0.405 * 25.4, 25.4 / 27, 27, "NPT", NptSource),
        new("1/4-18 NPT", 0.540 * 25.4, 25.4 / 18, 18, "NPT", NptSource),
        new("3/8-18 NPT", 0.675 * 25.4, 25.4 / 18, 18, "NPT", NptSource),
        new("1/2-14 NPT", 0.840 * 25.4, 25.4 / 14, 14, "NPT", NptSource),
        new("3/4-14 NPT", 1.050 * 25.4, 25.4 / 14, 14, "NPT", NptSource),
        new("1-11.5 NPT", 1.315 * 25.4, 25.4 / 11.5, null, "NPT", NptSource),
        new("1-1/4-11.5 NPT", 1.660 * 25.4, 25.4 / 11.5, null, "NPT", NptSource),
        new("1-1/2-11.5 NPT", 1.900 * 25.4, 25.4 / 11.5, null, "NPT", NptSource),
        new("2-11.5 NPT", 2.375 * 25.4, 25.4 / 11.5, null, "NPT", NptSource),
        new("2-1/2-8 NPT", 2.875 * 25.4, 25.4 / 8, 8, "NPT", NptSource),
        new("3-8 NPT", 3.500 * 25.4, 25.4 / 8, 8, "NPT", NptSource),
        new("4-8 NPT", 4.500 * 25.4, 25.4 / 8, 8, "NPT", NptSource),
        new("6-8 NPT", 6.625 * 25.4, 25.4 / 8, 8, "NPT", NptSource),
    };

    /// <summary>لوله‌ای موازی BSPP/G (گسترش ۰٫۳٫۰) — قطر = OD لوله.</summary>
    private static readonly List<CatalogEntry> BspParallel = new()
    {
        new("G1/16-28", 7.723, 25.4 / 28, 28, "BSPP", BspSource),
        new("G1/8-28", 9.728, 25.4 / 28, 28, "BSPP", BspSource),
        new("G1/4-19", 13.157, 25.4 / 19, 19, "BSPP", BspSource),
        new("G3/8-19", 16.662, 25.4 / 19, 19, "BSPP", BspSource),
        new("G1/2-14", 20.955, 25.4 / 14, 14, "BSPP", BspSource),
        new("G5/8-14", 22.911, 25.4 / 14, 14, "BSPP", BspSource),
        new("G3/4-14", 26.441, 25.4 / 14, 14, "BSPP", BspSource),
        new("G7/8-14", 30.201, 25.4 / 14, 14, "BSPP", BspSource),
        new("G1-11", 33.249, 25.4 / 11, 11, "BSPP", BspSource),
        new("G1-1/8-11", 37.897, 25.4 / 11, 11, "BSPP", BspSource),
        new("G1-1/4-11", 41.910, 25.4 / 11, 11, "BSPP", BspSource),
        new("G1-1/2-11", 47.803, 25.4 / 11, 11, "BSPP", BspSource),
        new("G1-3/4-11", 53.746, 25.4 / 11, 11, "BSPP", BspSource),
        new("G2-11", 59.614, 25.4 / 11, 11, "BSPP", BspSource),
    };

    /// <summary>ذوزنقه‌ای ISO (Tr) — زیرمجموعهٔ پرکاربرد (گسترش ۰٫۳٫۰).</summary>
    private static readonly List<CatalogEntry> TrapezoidalSeries = new()
    {
        new("Tr8×1.5", 8.0, 1.5, null, "Tr", TrapezoidalSource),
        new("Tr10×2", 10.0, 2.0, null, "Tr", TrapezoidalSource),
        new("Tr10×3", 10.0, 3.0, null, "Tr", TrapezoidalSource),
        new("Tr12×2", 12.0, 2.0, null, "Tr", TrapezoidalSource),
        new("Tr12×3", 12.0, 3.0, null, "Tr", TrapezoidalSource),
        new("Tr16×2", 16.0, 2.0, null, "Tr", TrapezoidalSource),
        new("Tr16×3", 16.0, 3.0, null, "Tr", TrapezoidalSource),
        new("Tr16×4", 16.0, 4.0, null, "Tr", TrapezoidalSource),
        new("Tr20×2", 20.0, 2.0, null, "Tr", TrapezoidalSource),
        new("Tr20×4", 20.0, 4.0, null, "Tr", TrapezoidalSource),
        new("Tr24×3", 24.0, 3.0, null, "Tr", TrapezoidalSource),
        new("Tr24×5", 24.0, 5.0, null, "Tr", TrapezoidalSource),
        new("Tr24×8", 24.0, 8.0, null, "Tr", TrapezoidalSource),
        new("Tr28×3", 28.0, 3.0, null, "Tr", TrapezoidalSource),
        new("Tr28×5", 28.0, 5.0, null, "Tr", TrapezoidalSource),
        new("Tr32×3", 32.0, 3.0, null, "Tr", TrapezoidalSource),
        new("Tr32×5", 32.0, 5.0, null, "Tr", TrapezoidalSource),
        new("Tr32×6", 32.0, 6.0, null, "Tr", TrapezoidalSource),
        new("Tr36×3", 36.0, 3.0, null, "Tr", TrapezoidalSource),
        new("Tr36×6", 36.0, 6.0, null, "Tr", TrapezoidalSource),
        new("Tr40×3", 40.0, 3.0, null, "Tr", TrapezoidalSource),
        new("Tr40×7", 40.0, 7.0, null, "Tr", TrapezoidalSource),
        new("Tr44×3", 44.0, 3.0, null, "Tr", TrapezoidalSource),
        new("Tr44×7", 44.0, 7.0, null, "Tr", TrapezoidalSource),
        new("Tr48×3", 48.0, 3.0, null, "Tr", TrapezoidalSource),
        new("Tr48×8", 48.0, 8.0, null, "Tr", TrapezoidalSource),
    };

    /// <summary>ACME عمومی اینچی (گسترش ۰٫۳٫۰).</summary>
    private static readonly List<CatalogEntry> AcmeSeries = new()
    {
        new("1/4-16 ACME", 0.250 * 25.4, 25.4 / 16, 16, "ACME", AcmeSource),
        new("5/16-14 ACME", 0.3125 * 25.4, 25.4 / 14, 14, "ACME", AcmeSource),
        new("3/8-12 ACME", 0.375 * 25.4, 25.4 / 12, 12, "ACME", AcmeSource),
        new("1/2-10 ACME", 0.500 * 25.4, 25.4 / 10, 10, "ACME", AcmeSource),
        new("5/8-8 ACME", 0.625 * 25.4, 25.4 / 8, 8, "ACME", AcmeSource),
        new("3/4-6 ACME", 0.750 * 25.4, 25.4 / 6, 6, "ACME", AcmeSource),
        new("7/8-6 ACME", 0.875 * 25.4, 25.4 / 6, 6, "ACME", AcmeSource),
        new("1-5 ACME", 1.000 * 25.4, 25.4 / 5, 5, "ACME", AcmeSource),
        new("1-1/4-5 ACME", 1.250 * 25.4, 25.4 / 5, 5, "ACME", AcmeSource),
        new("1-1/2-4 ACME", 1.500 * 25.4, 25.4 / 4, 4, "ACME", AcmeSource),
        new("1-3/4-4 ACME", 1.750 * 25.4, 25.4 / 4, 4, "ACME", AcmeSource),
        new("2-4 ACME", 2.000 * 25.4, 25.4 / 4, 4, "ACME", AcmeSource),
        new("2-1/2-3 ACME", 2.500 * 25.4, 25.4 / 3, 3, "ACME", AcmeSource),
        new("3-2 ACME", 3.000 * 25.4, 25.4 / 2, 2, "ACME", AcmeSource),
    };

    /// <summary>مخروط استاندارد NPT — ۱٫۷۸۹۹ درجه نسبت به محور (۱:۱۶).</summary>
    public const double NptTaperDegrees = 1.7899;

    /// <summary>
    /// مدخل‌های یک خانواده را برمی‌گرداند (کپی دفاعی تا کاتالوگ داخلی تغییرناپذیر بماند).
    /// </summary>
    public static IReadOnlyList<CatalogEntry> GetEntries(ThreadFamily family) => family switch
    {
        ThreadFamily.IsoMetric => MetricCoarse.Concat(MetricFine).ToList(),
        ThreadFamily.Unified => UnifiedCoarse.Concat(UnifiedFine).ToList(),
        ThreadFamily.Whitworth => WhitworthCoarse.Concat(WhitworthFine).ToList(),
        ThreadFamily.Npt => NptTapered.ToList(),
        ThreadFamily.Bsp => BspParallel.ToList(),
        ThreadFamily.IsoTrapezoidal => TrapezoidalSeries.ToList(),
        ThreadFamily.Acme => AcmeSeries.ToList(),
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
            HalfAngleDegrees = ThreadProfileFactory.FamilyHalfAngleDegrees(family),
            TaperDegrees = family == ThreadFamily.Npt ? NptTaperDegrees : 0,
            StandardId = $"CAT-{entry.SizeLabel}-{entry.Series}",
            DataSourceNote = entry.SourceNote,
        };
    }
}

using Sahand.Geometry.Meshing;

namespace Sahand.Exporters;

/// <summary>
/// وضعیت پشتیبانی هر فرمت — صداقت محصول (سند ۴-۳: ماتریس قابلیت).
/// </summary>
public enum ExportCapabilityStatus
{
    /// <summary>پیاده‌سازی و آزموده‌شده با round-trip.</summary>
    Supported,

    /// <summary>پیاده‌سازی شده؛ آزمون محدود (مثلاً بدون ابزار بیرونی).</summary>
    Limited,

    /// <summary>در برنامهٔ توسعه — نیاز به کتابخانه/مجوز؛ دکمه باید دلیل محدودیت را بگوید.</summary>
    Planned,

    /// <summary>وابسته به مجوز تجاری موتور؛ پیش از بررسی حقوقی فعال نمی‌شود.</summary>
    RequiresLicense,
}

/// <summary>
/// یک قابلیت خروجی با وضعیت و دلیل — منبع واحد UI و مستندات.
/// </summary>
public sealed record ExportFormatInfo(
    string FormatKey,
    string PersianName,
    ExportCapabilityStatus Status,
    string StatusReason,
    string UnitNote)
{
    /// <summary>پسوند فایل بدون نقطه.</summary>
    public string Extension => FormatKey.ToLowerInvariant();
}

/// <summary>
/// ماتریس قابلیت خروجی — تنها منبع حقیقت دربارهٔ فرمت‌ها (الزام سند ۴-۳).
/// </summary>
/// <remarks>
/// هیچ فرمتی خارج از این فهرست در UI فعال نمی‌شود و وضعیت هر فرمت با دلیل فارسی
/// به کاربر گفته می‌شود. STEP عمداً «برنامهٔ توسعه» است: صدور STEP معتبر نیازمند
/// هستهٔ B-Rep است و ساختنش از مش مثلثی ممنوع سند ۴ است («فایل جعلی با پسوند معتبر تولید نکن»).
/// </remarks>
public static class ExportCapabilityMatrix
{
    /// <summary>فهرست فرمت‌های قابل نمایش.</summary>
    public static IReadOnlyList<ExportFormatInfo> Formats { get; } =
    [
        new ExportFormatInfo("STL", "STL (باینری/ASCII)", ExportCapabilityStatus.Supported,
            "خروجی و آزمون round-trip با خوانندهٔ مستقل داخلی.",
            "واحد در STL ذخیره نمی‌شود؛ واحد انتخابی در فرادادهٔ نام فایل/گزارش ثبت و هشدار داده می‌شود."),
        new ExportFormatInfo("OBJ", "OBJ (+MTL)", ExportCapabilityStatus.Supported,
            "خروجی مش با گروه‌بندی؛ متریال نمایشی در MTL.",
            "واحد استاندارد ندارد؛ میلی‌متر مبنا است."),
        new ExportFormatInfo("PLY", "PLY (ASCII/باینری)", ExportCapabilityStatus.Supported,
            "خروجی مش با رنگ رأس اختیاری.",
            "واحد استاندارد ندارد؛ میلی‌متر مبنا است."),
        new ExportFormatInfo("3MF", "3MF", ExportCapabilityStatus.Supported,
            "بستهٔ OPC با manifest و واحد صریح میلی‌متر؛ آزمون round-trip داخلی.",
            "واحد در فایل صریح است (millimeter)."),
        new ExportFormatInfo("GLB", "glTF باینری (GLB)", ExportCapabilityStatus.Supported,
            "خروجی تک‌مش با متریال پایه.",
            "واحد glTF متر است؛ تبدیل mm→m در exporter اعمال و ثبت می‌شود."),
        new ExportFormatInfo("DXF", "DXF دوبعدی (R12)", ExportCapabilityStatus.Limited,
            "نقشهٔ پروفیل/نمای جانبی دوبعدی — مکمل، نه فرمت سه‌بعدی.",
            "خروجی دوبعدی است و جدا از فهرست سه‌بعدی ارائه می‌شود."),
        new ExportFormatInfo("STEP", "STEP AP242", ExportCapabilityStatus.Planned,
            "نیازمند هستهٔ B-Rep با مجوز قابل توزیع؛ ساخت STEP از مش مثلثی ممنوعِ سند ۴ است.",
            "—"),
    ];

    /// <summary>اطلاعات یک فرمت با کلید؛ null اگر ناشناخته.</summary>
    public static ExportFormatInfo? Find(string formatKey) =>
        Formats.FirstOrDefault(f => string.Equals(f.FormatKey, formatKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>آیا فرمت فعلاً خروجی واقعی دارد؟</summary>
    public static bool IsExportable(string formatKey) => Find(formatKey) is { Status: ExportCapabilityStatus.Supported or ExportCapabilityStatus.Limited };
}

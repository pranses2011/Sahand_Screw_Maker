using System.Text.Json;
using System.Text.Json.Serialization;
using Sahand.Core.Parts;
using Sahand.Core.Printing;
using Sahand.Core.Threads;

namespace Sahand.Core.Project;

/// <summary>
/// سند پروژهٔ بومی «سهند» — همهٔ دادهٔ پارامتریک لازم برای بازسازی هندسه را نگه می‌دارد.
/// </summary>
/// <remarks>
/// الزام سند ۳-۹: فایل مش جای پروژهٔ قابل ویرایش را نمی‌گیرد؛ این سند فقط پارامترها را
/// حمل می‌کند و هیچ دادهٔ حساس (راز، شمارهٔ کارت، مسیر سیستم، گزارش crash) در آن ذخیره نمی‌شود.
/// نسخهٔ schema برای مهاجرت‌های آینده حمل می‌شود؛ دادهٔ ناشناخته نسخه‌های جدید هنگام
/// بازکردن با نسخهٔ قدیمی دور ریخته نمی‌شود بلکه در <see cref="UnknownData"/> حفظ می‌گردد.
/// </remarks>
public sealed record ProjectDocument
{
    /// <summary>نسخهٔ جاری schema پروژه.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>نام پروژه.</summary>
    public string ProjectName { get; init; } = "پروژهٔ بدون نام";

    /// <summary>نوع قطعهٔ نگه‌داشته‌شده.</summary>
    public PartKind Kind { get; init; } = PartKind.Bolt;

    /// <summary>پیچ — فقط وقتی <see cref="Kind"/> برابر Bolt/Assembly معنا دارد.</summary>
    public BoltSpec? Bolt { get; init; }

    /// <summary>مهرهٔ مستقل.</summary>
    public NutSpec? Nut { get; init; }

    /// <summary>واشر.</summary>
    public WasherSpec? Washer { get; init; }

    /// <summary>میل رزوه‌دار.</summary>
    public ThreadedRodSpec? ThreadedRod { get; init; }

    /// <summary>مجموعهٔ جفت.</summary>
    public AssemblySpec? Assembly { get; init; }

    /// <summary>پروفایل چاپ ذخیره‌شده با پروژه.</summary>
    public PrintProfile? PrintProfile { get; init; }

    /// <summary>زمان ایجاد به وقت محلی ISO-8601.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    /// <summary>زمان آخرین تغییر.</summary>
    public DateTimeOffset ModifiedAt { get; init; } = DateTimeOffset.Now;

    /// <summary>نسخهٔ برنامهٔ ذخیره‌کننده — فقط برای عیب‌یابی/مهاجرت.</summary>
    public string AppVersion { get; init; } = "0.1.0";

    /// <summary>دادهٔ ناشناختهٔ نسخه‌های جدیدتر schema — برای حفظ اطلاعات هنگام مهاجرت معکوس.</summary>
    public JsonElement? UnknownData { get; init; }

    /// <summary>گزینه‌های سریال‌سازی JSON مشترک ذخیره/بارگذاری پروژه.</summary>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // enumها به‌صورت رشته ذخیره می‌شوند تا فایل پروژه برای انسان خوانا و مقاوم به ترتیب عددی باشد.
        Converters = { new JsonStringEnumConverter() },
    };
}

using Sahand.Core.Units;
using Sahand.Geometry.Meshing;

namespace Sahand.Exporters;

/// <summary>preset کیفیت خروجی مش — قابل فهم با tooltip (سند ۴-۴).</summary>
public enum ExportQualityPreset
{
    /// <summary>پیش‌نویس — فایل کوچک برای بازبینی سریع.</summary>
    Draft,

    /// <summary>متعادل — پیش‌فرض.</summary>
    Balanced,

    /// <summary>دقیق — برای چاپ قطعات کوچک.</summary>
    Fine,

    /// <summary>بسیار دقیق — فایل و زمان سنگین.</summary>
    VeryFine,

    /// <summary>فوق‌دقیق — مش خیلی ریز برای نرمی عالی رزوه (گسترش ۰٫۴٫۰).</summary>
    Ultra,

    /// <summary>حدی — بیشترین نرمی قطعه؛ سنگین‌ترین فایل و زمان ساخت (گسترش ۰٫۴٫۰).</summary>
    Extreme,
}

/// <summary>
/// تنظیمات خروجی: فرمت، کیفیت، واحد مقصد و مسیر — ورودی واحد همهٔ خروجی‌گیرها.
/// </summary>
/// <remarks>
/// کیفیت خروجی فایل از preset نمایش viewport جدا است (سند ۴-۴). واحد مقصد برای فرمت‌های
/// بدون واحد (STL/OBJ/PLY) فقط در نام/گزارش ثبت می‌شود؛ برای GLB تبدیل واقعی mm→m
/// اعمال می‌شود. نام فارسی/فاصله مجاز است (EXP-04/UI سند ۶-۴).
/// </remarks>
public sealed record ExportOptions
{
    /// <summary>فرمت مقصد (کلید ماتریس، مثل STL).</summary>
    public string FormatKey { get; init; } = "STL";

    /// <summary>کیفیت مش.</summary>
    public ExportQualityPreset Quality { get; init; } = ExportQualityPreset.Balanced;

    /// <summary>واحد مقصد برای فرمت‌های حامل/بدون واحد.</summary>
    public LengthUnit TargetUnit { get; init; } = LengthUnit.Millimeter;

    /// <summary>نام قطعه برای فراداده (اسم جزء در 3MF/GLB).</summary>
    public string PartName { get; init; } = "قطعه";

    /// <summary>STL به‌صورت ASCII (پیش‌فرض باینری).</summary>
    public bool AsciiStl { get; init; }

    /// <summary>PLY به‌صورت ASCII.</summary>
    public bool AsciiPly { get; init; }

    /// <summary>رنگ نمایشی متریال (RRGGBB) برای فرمت‌های حامل رنگ.</summary>
    public string? MaterialHexColor { get; init; }

    /// <summary>مقیاس اعمالی روی مختصات (پیش‌فرض ۱).</summary>
    public double ScaleFactor { get; init; } = 1.0;

    /// <summary>پسوند پیشنهادی فایل بر اساس فرمت.</summary>
    public string Extension => (ExportCapabilityMatrix.Find(FormatKey)?.Extension ?? "bin").ToLowerInvariant();
}

namespace Sahand.Geometry.Appearance;

/// <summary>
/// جلوهٔ نمایشی متریال — فقط «ظاهر» است و هیچ ادعایی دربارهٔ خواص مکانیکی ندارد (سند ۱-۴).
/// </summary>
/// <remarks>
/// رنگ‌ها برای پیش‌نمایش سه‌بعدی و فرمت‌های حامل رنگ (3MF/GLB) استفاده می‌شوند.
/// مقادیر تقریبی و نمایشی‌اند؛ متریال واقعی چاپ از جنس فیلامنت/رزین فرایند تعیین می‌شود.
/// </remarks>
public sealed record MaterialAppearance(string PersianName, byte R, byte G, byte B)
{
    /// <summary>فولاد بدون پوشش — خاکستری روشن.</summary>
    public static MaterialAppearance Steel { get; } = new("فولاد (نمایشی)", 0xB8, 0xBC, 0xC2);

    /// <summary>فولاد زنگ‌نزن — خاکستری روشن‌تر با ته‌مایهٔ سرد.</summary>
    public static MaterialAppearance Stainless { get; } = new("زنگ‌نزن (نمایشی)", 0xC7, 0xCC, 0xD1);

    /// <summary>برنج — زرد مات.</summary>
    public static MaterialAppearance Brass { get; } = new("برنج (نمایشی)", 0xC9, 0xA2, 0x4B);

    /// <summary>گالوانیزه — خاکستری مات با ته‌مایهٔ آبی.</summary>
    public static MaterialAppearance Galvanized { get; } = new("گالوانیزه (نمایشی)", 0x9E, 0xA8, 0xAE);

    /// <summary>پوشش سیاه (Black Oxide).</summary>
    public static MaterialAppearance BlackOxide { get; } = new("پوشش سیاه (نمایشی)", 0x3E, 0x42, 0x48);

    /// <summary>پلاستیک چاپی — رنگ خنثی برای پیش‌نمایش.</summary>
    public static MaterialAppearance Plastic { get; } = new("پلاستیک چاپی (نمایشی)", 0xE8, 0x8B, 0x3C);

    /// <summary>فهرست گزینه‌های UI.</summary>
    public static IReadOnlyList<MaterialAppearance> Presets { get; } =
    [
        Steel, Stainless, Brass, Galvanized, BlackOxide, Plastic,
    ];

    /// <summary>رنگ به‌صورت RRGGBB هگز برای فرمت‌های حامل رنگ.</summary>
    public string HexColor => $"#{R:X2}{G:X2}{B:X2}";
}

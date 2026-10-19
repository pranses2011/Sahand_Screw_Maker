namespace Sahand.Geometry.Meshing;

/// <summary>
/// preset کیفیت مش — کنترل وتر/زاویهٔ رزوه و رزولوشن زاویه‌ای (سند ۴-۴).
/// </summary>
/// <remarks>
/// دو preset متفاوت باید تفاوت عددی قابل‌اندازه‌گیری در تعداد وجه بدهند (EXP-02).
/// گام z نسبی است (نسبت به گام رزوه) تا برای رزوهٔ ریز و درشت به‌یک‌اندازه معنادار بماند.
/// preset نمایش viewport نباید با preset خروجی فایل اشتباه شود — این ساختار فقط برای خروجی/ساخت نهایی است.
/// </remarks>
public enum MeshQualityPreset
{
    /// <summary>پیش‌نمایش سریع — خطای وتر بیشتر؛ فقط برای viewport.</summary>
    Draft,

    /// <summary>تعادل سرعت/کیفیت — پیش‌فرض خروجی معمول.</summary>
    Balanced,

    /// <summary>دقیق — برای چاپ قطعات کوچک/ریز-گام.</summary>
    Fine,

    /// <summary>بسیار دقیق — فایل و زمان ساخت سنگین؛ پیش از اجرا به کاربر اعلام شود.</summary>
    VeryFine,

    /// <summary>فوق‌دقیق — ۵۱۲ بخش زاویه‌ای و گام z خیلی ریز؛ رزوهٔ بسیار نرم (گسترش ۰٫۴٫۰).</summary>
    Ultra,

    /// <summary>حدی — ۷۶۸ بخش زاویه‌ای و ریزترین گام z؛ بیشترین نرمی قطعه، سنگین‌ترین فایل/زمان (گسترش ۰٫۴٫۰).</summary>
    Extreme,
}

/// <summary>
/// تنظیمات تفکیک مش بر اساس preset — منبع واحد محاسبهٔ گام شبکه.
/// </summary>
public static class MeshQuality
{
    /// <summary>
    /// گام z شبکهٔ رزوه را برحسب میلی‌متر برای preset و گام رزوهٔ داده‌شده می‌دهد.
    /// </summary>
    /// <param name="preset">preset کیفیت.</param>
    /// <param name="pitchMm">گام رزوه — مقیاس نسبی تفکیک.</param>
    public static double ZStepFor(MeshQualityPreset preset, double pitchMm) => preset switch
    {
        MeshQualityPreset.Draft => Math.Max(pitchMm / 6.0, 0.3),
        MeshQualityPreset.Balanced => Math.Max(pitchMm / 10.0, 0.15),
        MeshQualityPreset.Fine => Math.Max(pitchMm / 16.0, 0.08),
        MeshQualityPreset.VeryFine => Math.Max(pitchMm / 24.0, 0.04),
        MeshQualityPreset.Ultra => Math.Max(pitchMm / 36.0, 0.025),
        MeshQualityPreset.Extreme => Math.Max(pitchMm / 48.0, 0.018),
        _ => Math.Max(pitchMm / 10.0, 0.15),
    };

    /// <summary>
    /// تعداد هدف بخش زاویه‌ای دور کامل — بر اساس preset و پیشروی (تا پیشرفت مارپیچی هر گام شبکه از گام z بیشتر نشود).
    /// </summary>
    /// <param name="preset">preset کیفیت.</param>
    /// <param name="leadMm">پیشروی رزوه (گام × شروع‌ها) — برای چندشروع شبکهٔ زاویه‌ای ریزتر لازم است.</param>
    /// <param name="zStepMm">گام z محاسبه‌شده.</param>
    public static int AngularSegmentsFor(MeshQualityPreset preset, double leadMm, double zStepMm)
    {
        var baseCount = preset switch
        {
            MeshQualityPreset.Draft => 48,
            MeshQualityPreset.Balanced => 96,
            MeshQualityPreset.Fine => 192,
            MeshQualityPreset.VeryFine => 320,
            MeshQualityPreset.Ultra => 512,
            MeshQualityPreset.Extreme => 768,
            _ => 96,
        };

        // پیشرفت مارپیچی در هر گام زاویه‌ای نباید از گام z بگذرد وگرنه دندانه‌ها روی سطح «له» می‌شوند.
        // سقف ۱۴۴۰ از ۰٫۴٫۰: اجازهٔ مش خیلی ریز برای Ultra/Extreme (پیش‌تر ۷۲۰ بود).
        var required = (int)Math.Ceiling(leadMm / Math.Max(zStepMm, 1e-6));
        return Math.Clamp(Math.Max(baseCount, required), 48, 1440);
    }
}

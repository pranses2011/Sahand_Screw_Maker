namespace Sahand.Core.Threads;

/// <summary>
/// یک نقطهٔ پروفیل مقطع رزوه در صفحهٔ (شعاع، محور) — واحد هر دو مختصات میلی‌متر.
/// </summary>
/// <remarks>
/// قرارداد: محور قطعه Z است و پروفیل در نیم‌صفحهٔ حاوی محور تعریف می‌شود؛
/// شعاع همیشه نامنفی است. جهت مثبت محور از سطح اتکا به سمت انتهای ساق است.
/// </remarks>
/// <param name="RadiusMm">فاصلهٔ نقطه از محور برحسب میلی‌متر (نامنفی).</param>
/// <param name="AxialMm">موقعیت محوری نقطه نسبت به مبدأ پروفیل برحسب میلی‌متر.</param>
public sealed record ThreadProfilePoint(double RadiusMm, double AxialMm);

/// <summary>
/// نتیجهٔ ساخت پروفیل رزوه از مشخصات — نقاط نهایی به‌همراه قطرهای مشتق.
/// </summary>
/// <param name="Points">نقاط پروفیل به‌ترتیب؛ اولین و آخرین نقطه روی قطر ریشه هستند و بازهٔ محوری کل دقیقاً یک «پیشروی» (lead) را می‌پوشاند.</param>
/// <param name="MajorDiameterMm">قطر خارجی پروفیل (برجسته‌ترین شعاع ×۲).</param>
/// <param name="MinorDiameterMm">قطر ریشهٔ پروفیل (کمترین شعاع ×۲).</param>
/// <param name="PitchDiameterMm">قطر گام (میانگین وزنی تاج/ریشه) — نمایشی و تقریبی.</param>
public sealed record ThreadProfileGeometry(
    IReadOnlyList<ThreadProfilePoint> Points,
    double MajorDiameterMm,
    double MinorDiameterMm,
    double PitchDiameterMm)
{
    /// <summary>
    /// تابع شعاعی تناوبی معادل این پروفیل را می‌سازد — ورودی مستقیم موتور هندسه.
    /// </summary>
    public RadialProfileFunction ToProfileFunction() => new(Points);
}

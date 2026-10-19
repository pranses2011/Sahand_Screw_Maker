using Sahand.Core.Threads;

namespace Sahand.Core.Validation;

/// <summary>
/// اعتبارسنج مشخصات رزوه پیش از ساخت هندسه (پوشش GEO-05 و GEO-07).
/// </summary>
/// <remarks>
/// قواعد سند ۳-۷: رد صفر/منفی/NaN/بی‌نهایت/بسیار بزرگ؛ سازگاری طول رزوه با طول قطعه در
/// اعتبارسنجِ قطعه انجام می‌شود. محدوده‌های اینجا از توان موتور هندسه می‌آیند نه حدس بازاری.
/// </remarks>
public static class ThreadValidator
{
    /// <summary>حداقل قطر قابل مدل‌سازی برحسب میلی‌متر — زیر آن کیفیت مش معتبر نمی‌ماند.</summary>
    public const double MinDiameterMm = 0.5;

    /// <summary>حداکثر قطر معقول برای دسکتاپ برحسب میلی‌متر.</summary>
    public const double MaxDiameterMm = 300.0;

    /// <summary>حداقل گام برحسب میلی‌متر — زیر آن دندانه در دقت مش قابل تفکیک نیست.</summary>
    public const double MinPitchMm = 0.1;

    /// <summary>حداکثر گام برحسب میلی‌متر.</summary>
    public const double MaxPitchMm = 24.0;

    /// <summary>حداکثر تعداد شروع‌های رزوه.</summary>
    public const int MaxStarts = 8;

    /// <summary>
    /// مشخصات رزوه را کامل بررسی می‌کند.
    /// </summary>
    /// <param name="spec">مشخصات رزوه.</param>
    /// <returns>نتیجه با پیام‌های فارسی قابل اقدام.</returns>
    public static ValidationResult Validate(ThreadSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var result = new ValidationResult();

        CheckFinite(result, "NominalDiameterMm", spec.NominalDiameterMm, "قطر اسمی");
        CheckFinite(result, "PitchMm", spec.PitchMm, "گام");
        CheckFinite(result, "ThreadLengthMm", spec.ThreadLengthMm, "طول رزوه");
        CheckFinite(result, "HalfAngleDegrees", spec.HalfAngleDegrees, "نیم‌زاویهٔ پروفیل");
        CheckFinite(result, "TaperDegrees", spec.TaperDegrees, "زاویهٔ مخروط");

        if (result.HasErrors) return result; // ادامهٔ بررسی با مقدار نامعتبر بی‌معناست.

        if (spec.NominalDiameterMm < MinDiameterMm || spec.NominalDiameterMm > MaxDiameterMm)
            result.AddError("NominalDiameterMm",
                $"قطر اسمی باید بین {MinDiameterMm} و {MaxDiameterMm} میلی‌متر باشد (مقدار فعلی: {spec.NominalDiameterMm:0.###}).");

        if (spec.PitchMm < MinPitchMm || spec.PitchMm > MaxPitchMm)
            result.AddError("PitchMm",
                $"گام باید بین {MinPitchMm} و {MaxPitchMm} میلی‌متر باشد (مقدار فعلی: {spec.PitchMm:0.###}).");

        // گام بزرگ‌تر از نصف قطر هندسهٔ معنادار رزوه را از بین می‌برد (عمق پروفیل > شعاع).
        if (spec.PitchMm > spec.NominalDiameterMm / 2.0)
            result.AddError("PitchMm",
                $"گام ({spec.PitchMm:0.###}mm) نسبت به قطر ({spec.NominalDiameterMm:0.###}mm) بسیار درشت است؛ گام حداکثر نصف قطر می‌تواند باشد.");

        if (spec.Starts < 1 || spec.Starts > MaxStarts)
            result.AddError("Starts",
                $"تعداد شروع‌ها باید بین ۱ و {MaxStarts} باشد (مقدار فعلی: {spec.Starts}).");

        if (spec.ThreadLengthMm < 0)
            result.AddError("ThreadLengthMm", "طول رزوه نمی‌تواند منفی باشد.");

        if (spec.TaperDegrees is < 0 or > 30)
            result.AddError("TaperDegrees",
                $"زاویهٔ مخروط باید بین ۰ و ۳۰ درجه باشد (مقدار فعلی: {spec.TaperDegrees:0.####}). NPT حدود ۱٫۷۹ درجه است.");

        if (spec.HalfAngleDegrees is < 0 or > 45)
            result.AddError("HalfAngleDegrees",
                $"نیم‌زاویهٔ پروفیل باید بین ۰ و ۴۵ درجه باشد (مقدار فعلی: {spec.HalfAngleDegrees:0.##}).");

        // خانوادهٔ سفارشی باید پروفیل معتبر داشته باشد؛ خطای دقیق از کارخانهٔ پروفیل گرفته می‌شود.
        if (spec.Family == ThreadFamily.Custom)
        {
            if (spec.CustomProfile is null || spec.CustomProfile.Count < 3)
                result.AddError("CustomProfile", "برای رزوهٔ سفارشی، پروفیل با دست‌کم ۳ نقطه لازم است.");
            else
            {
                try
                {
                    ThreadProfileFactory.BuildProfile(spec);
                }
                catch (ArgumentException ex)
                {
                    result.AddError("CustomProfile", ex.Message);
                }
            }
        }

        // هشدارهای غیرمسدودکننده.
        // NPT همیشه مخروطی است (۱:۱۶)؛ BSP موازی (BSPP) معتبر است و فقط مخروطی‌بودن BSPT اختیاری.
        if (spec.Family == ThreadFamily.Npt && spec.TaperDegrees <= 0)
            result.AddWarning("TaperDegrees",
                "NPT ذاتاً مخروطی است؛ زاویهٔ مخروط صفر گذاشته شده. مقدار ~۱٫۷۹ درجه (۱:۱۶) توصیه می‌شود.");

        if (spec.Family == ThreadFamily.Npt && spec.TaperDegrees is > 0 and (< 1.0 or > 3.0))
            result.AddWarning("TaperDegrees",
                $"مخروط NPT غیرمعمول است ({spec.TaperDegrees:0.##}° در برابر ~۱٫۷۹° استاندارد ۱:۱۶)؛ پیش از ساخت بررسی کنید.");

        if (spec.ModelingMode == ThreadModelingMode.Simplified)
            result.AddWarning("ModelingMode",
                "رزوهٔ نمایشی انتخاب شده: هندسهٔ واقعی رزوه در مدل و خروجی ساخته نمی‌شود و خروجی برچسب روشن خواهد داشت.");

        return result;
    }

    /// <summary>
    /// بررسی عددی بودن و محدودهٔ منطقی مقدار (رد NaN/Infinity/بسیار بزرگ — GEO-07).
    /// </summary>
    private static void CheckFinite(ValidationResult result, string parameterName, double value, string faName)
    {
        if (double.IsNaN(value))
            result.AddError(parameterName, $"{faName} عدد نیست (NaN)؛ مقدار معتبر وارد کنید.");

        if (double.IsInfinity(value))
            result.AddError(parameterName, $"{faName} بی‌نهایت است؛ مقدار متناهی وارد کنید.");

        if (Math.Abs(value) > 1e7)
            result.AddError(parameterName, $"{faName} خارج از محدودهٔ معقول مهندسی است (|مقدار| > 1e7).");
    }
}

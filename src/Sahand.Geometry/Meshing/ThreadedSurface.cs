using Sahand.Core.Threads;

namespace Sahand.Geometry.Meshing;

/// <summary>
/// سازندهٔ سطح رزوهٔ مارپیچِ واقعی روی شبکهٔ (θ × z) — هستهٔ هندسی محصول.
/// </summary>
/// <remarks>
/// مدل ریاضی: سطح رزوه = تابع شعاعی R(θ,z) = ρ(z − Δ(θ)) که در آن
/// Δ(θ) = ±(lead/2π)·θ است (علامت بر اساس راست‌گرد/چپ‌گرد) و ρ تابع پروفیل تناوبی
/// با دورهٔ lead است (الزام تفکیک pitch/lead در سند ۷-۵ همین‌جا اعمال می‌شود:
/// برای چندشروع، ρ در هر دورهٔ lead تعداد «شروع‌ها» دندانه دارد و فاز زاویه‌ای
/// دندانه‌های دو رشتهٔ مختلف به‌طور طبیعی از جابه‌جایی مارپیچی بیرون می‌آید).
/// runout (گذار ساق صاف به رزوه) با شیب خطی و پخ نوک با مخروط ۴۵ درجه ترکیب می‌شود.
/// محدودیت صادقانه: زوایای شکست پروفیل روی همهٔ θ دقیق نمی‌افتند؛ خطای وتر با preset
/// کیفیت کنترل می‌شود (سند ۴-۴) و در گزارش کیفیت قابل مشاهده است.
/// </remarks>
public static class ThreadedSurface
{
    /// <summary>
    /// ساق رزوه‌دار کامل پیچ را می‌سازد: سطح جانبی + درپوش‌های دو سر.
    /// </summary>
    /// <param name="mesh">مش مقصد.</param>
    /// <param name="spec">مشخصات رزوه.</param>
    /// <param name="profile">تابع پروفیل تناوبی (ساخته‌شده با ThreadProfileFactory).</param>
    /// <param name="zStartMm">آغاز ساق (معمولاً ۰ = زیر سر).</param>
    /// <param name="zEndMm">انتهای ساق (نوک).</param>
    /// <param name="nominalRadiusMm">شعاع اسمی ساق (برای ساق صاف و مخروط پخ).</param>
    /// <param name="threadStartMm">آغاز ناحیهٔ کامل رزوه؛ قبل از آن ساق صاف + runout است.</param>
    /// <param name="tipChamferMm">پخ نوک (۰ = بدون پخ).</param>
    /// <param name="endChamferMm">پخ سرِ ساق در zStart (۰ = بدون پخ؛ برای میل دوطرفه).</param>
    /// <param name="grid">شبکهٔ زاویه‌ای مشترک با سایر اجزای قطعه.</param>
    /// <param name="zStepMm">گام z شبکه.</param>
    /// <param name="capStart">درپوش در zStart لازم است (زیر سر معمولاً خودِ حلقهٔ سر می‌پوشاند).</param>
    /// <param name="capEnd">درپوش نوک لازم است.</param>
    public static void AddThreadedShank(
        MeshBuilder mesh,
        ThreadSpec spec,
        RadialProfileFunction profile,
        double zStartMm,
        double zEndMm,
        double nominalRadiusMm,
        double threadStartMm,
        double tipChamferMm,
        double endChamferMm,
        AngularGrid grid,
        double zStepMm,
        bool capStart,
        bool capEnd)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(profile);
        if (zEndMm <= zStartMm) throw new ArgumentException("انتهای ساق باید بزرگ‌تر از آغاز آن باشد.");

        var lead = spec.LeadMm;
        var handedSign = spec.Handedness == ThreadHandedness.Right ? 1.0 : -1.0;
        var taperFactor = Math.Tan(spec.TaperDegrees * Math.PI / 180.0);

        // طول ناحیهٔ runout: ۱٫۵ گام — گذار تدریجی ساق صاف به دندانهٔ کامل (تقریب استانداردِ نمایشی).
        var runoutLength = 1.5 * spec.PitchMm;
        var runoutStart = threadStartMm - runoutLength;

        double RadiusAt(double theta, double z)
        {
            // جابه‌جایی مارپیچی: جهت راست‌گرد = افزایش z با افزایش θ (دیده از +Z).
            var delta = handedSign * lead * theta / (2.0 * Math.PI);
            var r = profile.Evaluate(z - delta);

            // runout: از شعاع اسمی (ساق صاف) به کف رزوه به‌طور خطی می‌رسیم؛ max دندانه را «قورت» می‌دهد.
            if (z < threadStartMm)
            {
                var t = Math.Clamp((z - runoutStart) / runoutLength, 0.0, 1.0);
                var ramp = nominalRadiusMm - t * (nominalRadiusMm - profile.MinRadius);
                r = Math.Max(r, ramp);
            }

            // مخروط (NPT/BSP): کل پروفیل با شعاع نسبی z بزرگ می‌شود؛ مرجع zStart.
            if (taperFactor > 0)
            {
                r *= (nominalRadiusMm + (z - zStartMm) * taperFactor) / nominalRadiusMm;
            }

            // پخ نوک ۴۵ درجه: از (zEnd − پخ) به انتهای شعاعِ (اسمی − پخ) خطی کوچک می‌شود.
            if (tipChamferMm > 0 && z > zEndMm - tipChamferMm)
            {
                var cone = nominalRadiusMm - (z - (zEndMm - tipChamferMm));
                r = Math.Min(r, Math.Max(cone, 0.0));
            }

            // پخ سر ساق (برای میل دو سر): متقارن با نوک.
            if (endChamferMm > 0 && z < zStartMm + endChamferMm)
            {
                var cone = nominalRadiusMm - (zStartMm + endChamferMm - z);
                r = Math.Min(r, Math.Max(cone, 0.0));
            }

            return Math.Max(r, 0.0);
        }

        // سطوح z شبکه: یکنواخت + نقاط ویژهٔ ساختاری (گذرها و پخ‌ها) تا مرزها تیز بمانند.
        var zLevels = BuildZLevels(zStartMm, zEndMm, zStepMm,
            specialLevels: [threadStartMm, runoutStart, zEndMm - tipChamferMm, zStartMm + endChamferMm]);

        RadialSurface.AddOutwardSurface(mesh, grid, RadiusAt, zLevels);

        // درپوش‌ها: بادبزکی از محور؛ مرز درپوش همان تابع شعاعی در z انتهایی است (ستاره‌ای).
        if (capStart)
            RadialSurface.AddDiscCap(mesh, grid, theta => RadiusAt(theta, zStartMm), zStartMm, solidBelow: false);

        if (capEnd)
            RadialSurface.AddDiscCap(mesh, grid, theta => RadiusAt(theta, zEndMm), zEndMm, solidBelow: true);
    }

    /// <summary>
    /// حفرهٔ رزوهٔ داخلی مهره را می‌سازد — سطح شعاعی با نرمال رو به محور + پخ دهانه.
    /// </summary>
    /// <param name="mesh">مش مقصد.</param>
    /// <param name="spec">مشخصات رزوهٔ داخلی.</param>
    /// <param name="profile">پروفیل داخلی (دره روی دیواره، نوک به محور).</param>
    /// <param name="zBottomMm">کف مهره (۰).</param>
    /// <param name="zTopMm">سطح بالای مهره (ارتفاع).</param>
    /// <param name="mouthChamferMm">پخ دهانهٔ دو سر حفره (کمک به شروع پیچش و چاپ).</param>
    /// <param name="grid">شبکهٔ زاویه‌ای مشترک.</param>
    /// <param name="zStepMm">گام z.</param>
    public static void AddInternalCavity(
        MeshBuilder mesh,
        ThreadSpec spec,
        RadialProfileFunction profile,
        double zBottomMm,
        double zTopMm,
        double mouthChamferMm,
        AngularGrid grid,
        double zStepMm)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(profile);
        if (zTopMm <= zBottomMm) throw new ArgumentException("بالای حفره باید بزرگ‌تر از کف باشد.");

        var lead = spec.LeadMm;
        var handedSign = spec.Handedness == ThreadHandedness.Right ? 1.0 : -1.0;

        double RadiusAt(double theta, double z)
        {
            var delta = handedSign * lead * theta / (2.0 * Math.PI);
            var r = profile.Evaluate(z - delta);

            // پخ دهانه: حفره در دو سر به‌صورت مخروط ۴۵ درجه باز می‌شود (کمک به شروع پیچش و چاپ).
            if (mouthChamferMm > 0)
            {
                var holeRadius = profile.MaxRadius; // شعاع دیوارهٔ حفره (درهٔ داخلی).
                var bottomCone = holeRadius + Math.Clamp(mouthChamferMm - (z - zBottomMm), 0.0, mouthChamferMm);
                var topCone = holeRadius + Math.Clamp(mouthChamferMm - (zTopMm - z), 0.0, mouthChamferMm);
                r = Math.Max(r, Math.Max(bottomCone, topCone));
            }

            return r;
        }

        var zLevels = BuildZLevels(zBottomMm, zTopMm, zStepMm,
            specialLevels: [zBottomMm + mouthChamferMm, zTopMm - mouthChamferMm]);

        RadialSurface.AddInwardSurface(mesh, grid, RadiusAt, zLevels);
    }

    /// <summary>
    /// سطوح z شبکه را می‌سازد: یکنواخت + سطوح ویژه (مرزها/گذارها) مرتب و بدون تکرار.
    /// </summary>
    private static List<double> BuildZLevels(double zStart, double zEnd, double zStep, double[] specialLevels)
    {
        var step = Math.Max(zStep, 1e-4);
        var count = (int)Math.Ceiling((zEnd - zStart) / step);
        var levels = new List<double>(count + 8);

        for (var i = 0; i <= count; i++)
        {
            // clamp تا zEnd دقیق بیفتد؛ آخرین سطح دقیقاً zEnd است.
            levels.Add(Math.Min(zStart + i * step, zEnd));
        }

        foreach (var special in specialLevels)
        {
            if (special > zStart + 1e-9 && special < zEnd - 1e-9)
                levels.Add(special);
        }

        levels.Sort();

        // حذف تکراری‌های نزدیک (کمتر از 1e-7) که وجه‌های بی‌مساحت می‌سازند.
        var result = new List<double>(levels.Count) { levels[0] };
        foreach (var z in levels)
        {
            if (z - result[^1] > 1e-7)
                result.Add(z);
        }

        // تضمین سرِ پایانی دقیق.
        if (result[^1] < zEnd - 1e-9)
            result.Add(zEnd);

        return result;
    }
}

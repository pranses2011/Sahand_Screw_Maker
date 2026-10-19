using Sahand.Core.Threads;

namespace Sahand.Geometry.Meshing;

/// <summary>
/// سازندهٔ سطح رزوهٔ مارپیچِ واقعی روی شبکهٔ (θ × z) — هستهٔ هندسی محصول.
/// </summary>
/// <remarks>
/// مدل ریاضی: سطح رزوه = تابع شعاعی R(θ,z) = ρ(z − Δ(θ)) که در آن
/// Δ(θ) = ±(lead/2π)·θ است (علامت بر اساس راست‌گرد/چپ‌گرد) و ρ تابع پروفیل تناوبی
/// با دورهٔ lead است. تفکیک pitch/lead (سند ۷-۵) همین‌جا اعمال می‌شود: برای چندشروع،
/// ρ در هر دورهٔ lead تعداد «شروع‌ها» دندانه دارد و فاز زاویه‌ای دندانه‌های رشته‌های
/// مختلف به‌طور طبیعی از جابه‌جایی مارپیچی بیرون می‌آید.
/// ناحیهٔ کامل رزوه می‌تواند چند تکه باشد (مثل میلهٔ دو سر رزوه با میان‌بخش صاف)؛
/// گذارِ هر مرز با شیب خطی ۱٫۵ گام (runout) انجام می‌شود و کل ساق با «یک» تابع شعاع
/// و «یک» شبکه ساخته می‌شود تا یال‌های مشترک دقیق و مش بسته بماند.
/// محدودیت صادقانه: زوایای شکست پروفیل روی همهٔ θ دقیق نمی‌افتند؛ خطای وتر با preset
/// کیفیت کنترل می‌شود (سند ۴-۴).
/// </remarks>
public static class ThreadedSurface
{
    /// <summary>ناحیهٔ رزوهٔ کامل [From, To] برحسب میلی‌متر روی محور قطعه.</summary>
    public readonly record struct ThreadZone(double FromMm, double ToMm);

    /// <summary>طول گذار runout برحسب میلی‌متر — ۱٫۵ گام؛ تقریب نمایشیِ متداول.</summary>
    private static double RunoutLength(ThreadSpec spec) => 1.5 * spec.PitchMm;

    /// <summary>
    /// تابع شعاع کامل ساق رزوه‌دار را می‌سازد — استفادهٔ مشترکِ سازندهٔ ساق و درپوش‌های حلقه‌ای سر.
    /// </summary>
    /// <param name="spec">مشخصات رزوه.</param>
    /// <param name="profile">پروفیل تناوبی.</param>
    /// <param name="nominalRadiusMm">شعاع اسمی ساق (سطح صاف/تاج).</param>
    /// <param name="zones">نواحی رزوهٔ کامل.</param>
    /// <param name="zStartMm">مرجع مخروط و پخ سر.</param>
    /// <param name="zEndMm">مرجع پخ نوک.</param>
    /// <param name="tipChamferMm">پخ نوک (سمت z بزرگ‌تر).</param>
    /// <param name="endChamferMm">پخ سر (سمت z کوچک‌تر).</param>
    public static Func<double, double, double> CreateShankRadiusFunction(
        ThreadSpec spec,
        RadialProfileFunction profile,
        double nominalRadiusMm,
        ThreadZone[] zones,
        double zStartMm,
        double zEndMm,
        double tipChamferMm,
        double endChamferMm)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(zones);

        var lead = spec.LeadMm;
        var handedSign = spec.Handedness == ThreadHandedness.Right ? 1.0 : -1.0;
        var taperFactor = Math.Tan(spec.TaperDegrees * Math.PI / 180.0);
        var runout = RunoutLength(spec);
        var rootRadius = profile.MinRadius;

        return (theta, z) =>
        {
            // جابه‌جایی مارپیچی: راست‌گرد = افزایش z با افزایش θ (دید از +Z).
            var delta = handedSign * lead * theta / (2.0 * Math.PI);
            var r = profile.Evaluate(z - delta);

            // گذار هر مرز ناحیه با شیب خطی؛ بیرون از گذار اثر ندارد.
            foreach (var zone in zones)
            {
                if (z < zone.FromMm)
                {
                    var t = Math.Clamp((zone.FromMm - z) / runout, 0.0, 1.0);
                    r = Math.Max(r, rootRadius + t * (nominalRadiusMm - rootRadius));
                }
                else if (z > zone.ToMm)
                {
                    var t = Math.Clamp((z - zone.ToMm) / runout, 0.0, 1.0);
                    r = Math.Max(r, rootRadius + t * (nominalRadiusMm - rootRadius));
                }
            }

            // مخروط (NPT/BSP): مقیاس نسبی شعاع با z.
            if (taperFactor > 0)
            {
                r *= (nominalRadiusMm + (z - zStartMm) * taperFactor) / nominalRadiusMm;
            }

            // پخ نوک ۴۵ درجه: از (zEnd − پخ) تا شعاع (اسمی − پخ) در نوک.
            if (tipChamferMm > 0 && z > zEndMm - tipChamferMm)
            {
                var cone = nominalRadiusMm - (z - (zEndMm - tipChamferMm));
                r = Math.Min(r, Math.Max(cone, 0.0));
            }

            // پخ سر ۴۵ درجه (میل دوطرفه).
            if (endChamferMm > 0 && z < zStartMm + endChamferMm)
            {
                var cone = nominalRadiusMm - (zStartMm + endChamferMm - z);
                r = Math.Min(r, Math.Max(cone, 0.0));
            }

            return Math.Max(r, 0.0);
        };
    }

    /// <summary>
    /// ساق رزوه‌دار کامل را می‌سازد: سطح جانبی یکپارچه + درپوش‌های دلخواه دو سر.
    /// </summary>
    /// <param name="mesh">مش مقصد.</param>
    /// <param name="spec">مشخصات رزوه.</param>
    /// <param name="profile">پروفیل تناوبی.</param>
    /// <param name="zStartMm">آغاز ساق.</param>
    /// <param name="zEndMm">انتهای ساق (نوک).</param>
    /// <param name="nominalRadiusMm">شعاع اسمی ساق.</param>
    /// <param name="zones">نواحی رزوهٔ کامل.</param>
    /// <param name="tipChamferMm">پخ نوک (۰ = بدون پخ).</param>
    /// <param name="endChamferMm">پخ سر در zStart (۰ = بدون پخ).</param>
    /// <param name="grid">شبکهٔ زاویه‌ای مشترک.</param>
    /// <param name="zStepMm">گام z شبکه.</param>
    /// <param name="capStart">درپوش در zStart (زیر سرِ پیچ لازم نیست؛ حلقهٔ سر می‌پوشاند).</param>
    /// <param name="capEnd">درپوش نوک.</param>
    public static void AddThreadedShank(
        MeshBuilder mesh,
        ThreadSpec spec,
        RadialProfileFunction profile,
        double zStartMm,
        double zEndMm,
        double nominalRadiusMm,
        ThreadZone[] zones,
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
        ArgumentNullException.ThrowIfNull(zones);
        if (zEndMm <= zStartMm) throw new ArgumentException("انتهای ساق باید بزرگ‌تر از آغاز آن باشد.");

        var radiusAt = CreateShankRadiusFunction(
            spec, profile, nominalRadiusMm, zones, zStartMm, zEndMm, tipChamferMm, endChamferMm);

        // سطوح ویژهٔ z: مرزهای نواحی/گذارها و پخ‌ها تا مرزهای تیز روی رأس شبکه بیفتند.
        var runout = RunoutLength(spec);
        var special = new List<double> { zStartMm, zEndMm };
        foreach (var zone in zones)
        {
            special.Add(zone.FromMm);
            special.Add(zone.ToMm);
            special.Add(zone.FromMm - runout);
            special.Add(zone.ToMm + runout);
        }
        if (tipChamferMm > 0) special.Add(zEndMm - tipChamferMm);
        if (endChamferMm > 0) special.Add(zStartMm + endChamferMm);

        var zLevels = BuildZLevels(zStartMm, zEndMm, zStepMm, [.. special]);

        RadialSurface.AddOutwardSurface(mesh, grid, radiusAt, zLevels);

        if (capStart)
            RadialSurface.AddDiscCap(mesh, grid, theta => radiusAt(theta, zStartMm), zStartMm, solidBelow: false);

        if (capEnd)
            RadialSurface.AddDiscCap(mesh, grid, theta => radiusAt(theta, zEndMm), zEndMm, solidBelow: true);
    }

    /// <summary>
    /// حفرهٔ رزوهٔ داخلی مهره را می‌سازد — سطح شعاعی با نرمال رو به محور + پخ دهانه.
    /// </summary>
    /// <param name="mesh">مش مقصد.</param>
    /// <param name="spec">مشخصات رزوهٔ داخلی.</param>
    /// <param name="profile">پروفیل داخلی (دره روی دیواره، نوک به محور).</param>
    /// <param name="zBottomMm">کف مهره (۰).</param>
    /// <param name="zTopMm">سطح بالای مهره.</param>
    /// <param name="mouthChamferMm">پخ دهانهٔ دو سر حفره.</param>
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

            // پخ دهانه: حفره در دو سر با مخروط ۴۵ درجه باز می‌شود (کمک به شروع پیچش و چاپ).
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
            [zBottomMm + mouthChamferMm, zTopMm - mouthChamferMm]);

        RadialSurface.AddInwardSurface(mesh, grid, RadiusAt, zLevels);
    }

    /// <summary>
    /// سطوح z شبکه را می‌سازد: یکنواخت + سطوح ویژه مرتب و بدون تکرار.
    /// </summary>
    private static List<double> BuildZLevels(double zStart, double zEnd, double zStep, double[] specialLevels)
    {
        var step = Math.Max(zStep, 1e-4);
        var count = (int)Math.Ceiling((zEnd - zStart) / step);
        var levels = new List<double>(count + 12);

        for (var i = 0; i <= count; i++)
        {
            levels.Add(Math.Min(zStart + i * step, zEnd));
        }

        levels.AddRange(specialLevels.Where(s => s > zStart + 1e-9 && s < zEnd - 1e-9));
        levels.Sort();

        // حذف تکراری‌های نزدیک که وجه بی‌مساحت می‌سازند.
        var result = new List<double>(levels.Count) { levels[0] };
        foreach (var z in levels)
        {
            if (z - result[^1] > 1e-7)
                result.Add(z);
        }

        if (result[^1] < zEnd - 1e-9)
            result.Add(zEnd);

        return result;
    }
}

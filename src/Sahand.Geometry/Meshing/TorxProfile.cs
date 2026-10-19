namespace Sahand.Geometry.Meshing;

/// <summary>
/// پروفیل شش‌پر تورکس (hexalobular — بر پایهٔ ISO 10664) به‌صورت تابع شعاع r(θ).
/// </summary>
/// <remarks>
/// ساخت هندسی: ۶ کمانِ محدبِ پَر (شعاع Re، مرکز در فاصلهٔ C روی زوایای k·۶۰°) و
/// ۶ کمانِ مقعرِ دره (شعاع Ri، مرکز در فاصلهٔ Dv روی زوایای ۳۰°+k·۶۰°) که به‌صورت
/// خارجی مماس‌اند؛ مرز ناحیه star-شکل است و r(θ) تک‌مقداری در می‌آید:
/// در محدودهٔ پَر = دورترین تقاطع پرتو با دایرهٔ پَر؛ در محدودهٔ دره = نزدیک‌ترین
/// تقاطع با دایرهٔ دره. زاویهٔ گذار، جهتِ نقطهٔ مماس از مرکز است.
/// نسبت‌های پروفایل: Re = 0.155·E، C = E/2 − Re، عمق دره B ≈ 0.72·E و Ri ≈ 0.05·E —
/// تقریب مهندسیِ شکل تجاری تورکس، نه انطباق رسمی استاندارد؛ گارد‌های سازنده اندازهٔ
/// نامعقول را رد می‌کنند. برای محاسبه دقیق مماس‌بودن، Ri از حل معادلهٔ فاصلهٔ مماس
/// خارجی به دست می‌آید (فاکتورهای ثابت پیش‌محاسبه‌شده).
/// </remarks>
public static class TorxProfile
{
    // نسبت‌های پروفایل (بر پایهٔ E = قطر اسمی روی پَرها).
    private const double LobeRadiusFactor = 0.155;        // Re/E
    private const double ValleyDepthFactor = 0.36;        // B/2 نسبت به E (B ≈ 0.72E)
    private const double LobeCenterFactor = 0.5 - LobeRadiusFactor; // C/E

    /// <summary>شعاع پروفیل تورکس در زاویهٔ <paramref name="theta"/> (رادیان، هر دور).</summary>
    /// <param name="nominalAcrossLobesMm">قطر اسمی E — فاصلهٔ قطری روی پَرها.</param>
    /// <param name="theta">زاویهٔ قطبی از محور (رادیان).</param>
    /// <exception cref="ArgumentException">اگر قطر اسمی نامعتبر باشد.</exception>
    public static double RadiusAt(double nominalAcrossLobesMm, double theta)
    {
        if (nominalAcrossLobesMm <= 0)
            throw new ArgumentException("قطر اسمی تورکس باید مثبت باشد.");

        var e = nominalAcrossLobesMm;
        var lobeR = LobeRadiusFactor * e;                 // Re
        var lobeC = LobeCenterFactor * e;                 // C
        var (valleyD, valleyR, transition) = ValleyGeometry(e, lobeR, lobeC);

        // زاویهٔ محلی در بازهٔ [۰، ۶۰°) — پَر روی ۰ (و ۶۰) و دره روی ۳۰.
        var sector = theta % (Math.PI / 3.0);
        if (sector < 0) sector += Math.PI / 3.0;

        var local = Math.Min(sector, Math.PI / 3.0 - sector); // فاصلهٔ زاویه‌ای تا نزدیک‌ترین پَر
        var delta = Math.Abs(sector - Math.PI / 6.0);         // فاصله تا دره

        if (local <= transition)
        {
            // محدودهٔ پَر: دورترین تقاطع پرتو با دایرهٔ پَر.
            var sin = lobeC * Math.Sin(local);
            if (sin >= lobeR) return valleyD - valleyR; // پَر خیلی باریک — عمق دره
            return lobeC * Math.Cos(local) + Math.Sqrt(lobeR * lobeR - sin * sin);
        }

        // محدودهٔ دره: نزدیک‌ترین تقاطع با دایرهٔ دره (کمان مقعر).
        var sinV = valleyD * Math.Sin(delta);
        if (sinV >= valleyR) return valleyD - valleyR;
        return valleyD * Math.Cos(delta) - Math.Sqrt(valleyR * valleyR - sinV * sinV);
    }

    /// <summary>
    /// زوایای ویژهٔ شبکهٔ زاویه‌ای — گذارهای پَر/دره و مراکز پَر و دره (۱۲+۱۲ زاویه در دور کامل).
    /// </summary>
    /// <param name="nominalAcrossLobesMm">قطر اسمی E.</param>
    public static IEnumerable<double> SpecialAngles(double nominalAcrossLobesMm)
    {
        var e = nominalAcrossLobesMm;
        var lobeR = LobeRadiusFactor * e;
        var lobeC = LobeCenterFactor * e;
        var (_, _, transition) = ValleyGeometry(e, lobeR, lobeC);

        for (var k = 0; k < 6; k++)
        {
            var lobeCenter = k * Math.PI / 3.0;
            yield return Norm(lobeCenter);                        // مرکز پَر (اوج شعاع)
            yield return Norm(lobeCenter + transition);           // گذار صعودی به دره
            yield return Norm(lobeCenter - transition);           // گذار نزولی به دره
            yield return Norm(lobeCenter + Math.PI / 6.0);        // مرکز دره (کف شعاع)
        }
    }

    /// <summary>قطر عبور از دره‌ها (B ≈ ۰٫۷۲E) — برای گاردهای اعتبارسنجی.</summary>
    public static double AcrossValleysMm(double nominalAcrossLobesMm) =>
        nominalAcrossLobesMm * 2.0 * ValleyDepthFactor;

    /// <summary>
    /// هندسهٔ دره: مرکز Dv و شعاع Ri از حل مماس‌بودن خارجی دایرهٔ دره با دو دایرهٔ پَر مجاور.
    /// </summary>
    /// <remarks>
    /// معادله: |L−V| = Re + Ri با L=(C،۰°)، V=(Dv،۳۰°) و قید Dv − Ri = B/2.
    /// حل جبری: C² − 2·C·Dv·cos30° = (Dv − B/2 − Re)² − Dv² پس از بسط ساده می‌شود
    /// به معادلهٔ خطی در Dv؛ زاویهٔ گذار هم از جهتِ نقطهٔ مماس به دست می‌آید.
    /// </remarks>
    private static (double ValleyCenter, double ValleyRadius, double TransitionAngle) ValleyGeometry(
        double e, double lobeR, double lobeC)
    {
        var halfValley = ValleyDepthFactor * e; // B/2

        // حل خطی: C² + Dv² − 2·C·Dv·cos30 = (Dv − m)² با m = B/2 − Re و Ri = Dv − B/2:
        // Dv = (C² − m²) / (2·(C·cos30 − m)) — مخرج با نسبت‌های ثابت همیشه مثبت است.
        var m = halfValley - lobeR;
        var cos30 = Math.Cos(Math.PI / 6.0);
        var denominator = 2.0 * (lobeC * cos30 - m);
        var valleyD = denominator > 1e-12
            ? (lobeC * lobeC - m * m) / denominator
            : halfValley + 0.05 * e; // حالت انحطاطی: درهٔ تقریبی تیز

        var valleyR = Math.Max(valleyD - halfValley, 0.015 * e);

        // نقطهٔ مماس بین دایرهٔ پَر (مرکز L روی زاویهٔ ۰) و دایرهٔ دره (مرکز V روی ۳۰°).
        var lx = lobeC;
        var vx = valleyD * cos30;
        var vy = valleyD * 0.5;
        var dx = vx - lx;
        var dy = vy;
        var dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < 1e-12)
            return (valleyD, valleyR, Math.PI / 12.0);

        var scale = lobeR / dist;
        var tx = lx + scale * dx;
        var ty = scale * dy;
        var transition = Math.Atan2(Math.Abs(ty), Math.Abs(tx));

        // گارد: زاویهٔ گذار باید پیش از مرکز دره (۳۰°) باشد؛ وگرنه پروفایل به‌هم می‌ریزد.
        if (transition >= Math.PI / 6.0 - 1e-6 || transition < 1e-6)
            transition = Math.PI / 12.0;

        return (valleyD, valleyR, transition);
    }

    private static double Norm(double angle)
    {
        var a = angle % (2.0 * Math.PI);
        if (a < 0) a += 2.0 * Math.PI;
        return a;
    }
}

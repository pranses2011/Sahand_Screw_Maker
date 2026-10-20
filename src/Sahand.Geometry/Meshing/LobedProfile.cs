using Sahand.Core.Parts;

namespace Sahand.Geometry.Meshing;

/// <summary>
/// مشخصات پروفیل گلبرگی — تعمیم الگوی تورکس (ISO 10664) به N پر (گسترش ۰٫۵٫۰).
/// </summary>
/// <param name="NominalAcrossLobesMm">قطر اسمی E — فاصلهٔ قطری روی پَرها.</param>
/// <param name="LobeCount">تعداد پَرها (تورکس ۶، پنتالوب ۵، تری‌وینگ ۳، تورک‌ست ۴).</param>
/// <param name="LobeRadiusFactor">Re/E — شعاع نسبی کمان پَر.</param>
/// <param name="ValleyDepthFactor">B/2 نسبت به E — نصف قطر عبور از دره‌ها.</param>
/// <param name="LobeTiltRadians">کج‌شدگی متناوب پَرها (تورک‌ست) — صفر برای بقیه.</param>
public readonly record struct LobeSpec(
    double NominalAcrossLobesMm,
    int LobeCount,
    double LobeRadiusFactor,
    double ValleyDepthFactor,
    double LobeTiltRadians = 0.0);

/// <summary>
/// پروفیل N-پر گلبرگی به‌صورت تابع شعاع r(θ) — زیرساخت مشترک تورکس/پنتالوب/تری‌وینگ/تورک‌ست.
/// </summary>
/// <remarks>
/// ساخت هندسی (همان مدل TorxProfile، تعمیم‌یافته): N کمان محدب پَر (شعاع Re، مرکز در
/// فاصلهٔ C روی زوایای k·۲π/N) و N کمان مقعر دره (شعاع Ri، مرکز در فاصلهٔ Dv روی
/// زوایای (k+½)·۲π/N) که به‌صورت خارجی مماس‌اند؛ مرز star-شکل است و r(θ) تک‌مقداری است.
/// کج‌شدگی تورک‌ست: پَرهای زوج/فرد به‌اندازهٔ ±Tilt چرخیده‌اند تا صلیبِ آفست ساخته شود.
/// نسبت‌های هر محرک «تقریب مهندسیِ» شکل تجاری است، نه انطباق رسمی استاندارد؛ گاردهای
/// سازنده اندازهٔ نامعقول را رد می‌کنند.
/// </remarks>
public static class LobedProfile
{
    /// <summary>پروفیل تورکس شش‌پر — معادل TorxProfile (نگه‌داشته برای سازگاری).</summary>
    public static LobeSpec Torx(double nominalAcrossLobesMm) =>
        new(nominalAcrossLobesMm, 6, 0.155, 0.36);

    /// <summary>پروفیل پنتالوب پنج‌پر — گلبرگ‌های گردتر و دره‌های بازتر.</summary>
    public static LobeSpec Pentalobe(double nominalAcrossLobesMm) =>
        new(nominalAcrossLobesMm, 5, 0.21, 0.33);

    /// <summary>پروفیل تری‌وینگ سه‌پر — بال‌های پهن با دره‌های کم‌عمق.</summary>
    public static LobeSpec TriWing(double nominalAcrossLobesMm) =>
        new(nominalAcrossLobesMm, 3, 0.24, 0.30);

    /// <summary>پروفیل تورک‌ست چهارپر — صلیب آفست با کج‌شدگی متناوب ±۱۰°.</summary>
    public static LobeSpec TorqSet(double nominalAcrossLobesMm) =>
        new(nominalAcrossLobesMm, 4, 0.16, 0.34, LobeTiltRadians: 10.0 * Math.PI / 180.0);

    /// <summary>
    /// مشخصات محرک گلبرگی از نوع <paramref name="drive"/> می‌سازد؛ null برای محرک‌های غیرگلبرگی.
    /// </summary>
    public static LobeSpec? ForDrive(DriveStyle drive, double nominalAcrossLobesMm) => drive switch
    {
        DriveStyle.Torx or DriveStyle.SecurityTorx => Torx(nominalAcrossLobesMm),
        DriveStyle.Pentalobe => Pentalobe(nominalAcrossLobesMm),
        DriveStyle.TriWing => TriWing(nominalAcrossLobesMm),
        DriveStyle.TorqSet => TorqSet(nominalAcrossLobesMm),
        _ => null,
    };

    /// <summary>شعاع پروفیل در زاویهٔ <paramref name="theta"/> (رادیان، هر دور).</summary>
    public static double RadiusAt(LobeSpec spec, double theta)
    {
        if (spec.NominalAcrossLobesMm <= 0)
            throw new ArgumentException("قطر اسمی محرک گلبرگی باید مثبت باشد.");
        if (spec.LobeCount is < 3 or > 12)
            throw new ArgumentException("تعداد پَرها باید بین ۳ و ۱۲ باشد.");

        var period = 2.0 * Math.PI / spec.LobeCount;
        var e = spec.NominalAcrossLobesMm;
        var lobeR = spec.LobeRadiusFactor * e;
        var lobeC = e / 2.0 - lobeR;
        var (valleyD, valleyR, transition) = ValleyGeometry(spec, lobeR, lobeC);

        // بازهٔ فعلی و کج‌شدگی متناوب پَر (تورک‌ست).
        var rawSector = theta % period;
        if (rawSector < 0) rawSector += period;
        var k = (int)Math.Floor(theta / period);
        if (theta < 0) k--; // correcting floor for negative angles
        var tilt = spec.LobeTiltRadians * (Math.Abs(k % 2L) == 1 ? -1.0 : 1.0);

        // زاویهٔ محلی نسبت به مرکز پَرِ این بازه — نرمال به [−period/۲، period/۲].
        var local = rawSector - tilt;
        if (local > period / 2.0) local -= period;
        else if (local < -period / 2.0) local += period;

        var distanceToLobe = Math.Abs(local);
        var distanceToValley = period / 2.0 - distanceToLobe;

        if (distanceToLobe <= transition)
        {
            // محدودهٔ پَر: دورترین تقاطع پرتو با دایرهٔ پَر.
            var sin = lobeC * Math.Sin(distanceToLobe);
            if (sin >= lobeR) return valleyD - valleyR;
            return lobeC * Math.Cos(distanceToLobe) + Math.Sqrt(lobeR * lobeR - sin * sin);
        }

        // محدودهٔ دره: نزدیک‌ترین تقاطع با دایرهٔ دره (کمان مقعر).
        var sinV = valleyD * Math.Sin(distanceToValley);
        if (sinV >= valleyR) return valleyD - valleyR;
        return valleyD * Math.Cos(distanceToValley) - Math.Sqrt(valleyR * valleyR - sinV * sinV);
    }

    /// <summary>
    /// زوایای ویژهٔ شبکهٔ زاویه‌ای — مراکز پَر/دره و گذارهای هر بازه (۴×N زاویه در دور کامل).
    /// </summary>
    public static IEnumerable<double> SpecialAngles(LobeSpec spec)
    {
        if (spec.NominalAcrossLobesMm <= 0 || spec.LobeCount is < 3 or > 12)
            yield break;

        var period = 2.0 * Math.PI / spec.LobeCount;
        var e = spec.NominalAcrossLobesMm;
        var lobeR = spec.LobeRadiusFactor * e;
        var lobeC = e / 2.0 - lobeR;
        var (_, _, transition) = ValleyGeometry(spec, lobeR, lobeC);

        for (var k = 0; k < spec.LobeCount; k++)
        {
            var tilt = spec.LobeTiltRadians * (k % 2 == 1 ? -1.0 : 1.0);
            var lobeCenter = k * period + tilt;
            yield return Norm(lobeCenter);                  // مرکز پَر (اوج شعاع)
            yield return Norm(lobeCenter + transition);     // گذار صعودی به دره
            yield return Norm(lobeCenter - transition);     // گذار نزولی به دره
            yield return Norm(lobeCenter + period / 2.0);   // مرکز دره (کف شعاع)
        }
    }

    /// <summary>قطر عبور از دره‌ها (B ≈ ۲×ValleyDepthFactor) — برای گاردهای اعتبارسنجی.</summary>
    public static double AcrossValleysMm(LobeSpec spec) =>
        spec.NominalAcrossLobesMm * 2.0 * spec.ValleyDepthFactor;

    /// <summary>
    /// هندسهٔ دره: مرکز Dv و شعاع Ri از حل مماس‌بودن خارجی دایرهٔ دره با دو دایرهٔ پَر مجاور.
    /// </summary>
    /// <remarks>
    /// معادلهٔ تعمیم‌یافتهٔ TorxProfile با زاویهٔ میانی π/N به‌جای ۳۰°:
    /// C² − ۲·C·Dv·cos(π/N) = (Dv − m)² − Dv² با m = B/2 − Re و Ri = Dv − B/2؛
    /// پس از بسط به معادلهٔ خطی در Dv می‌رسد.
    /// </remarks>
    private static (double ValleyCenter, double ValleyRadius, double TransitionAngle) ValleyGeometry(
        LobeSpec spec, double lobeR, double lobeC)
    {
        var e = spec.NominalAcrossLobesMm;
        var halfValley = spec.ValleyDepthFactor * e;
        var mid = Math.PI / spec.LobeCount;

        var m = halfValley - lobeR;
        var cosMid = Math.Cos(mid);
        var denominator = 2.0 * (lobeC * cosMid - m);
        var valleyD = denominator > 1e-12
            ? (lobeC * lobeC - m * m) / denominator
            : halfValley + 0.05 * e; // حالت انحطاطی: درهٔ تقریبی تیز

        var valleyR = Math.Max(valleyD - halfValley, 0.015 * e);

        // نقطهٔ مماس بین دایرهٔ پَر (مرکز روی زاویهٔ ۰) و دایرهٔ دره (مرکز روی mid).
        var lx = lobeC;
        var vx = valleyD * cosMid;
        var vy = valleyD * Math.Sin(mid);
        var dx = vx - lx;
        var dy = vy;
        var dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < 1e-12)
            return (valleyD, valleyR, mid / 2.0);

        var scale = lobeR / dist;
        var tx = lx + scale * dx;
        var ty = scale * dy;
        var transition = Math.Atan2(Math.Abs(ty), Math.Abs(tx));

        // گارد: زاویهٔ گذار باید پیش از مرکز دره باشد؛ وگرنه پروفایل به‌هم می‌ریزد.
        if (transition >= mid - 1e-6 || transition < 1e-6)
            transition = mid / 2.0;

        return (valleyD, valleyR, transition);
    }

    private static double Norm(double angle)
    {
        var a = angle % (2.0 * Math.PI);
        if (a < 0) a += 2.0 * Math.PI;
        return a;
    }
}

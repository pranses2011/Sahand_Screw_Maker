using Sahand.Core.Parts;
using Sahand.Core.Threads;
using Sahand.Geometry.Meshing;

namespace Sahand.Geometry.Builders;

/// <summary>
/// سازندهٔ مش پیچ — ترکیب ساق رزوه‌دار، سر (شش‌گوش/آلن/خزینه/نیم‌گرد) و محرک.
/// </summary>
/// <remarks>
/// قرارداد محور (سند ۷-۵): z=۰ روی سطح اتکای سر؛ سر در z منفی؛ ساق در z ∈ [۰، L].
/// همهٔ اجزا روی «یک» شبکهٔ زاویه‌ای مشترک ساخته می‌شوند (با زوایای ویژهٔ گوشه‌های شش‌گوش
/// و خروج شیار) تا یال‌های مشترک دقیق و مش watertight بماند.
/// محدودهٔ پشتیبانی محرک در هر سر در PartValidator و ماتریس قابلیت مستند شده است؛
/// ترکیب‌های پشتیبانی‌نشده با پیام فارسی رد می‌شوند (نه دکمهٔ بی‌عمل).
/// </remarks>
public static class BoltBuilder
{
    /// <summary>
    /// مش کامل پیچ را می‌سازد.
    /// </summary>
    /// <param name="spec">مشخصات معتبر پیچ (با PartValidator آزموده‌شده).</param>
    /// <param name="quality">preset کیفیت مش.</param>
    /// <exception cref="ArgumentException">اگر ترکیب سر/محرک پشتیبانی نشده یا ابعاد ناسازگار باشد.</exception>
    public static Mesh Build(BoltSpec spec, MeshQualityPreset quality)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var thread = spec.Thread;
        var profile = ThreadProfileFactory.BuildProfile(thread);
        var nominalR = thread.NominalDiameterMm / 2.0;

        // پروفیل «بدون رزوهٔ واقعی»: تابع ثابت به اندازهٔ شعاع اسمی — برای حالت نمایشی.
        if (thread.ModelingMode == ThreadModelingMode.Simplified)
        {
            profile = new RadialProfileFunction(
                [new ThreadProfilePoint(nominalR, 0), new ThreadProfilePoint(nominalR, thread.LeadMm)]);
        }

        var zStep = MeshQuality.ZStepFor(quality, thread.PitchMm);
        var angularCount = MeshQuality.AngularSegmentsFor(quality, thread.LeadMm, zStep);
        var length = spec.LengthUnderHeadMm;
        var topZ = -spec.HeadHeightMm;

        // زوایای ویژهٔ شبکه: گوشه‌های شش‌گوش سر/آلن + خروج شیار.
        var specialAngles = new List<double>();
        if (spec.Head is HeadStyle.Hex && spec.HeadAcrossFlatsMm is > 0)
            specialAngles.AddRange(HexCornerAngles());
        if (spec.Drive == DriveStyle.HexSocket && spec.HexSocketAcrossFlatsMm is > 0)
            specialAngles.AddRange(HexCornerAngles());
        if (spec.Drive is DriveStyle.Slot or DriveStyle.Phillips)
        {
            var headR = HeadFlatTopRadius(spec);
            var w = spec.SlotWidthMm ?? throw new ArgumentException(
                "برای محرک شیاری، پهنای شیار لازم است.");
            specialAngles.AddRange(SlotCutter.GetSpecialAngles(headR, w, spec.Drive == DriveStyle.Phillips));
        }

        var grid = AngularGrid.Create(angularCount, specialAngles);
        var mesh = new MeshBuilder();

        // --- ساق رزوه‌دار ---
        var threadLength = spec.ThreadRunoutFromTipMm ?? length;
        var threadStart = length - threadLength;
        var zones = new[] { new ThreadedSurface.ThreadZone(threadStart, length) };

        var shankRings = ThreadedSurface.AddThreadedShank(
            mesh, thread, profile,
            zStartMm: 0, zEndMm: length,
            nominalRadiusMm: nominalR,
            zones,
            tipChamferMm: spec.TipChamferMm,
            endChamferMm: 0,
            grid, zStep,
            capStart: false,   // زیر سر: حلقهٔ اتکای سر می‌پوشاند.
            capEnd: true);

        // --- سر ---
        switch (spec.Head)
        {
            case HeadStyle.Hex:
                BuildHexHead(mesh, spec, grid, topZ, shankRings[0]);
                break;
            case HeadStyle.SocketCap:
                BuildSocketCapHead(mesh, spec, grid, topZ, shankRings[0], quality);
                break;
            case HeadStyle.FlatCountersunk:
                BuildCountersunkHead(mesh, spec, grid, topZ, shankRings[0]);
                break;
            case HeadStyle.Pan:
                BuildPanHead(mesh, spec, grid, topZ, shankRings[0]);
                break;
            default:
                throw new ArgumentException($"سر «{spec.Head}» در این نسخه پیاده‌سازی نشده است.");
        }

        return mesh.Build();
    }

    /// <summary>شعاع رویهٔ تختِ سر برای ماشین‌آلات شیار.</summary>
    private static double HeadFlatTopRadius(BoltSpec spec) => spec.Head switch
    {
        HeadStyle.SocketCap or HeadStyle.Pan => (spec.HeadDiameterMm ?? 0) / 2.0,
        _ => throw new ArgumentException("محرک شیاری فقط روی سر با رویهٔ تختِ دایره‌ای پشتیبانی می‌شود."),
    };

    /// <summary>زوایای گوشه‌های شش‌گوش: ۳۰° + k·۶۰° (تخت‌ها روی مضرب‌های ۶۰°).</summary>
    private static IEnumerable<double> HexCornerAngles()
    {
        for (var k = 0; k < 6; k++)
            yield return Math.PI / 6.0 + k * Math.PI / 3.0;
    }

    /// <summary>شعاع شش‌گوش در زاویهٔ θ — تخت‌ها روی k·۶۰°، گوشه‌ها روی ۳۰°+k·۶۰°.</summary>
    private static double HexRadius(double acrossFlatsMm, double theta)
    {
        var sector = theta % (Math.PI / 3.0);
        if (sector < 0) sector += Math.PI / 3.0;
        return acrossFlatsMm / 2.0 / Math.Cos(sector - Math.PI / 6.0);
    }

    /// <summary>سر شش‌گوش با پخ فوقانی و حفرهٔ آلن اختیاری.</summary>
    private static void BuildHexHead(
        MeshBuilder mesh,
        BoltSpec spec,
        AngularGrid grid,
        double topZ,
        int[] shankBottomRing)
    {
        var af = spec.HeadAcrossFlatsMm
            ?? throw new ArgumentException("سر شش‌گوش بدون عرض آچارخور قابل ساخت نیست.");
        var chamfer = Math.Min(spec.HeadTopChamferMm, spec.HeadHeightMm / 2.0);
        var hasSocket = spec.Drive == DriveStyle.HexSocket;

        // پخ فوقانی ۴۵ درجه: در z ∈ [topZ, topZ+پخ] شعاع به‌اندازهٔ فاصله تا topZ کم می‌شود.
        double RadiusAt(double theta, double z) =>
            HexRadius(af, theta) - Math.Max(0.0, (topZ + chamfer) - z);

        var levels = DedupLevels(topZ, topZ + chamfer, 0.0);
        var headRings = RadialSurface.AddOutwardSurface(
            mesh, grid, RadiusAt, levels,
            capFirst: !hasSocket, // بدون حفره: درپوش کامل رویهٔ بالا.
            capLast: false);

        if (hasSocket)
        {
            var socketAf = spec.HexSocketAcrossFlatsMm
                ?? throw new ArgumentException("حفرهٔ آلن بدون قطر قابل ساخت نیست.");
            var socketDepth = spec.HexSocketDepthMm!.Value;
            if (socketDepth >= spec.HeadHeightMm)
                throw new ArgumentException("عمق حفرهٔ آلن باید کمتر از ارتفاع سر باشد.");

            // گارد هندسی: گوشهٔ حفره باید داخل مرزِ پخ‌خوردهٔ سر جا شود وگرنه درپوش حلقه‌ای معکوس می‌شود.
            if (socketAf / Math.Sqrt(3.0) >= af / 2.0 - chamfer)
                throw new ArgumentException(
                    "حفرهٔ آلن از مرز داخلی سر شش‌گوش بزرگ‌تر است؛ قطر حفره را کم یا عرض آچارخور را زیاد کنید.");

            double SocketRadius(double theta, double z) => HexRadius(socketAf, theta);
            var socketRings = RadialSurface.AddInwardSurface(
                mesh, grid, SocketRadius,
                DedupLevels(topZ, topZ + socketDepth),
                capFirst: false,
                capLast: true); // کف حفره: نرمال −Z (ماده در z بزرگ‌تر).

            // رویهٔ بالای سر: حلقه بین حفرهٔ آلن و مرز شش‌گوشِ پخ‌خورده — نرمال −Z.
            RadialSurface.AddRingCap(mesh, socketRings[0], headRings[0], solidBelow: false);
        }

        // سطح اتکای سر: حلقه بین مرز ساق و مرز شش‌گوش در z=0.
        RadialSurface.AddRingCap(mesh, shankBottomRing, headRings[^1], solidBelow: true);
    }

    /// <summary>سر استوانه‌ای آلن (Socket Cap) با حفرهٔ شش‌گوش یا شیار/فیلیپس.</summary>
    private static void BuildSocketCapHead(
        MeshBuilder mesh,
        BoltSpec spec,
        AngularGrid grid,
        double topZ,
        int[] shankBottomRing,
        MeshQualityPreset quality)
    {
        var headR = (spec.HeadDiameterMm ?? 0) / 2.0;
        if (headR <= 0) throw new ArgumentException("قطر سر آلن باید مثبت باشد.");

        // با محرک شیاری، کل دیواره و رویه را ماشین‌آلات شیار می‌سازد (بدون پخ بالایی).
        if (spec.Drive is DriveStyle.Slot or DriveStyle.Phillips)
        {
            var slotWidth = spec.SlotWidthMm!.Value;
            var slotDepth = spec.SlotDepthMm!.Value;
            var ringSide = SlotCutter.Build(
                mesh, grid, headR,
                sideFromZ: 0.0, topZ,
                slotWidth, slotDepth,
                phillips: spec.Drive == DriveStyle.Phillips);

            RadialSurface.AddRingCap(mesh, shankBottomRing, ringSide, solidBelow: true);
            return;
        }

        var chamfer = Math.Min(spec.HeadTopChamferMm, spec.HeadHeightMm / 2.0);
        double RadiusAt(double theta, double z) => headR - Math.Max(0.0, (topZ + chamfer) - z);

        var headRings = RadialSurface.AddOutwardSurface(
            mesh, grid, RadiusAt,
            DedupLevels(topZ, topZ + chamfer, 0.0),
            capFirst: spec.Drive != DriveStyle.HexSocket, // بدون حفره: درپوش کامل.
            capLast: false);

        if (spec.Drive == DriveStyle.HexSocket)
        {
            var socketAf = spec.HexSocketAcrossFlatsMm!.Value;
            var socketDepth = spec.HexSocketDepthMm!.Value;
            if (socketDepth >= spec.HeadHeightMm)
                throw new ArgumentException("عمق حفرهٔ آلن باید کمتر از ارتفاع سر باشد.");

            double SocketRadius(double theta, double z) => HexRadius(socketAf, theta);
            var socketRings = RadialSurface.AddInwardSurface(
                mesh, grid, SocketRadius,
                DedupLevels(topZ, topZ + socketDepth),
                capFirst: false,
                capLast: true);

            // رویهٔ بالای سر: حلقه بین حفرهٔ آلن و مرز (پخ‌خورده) سر — نرمال −Z.
            RadialSurface.AddRingCap(mesh, socketRings[0], headRings[0], solidBelow: false);
        }

        RadialSurface.AddRingCap(mesh, shankBottomRing, headRings[^1], solidBelow: true);
    }

    /// <summary>سر خزینه مخروطی — فقط بدون محرک در این نسخه (محدودیت مستندشده).</summary>
    private static void BuildCountersunkHead(
        MeshBuilder mesh,
        BoltSpec spec,
        AngularGrid grid,
        double topZ,
        int[] shankBottomRing)
    {
        if (spec.Drive != DriveStyle.None)
            throw new ArgumentException(
                "محرک روی سر خزینه در این نسخه پشتیبانی نمی‌شود (در برنامهٔ توسعه).");

        var headR = (spec.HeadDiameterMm ?? 0) / 2.0;
        if (headR <= 0) throw new ArgumentException("قطر سر خزینه باید مثبت باشد.");

        var halfAngle = spec.CountersunkAngleDegrees / 2.0;
        var topRadius = headR - spec.HeadHeightMm * Math.Tan(halfAngle * Math.PI / 180.0);
        if (topRadius <= spec.Thread.NominalDiameterMm / 2.0)
            throw new ArgumentException(
                "ارتفاع سر خزینه نسبت به زاویه/قطر طوری است که رویهٔ بالایی از ساق کوچک‌تر می‌شود؛ قطر یا زاویه را اصلاح کنید.");

        // مخروط خطی از topRadius در topZ تا headR در z=0.
        double RadiusAt(double theta, double z)
        {
            var t = (z - topZ) / -topZ; // ۰ در topZ و ۱ در z=0.
            return topRadius + t * (headR - topRadius);
        }

        var headRings = RadialSurface.AddOutwardSurface(
            mesh, grid, RadiusAt,
            [topZ, 0.0],
            capFirst: true,
            capLast: false);

        RadialSurface.AddRingCap(mesh, shankBottomRing, headRings[^1], solidBelow: true);
    }

    /// <summary>سر نیم‌گرد (Pan) — گنبد ربع‌بیضی با پایهٔ استوانه‌ای؛ بدون محرک در این نسخه.</summary>
    private static void BuildPanHead(
        MeshBuilder mesh,
        BoltSpec spec,
        AngularGrid grid,
        double topZ,
        int[] shankBottomRing)
    {
        if (spec.Drive != DriveStyle.None)
            throw new ArgumentException(
                "محرک روی سر نیم‌گرد در این نسخه پشتیبانی نمی‌شود (در برنامهٔ توسعه).");

        var headR = (spec.HeadDiameterMm ?? 0) / 2.0;
        if (headR <= 0) throw new ArgumentException("قطر سر نیم‌گرد باید مثبت باشد.");

        var height = spec.HeadHeightMm;
        var domeStart = topZ + 0.25 * height; // پایین ۲۵٪ استوانه؛ بالای ۷۵٪ گنبد.
        var apexR = Math.Max(headR * 0.05, 0.1);

        // گنبد ربع‌بیضی: r(t) = apex + (R − apex)·sin(t·π/۲) — نمونه‌برداری چندخطی.
        var levels = new List<double> { topZ };
        const int domeSamples = 10;
        for (var i = 1; i <= domeSamples; i++)
        {
            var t = (double)i / domeSamples;
            levels.Add(topZ + t * (domeStart - topZ));
        }
        levels.Add(0.0);

        double RadiusAt(double theta, double z)
        {
            if (z <= domeStart)
            {
                var t = Math.Clamp((z - topZ) / (domeStart - topZ), 0.0, 1.0);
                return apexR + (headR - apexR) * Math.Sin(t * Math.PI / 2.0);
            }
            return headR;
        }

        var headRings = RadialSurface.AddOutwardSurface(
            mesh, grid, RadiusAt,
            DedupSorted(levels),
            capFirst: true,
            capLast: false);

        RadialSurface.AddRingCap(mesh, shankBottomRing, headRings[^1], solidBelow: true);
    }

    /// <summary>سطوح را مرتب و بدون تکرار نزدیک نگه می‌دارد.</summary>
    private static List<double> DedupLevels(params double[] levels) => DedupSorted(levels);

    private static List<double> DedupSorted(IEnumerable<double> levels)
    {
        var sorted = levels.OrderBy(x => x).ToList();
        var result = new List<double> { sorted[0] };
        foreach (var z in sorted)
        {
            if (z - result[^1] > 1e-9)
                result.Add(z);
        }
        return result;
    }
}

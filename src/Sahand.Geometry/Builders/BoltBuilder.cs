using Sahand.Core.Parts;
using Sahand.Core.Threads;
using Sahand.Geometry.Meshing;

namespace Sahand.Geometry.Builders;

/// <summary>
/// سازندهٔ مش پیچ — ترکیب ساق رزوه‌دار، سر (شش‌گوش/آلن/خزینه/نیم‌گرد/دکمه‌ای/فلنجی) و محرک.
/// </summary>
/// <remarks>
/// قرارداد محور (سند ۷-۵): z=۰ روی سطح اتکای سر؛ سر در z منفی؛ ساق در z ∈ [۰، L].
/// همهٔ اجزا روی «یک» شبکهٔ زاویه‌ای مشترک ساخته می‌شوند (با زوایای ویژهٔ گوشه‌های شش‌گوش
/// و خروج شیار) تا یال‌های مشترک دقیق و مش watertight بماند.
/// محدودهٔ پشتیبانی محرک در هر سر در PartValidator و ماتریس قابلیت مستند شده است؛
/// ترکیب‌های پشتیبانی‌نشده با پیام فارسی رد می‌شوند (نه دکمهٔ بی‌عمل).
/// گسترش ۰٫۴٫۰: سر دکمه‌ای (گنبد سهمی + پلتفرم قله) و سر فلنجی (دیسک + شش‌گوش) و
/// محرک‌های امنیتی پین‌دار (آلن/تورکس با پین مرکزی).
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
        var profile = ThreadProfileFactory.BuildProfile(thread).ToProfileFunction();
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

        // زوایای ویژهٔ شبکه: گوشه‌های شش‌گوش سر/آلن + خروج شیار + گذارهای تورکس.
        var specialAngles = new List<double>();
        if (spec.Head is (HeadStyle.Hex or HeadStyle.Flange) && spec.HeadAcrossFlatsMm is > 0)
            specialAngles.AddRange(HexCornerAngles());
        if (spec.Drive is (DriveStyle.HexSocket or DriveStyle.PinHex) && spec.HexSocketAcrossFlatsMm is > 0)
            specialAngles.AddRange(HexCornerAngles());
        if (spec.Drive is (DriveStyle.Torx or DriveStyle.SecurityTorx) && spec.TorxAcrossLobesMm is > 0)
            specialAngles.AddRange(TorxProfile.SpecialAngles(spec.TorxAcrossLobesMm.Value));
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
            case HeadStyle.Button:
                BuildButtonHead(mesh, spec, grid, topZ, shankRings[0]);
                break;
            case HeadStyle.Flange:
                BuildFlangeHead(mesh, spec, grid, topZ, shankRings[0]);
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

    /// <summary>محرک حفرهٔ آلن دارد (ساده یا امنیتی پین‌دار).</summary>
    private static bool HasHexSocketDrive(BoltSpec spec) =>
        spec.Drive is DriveStyle.HexSocket or DriveStyle.PinHex;

    /// <summary>محرک حفرهٔ تورکس دارد (ساده یا امنیتی پین‌دار).</summary>
    private static bool HasTorxDrive(BoltSpec spec) =>
        spec.Drive is DriveStyle.Torx or DriveStyle.SecurityTorx;

    /// <summary>محرک پین امنیتی مرکزی دارد.</summary>
    private static bool HasSecurityPin(BoltSpec spec) =>
        spec.Drive is DriveStyle.PinHex or DriveStyle.SecurityTorx;

    /// <summary>سر شش‌گوش با پخ فوقانی و حفرهٔ آلن/تورکس اختیاری (با پین امنیتی اختیاری).</summary>
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
        var hasSocket = HasHexSocketDrive(spec);
        var hasTorx = HasTorxDrive(spec);

        // پخ فوقانی ۴۵ درجه: در z ∈ [topZ, topZ+پخ] شعاع به‌اندازهٔ فاصله تا topZ کم می‌شود.
        double RadiusAt(double theta, double z) =>
            HexRadius(af, theta) - Math.Max(0.0, (topZ + chamfer) - z);

        var levels = DedupLevels(topZ, topZ + chamfer, 0.0);
        var headRings = RadialSurface.AddOutwardSurface(
            mesh, grid, RadiusAt, levels,
            capFirst: !hasSocket && !hasTorx, // بدون حفره: درپوش کامل رویهٔ بالا.
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
                capLast: !HasSecurityPin(spec)); // کف حفره: fan ساده یا حلقهٔ دور پین.

            // رویهٔ بالای سر: حلقه بین حفرهٔ آلن و مرز شش‌گوشِ پخ‌خورده — نرمال −Z.
            RadialSurface.AddRingCap(mesh, socketRings[0], headRings[0], solidBelow: false);

            if (HasSecurityPin(spec))
                AddSecurityPin(mesh, spec, grid, floorZ: topZ + socketDepth, topZ, socketRings[^1]);
        }

        if (hasTorx)
        {
            // گارد هندسی: پَرِ تورکس باید داخل مرزِ پخ‌خوردهٔ سر (کمینهٔ شعاع تخت) جا شود.
            var nominalE = spec.TorxAcrossLobesMm
                ?? throw new ArgumentException("حفرهٔ تورکس بدون قطر اسمی قابل ساخت نیست.");
            if (nominalE / 2.0 >= af / 2.0 - chamfer)
                throw new ArgumentException(
                    "حفرهٔ تورکس از مرز داخلی سر شش‌گوش بزرگ‌تر است؛ قطر تورکس را کم یا عرض آچارخور را زیاد کنید.");
            AddTorxPocket(mesh, spec, grid, topZ, headRings[0]);
        }

        // سطح اتکای سر: حلقه بین مرز ساق و مرز شش‌گوش در z=0.
        RadialSurface.AddRingCap(mesh, shankBottomRing, headRings[^1], solidBelow: true);
    }

    /// <summary>سر استوانه‌ای آلن (Socket Cap) با حفرهٔ شش‌گوش/تورکس یا شیار/فیلیپس.</summary>
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
            capFirst: spec.Drive is not (DriveStyle.HexSocket or DriveStyle.PinHex or
                                        DriveStyle.Torx or DriveStyle.SecurityTorx), // بدون حفره: درپوش کامل.
            capLast: false);

        if (HasHexSocketDrive(spec))
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
                capLast: !HasSecurityPin(spec));

            // رویهٔ بالای سر: حلقه بین حفرهٔ آلن و مرز (پخ‌خورده) سر — نرمال −Z.
            RadialSurface.AddRingCap(mesh, socketRings[0], headRings[0], solidBelow: false);

            if (HasSecurityPin(spec))
                AddSecurityPin(mesh, spec, grid, floorZ: topZ + socketDepth, topZ, socketRings[^1]);
        }

        if (HasTorxDrive(spec))
        {
            // گارد هندسی: پَرِ تورکس باید داخل مرز پخ‌خوردهٔ بالای سر جا شود.
            var nominalE = spec.TorxAcrossLobesMm
                ?? throw new ArgumentException("حفرهٔ تورکس بدون قطر اسمی قابل ساخت نیست.");
            if (nominalE / 2.0 >= headR - chamfer)
                throw new ArgumentException(
                    "حفرهٔ تورکس از مرز سر بزرگ‌تر است؛ قطر تورکس را کم یا قطر سر را زیاد کنید.");
            AddTorxPocket(mesh, spec, grid, topZ, headRings[0]);
        }

        RadialSurface.AddRingCap(mesh, shankBottomRing, headRings[^1], solidBelow: true);
    }

    /// <summary>
    /// حفرهٔ تورکس (شش‌پر) روی رویهٔ تختِ سر — همان الگوی حفرهٔ آلن با تابع شعاع تورکس.
    /// </summary>
    /// <remarks>گاردهای هندسی در PartValidator آمده‌اند؛ اینجا فقط گارد ساخت هست.
    /// رویهٔ بالای سر (حلقهٔ بیرونی) باید پیشاپیش ساخته شده باشد و حلقهٔ headTopRing آن را می‌بندد.</remarks>
    private static void AddTorxPocket(
        MeshBuilder mesh,
        BoltSpec spec,
        AngularGrid grid,
        double topZ,
        int[] headTopRing)
    {
        var nominalE = spec.TorxAcrossLobesMm
            ?? throw new ArgumentException("حفرهٔ تورکس بدون قطر اسمی قابل ساخت نیست.");
        var depth = spec.TorxDepthMm
            ?? throw new ArgumentException("حفرهٔ تورکس بدون عمق قابل ساخت نیست.");
        if (depth >= spec.HeadHeightMm)
            throw new ArgumentException("عمق حفرهٔ تورکس باید کمتر از ارتفاع سر باشد.");

        // دیواره‌های عمودی حفره + کف — تابع شعاع تورکس مستقل از z (دیوارهٔ عمودی).
        double TorxRadius(double theta, double z) => TorxProfile.RadiusAt(nominalE, theta);
        var pocketRings = RadialSurface.AddInwardSurface(
            mesh, grid, TorxRadius,
            DedupLevels(topZ, topZ + depth),
            capFirst: false,
            capLast: !HasSecurityPin(spec)); // کف: fan ساده یا حلقهٔ دور پین.

        // رویهٔ بالای سر: حلقه بین حفرهٔ تورکس و مرز سر — نرمال −Z.
        RadialSurface.AddRingCap(mesh, pocketRings[0], headTopRing, solidBelow: false);

        if (HasSecurityPin(spec))
            AddSecurityPin(mesh, spec, grid, floorZ: topZ + depth, topZ, pocketRings[^1]);
    }

    /// <summary>
    /// پین مرکزی محرک امنیتی (PinHex/SecurityTorx) — استوانهٔ ایستاده از کف حفره تا رویهٔ سر.
    /// </summary>
    /// <remarks>
    /// کف حفره را از fan به حلقهٔ دور پین تبدیل می‌کند (سازندهٔ حفره باید capLast را خاموش کرده باشد).
    /// بالای پین هم‌تراز رویهٔ سر با fan cap نرمال −Z بسته می‌شود.
    /// </remarks>
    private static void AddSecurityPin(
        MeshBuilder mesh,
        BoltSpec spec,
        AngularGrid grid,
        double floorZ,
        double topZ,
        int[] socketRingAtFloor)
    {
        var pinDiameter = spec.SecurityPinDiameterMm
            ?? throw new ArgumentException("محرک امنیتی پین‌دار بدون قطر پین قابل ساخت نیست.");
        var pinR = pinDiameter / 2.0;
        if (pinR <= 0) throw new ArgumentException("قطر پین امنیتی باید مثبت باشد.");

        // دیوارهٔ پین — سطح استوانه‌ای بیرونی، ترازهای صعودی [topZ, floorZ].
        double PinRadius(double theta, double z) => pinR;
        var pinRings = RadialSurface.AddOutwardSurface(
            mesh, grid, PinRadius,
            DedupLevels(topZ, floorZ),
            capFirst: false,
            capLast: false);

        // بالای پین: هم‌تراز رویهٔ سر (topZ) — نرمال −Z (ماده در z بزرگ‌تر یعنی بدنهٔ پین).
        RadialSurface.AddFanCap(mesh, pinRings[0], topZ, solidBelow: false);

        // کف حفره: حلقه بین پین و دیوارهٔ حفره در floorZ — نرمال −Z (ماده در z بزرگ‌تر).
        RadialSurface.AddRingCap(mesh, pinRings[^1], socketRingAtFloor, solidBelow: false);
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

    /// <summary>
    /// سر دکمه‌ای (Button) — گنبد سهمی کم‌انحنا با لبهٔ گرد (گسترش ۰٫۴٫۰).
    /// </summary>
    /// <remarks>
    /// پروفیل گنبد: r = apex + (R − apex)·sqrt(1 − t²) — در لبهٔ پایین گنبد مماس عمودی
    /// دارد (لبهٔ گرد واقعی) و در قله به apexR می‌رسد. با محرک حفره‌ای (آلن/تورکس/پین‌دار)،
    /// قلهٔ گنبد تا شعاع بیشینهٔ حفره «پلتفرم تخت» می‌شود و حفره روی همان تراز ساخته می‌شود
    /// (مدل سر دکمه‌ای آلن‌دار DIN 913). حداکثر عمق حفره تا سطح اتکا با گارد فاصلهٔ ایمن.
    /// </remarks>
    private static void BuildButtonHead(
        MeshBuilder mesh,
        BoltSpec spec,
        AngularGrid grid,
        double topZ,
        int[] shankBottomRing)
    {
        var headR = (spec.HeadDiameterMm ?? 0) / 2.0;
        if (headR <= 0) throw new ArgumentException("قطر سر دکمه‌ای باید مثبت باشد.");

        var height = spec.HeadHeightMm;
        var domeStart = topZ + 0.45 * height; // پایین ۴۵٪ استوانه؛ بالای ۵۵٪ گنبد کم‌انحنا.
        var apexR = Math.Max(headR * 0.06, 0.08);

        // شعاع گنبد سهمی در z — در domeStart به R می‌رسد با مماس عمودی (لبهٔ گرد).
        double DomeRadius(double z)
        {
            var t = Math.Clamp((domeStart - z) / (domeStart - topZ), 0.0, 1.0);
            return apexR + (headR - apexR) * Math.Sqrt(1.0 - t * t);
        }

        double RadiusAt(double theta, double z) => z <= domeStart ? DomeRadius(z) : headR;

        var hasSocket = HasHexSocketDrive(spec);
        var hasTorx = HasTorxDrive(spec);

        // پلتفرم قله برای محرک حفره‌ای: گنبد تا شعاع پلتفرم ساخته می‌شود و بالایش تخت.
        var socketMaxR = 0.0;
        if (hasSocket)
            socketMaxR = (spec.HexSocketAcrossFlatsMm ?? 0) / Math.Sqrt(3.0); // گوشهٔ حفرهٔ شش‌گوش.
        if (hasTorx && socketMaxR == 0.0)
            socketMaxR = (spec.TorxAcrossLobesMm ?? 0) / 2.0;                 // پَرِ تورکس.

        double capZ;
        if (socketMaxR > 0.0)
        {
            // حاشیهٔ ایمن پلتفرم: شعاع پلتفرم باید از گوشهٔ حفره بزرگ‌تر باشد تا رأس‌های
            // حلقهٔ گنبد و حلقهٔ حفره در تراز capZ هم‌مکان (و بدون اشتراک اندیس) نشوند —
            // هم‌مکان‌شدن تقاطع کاذب و وجه بی‌مساحت می‌سازد.
            var platformR = Math.Min(socketMaxR * 1.2 + 0.25, headR * 0.9);
            if (platformR >= headR * 0.95)
                throw new ArgumentException(
                    "حفرهٔ محرک از گنبد سر دکمه‌ای بزرگ‌تر است؛ قطر محرک را کم یا قطر سر را زیاد کنید.");
            if (platformR <= apexR)
                throw new ArgumentException(
                    "قطر محرک از قلهٔ گنبد کوچک‌تر است؛ قطر محرک را زیاد کنید.");

            // حل معکوس پروفیل: شعاع platformR در کدام تراز؟
            var u = (platformR - apexR) / (headR - apexR);
            var tStar = Math.Sqrt(Math.Max(0.0, 1.0 - u * u));
            capZ = domeStart - tStar * (domeStart - topZ);
            if (capZ <= topZ + 1e-7) capZ = topZ + 1e-7; // گارد عددی تراز.
        }
        else
        {
            capZ = topZ;
        }

        // ترازهای گنبد از capZ (یا قله) تا domeStart و سپس استوانه تا 0.
        var levels = new List<double> { capZ };
        const int domeSamples = 12;
        for (var i = 1; i < domeSamples; i++)
        {
            var z = capZ + (domeStart - capZ) * i / domeSamples;
            levels.Add(z);
        }
        levels.Add(domeStart);
        levels.Add(0.0);

        var headRings = RadialSurface.AddOutwardSurface(
            mesh, grid, RadiusAt,
            DedupSorted(levels),
            capFirst: socketMaxR <= 0.0, // بدون محرک: قله با fan بسته می‌شود.
            capLast: false);

        if (hasSocket)
        {
            var socketAf = spec.HexSocketAcrossFlatsMm!.Value;
            var socketDepth = spec.HexSocketDepthMm!.Value;

            // گارد عمق: کف حفره باید با فاصلهٔ ایمن بالای سطح اتکا بماند.
            if (capZ + socketDepth >= -spec.Thread.NominalDiameterMm * 0.05)
                throw new ArgumentException(
                    "عمق حفرهٔ محرک روی سر دکمه‌ای بیش از حد زیاد است؛ کف حفره به سطح اتکای سر می‌رسد.");

            double SocketRadius(double theta, double z) => HexRadius(socketAf, theta);
            var socketRings = RadialSurface.AddInwardSurface(
                mesh, grid, SocketRadius,
                DedupLevels(capZ, capZ + socketDepth),
                capFirst: false,
                capLast: !HasSecurityPin(spec));

            // پلتفرم قله: حلقه بین حفره و مرز گنبد در capZ — نرمال −Z.
            RadialSurface.AddRingCap(mesh, socketRings[0], headRings[0], solidBelow: false);

            if (HasSecurityPin(spec))
                AddSecurityPin(mesh, spec, grid, floorZ: capZ + socketDepth, capZ, socketRings[^1]);
        }

        if (hasTorx)
        {
            var nominalE = spec.TorxAcrossLobesMm!.Value;
            var depth = spec.TorxDepthMm!.Value;

            if (capZ + depth >= -spec.Thread.NominalDiameterMm * 0.05)
                throw new ArgumentException(
                    "عمق حفرهٔ تورکس روی سر دکمه‌ای بیش از حد زیاد است؛ کف حفره به سطح اتکای سر می‌رسد.");

            double TorxRadius(double theta, double z) => TorxProfile.RadiusAt(nominalE, theta);
            var pocketRings = RadialSurface.AddInwardSurface(
                mesh, grid, TorxRadius,
                DedupLevels(capZ, capZ + depth),
                capFirst: false,
                capLast: !HasSecurityPin(spec));

            RadialSurface.AddRingCap(mesh, pocketRings[0], headRings[0], solidBelow: false);

            if (HasSecurityPin(spec))
                AddSecurityPin(mesh, spec, grid, floorZ: capZ + depth, capZ, pocketRings[^1]);
        }

        // سطح اتکای سر.
        RadialSurface.AddRingCap(mesh, shankBottomRing, headRings[^1], solidBelow: true);
    }

    /// <summary>
    /// سر فلنجی (Flange) — دیسک فلنج در پایه + بدنهٔ شش‌گوش با پخ فوقانی (گسترش ۰٫۴٫۰).
    /// </summary>
    /// <remarks>
    /// دیسک فلنج از z=−ضخامت تا z=۰ (سطح اتکای وسیع)؛ شش‌گوش از topZ تا بالای فلنج.
    /// شانهٔ گذار در بالای فلنج با درپوش حلقه‌ای بین مرز شش‌گوش و مرز فلنج بسته می‌شود.
    /// محرک حفره‌ای (آلن/تورکس/پین‌دار) روی رویهٔ بالای شش‌گوش — همان الگوی سر شش‌گوش.
    /// </remarks>
    private static void BuildFlangeHead(
        MeshBuilder mesh,
        BoltSpec spec,
        AngularGrid grid,
        double topZ,
        int[] shankBottomRing)
    {
        if (spec.Drive is DriveStyle.Slot or DriveStyle.Phillips)
            throw new ArgumentException("محرک شیاری روی سر فلنجی پشتیبانی نمی‌شود.");

        var af = spec.HeadAcrossFlatsMm
            ?? throw new ArgumentException("سر فلنجی بدون عرض آچارخورِ بخش شش‌گوش قابل ساخت نیست.");
        var flangeD = spec.FlangeDiameterMm
            ?? throw new ArgumentException("سر فلنجی بدون قطر دیسک فلنج قابل ساخت نیست.");
        var flangeT = spec.FlangeThicknessMm
            ?? throw new ArgumentException("سر فلنجی بدون ضخامت دیسک فلنج قابل ساخت نیست.");
        var flangeR = flangeD / 2.0;
        var flangeTop = -flangeT; // تراز بالای دیسک فلنج.
        var hexHeight = flangeTop - topZ;

        if (hexHeight <= 0)
            throw new ArgumentException(
                "ضخامت فلنج از ارتفاع کل سر بیشتر است؛ بخش شش‌گوش باقی نمی‌ماند.");

        var hasSocket = HasHexSocketDrive(spec);
        var hasTorx = HasTorxDrive(spec);
        var chamfer = Math.Min(spec.HeadTopChamferMm, hexHeight / 2.0);

        // بدنهٔ شش‌گوش با پخ فوقانی — از topZ تا flangeTop.
        double HexRadiusAt(double theta, double z) =>
            HexRadius(af, theta) - Math.Max(0.0, (topZ + chamfer) - z);

        var hexRings = RadialSurface.AddOutwardSurface(
            mesh, grid, HexRadiusAt,
            DedupLevels(topZ, topZ + chamfer, flangeTop),
            capFirst: !hasSocket && !hasTorx,
            capLast: false);

        if (hasSocket)
        {
            var socketAf = spec.HexSocketAcrossFlatsMm!.Value;
            var socketDepth = spec.HexSocketDepthMm!.Value;
            if (socketDepth >= hexHeight)
                throw new ArgumentException("عمق حفرهٔ آلن باید کمتر از ارتفاع بخش شش‌گوش سر فلنجی باشد.");

            if (socketAf / Math.Sqrt(3.0) >= af / 2.0 - chamfer)
                throw new ArgumentException(
                    "حفرهٔ آلن از مرز داخلی شش‌گوشِ سر فلنجی بزرگ‌تر است؛ قطر حفره را کم یا عرض آچارخور را زیاد کنید.");

            double SocketRadius(double theta, double z) => HexRadius(socketAf, theta);
            var socketRings = RadialSurface.AddInwardSurface(
                mesh, grid, SocketRadius,
                DedupLevels(topZ, topZ + socketDepth),
                capFirst: false,
                capLast: !HasSecurityPin(spec));

            RadialSurface.AddRingCap(mesh, socketRings[0], hexRings[0], solidBelow: false);

            if (HasSecurityPin(spec))
                AddSecurityPin(mesh, spec, grid, floorZ: topZ + socketDepth, topZ, socketRings[^1]);
        }

        if (hasTorx)
        {
            var nominalE = spec.TorxAcrossLobesMm
                ?? throw new ArgumentException("حفرهٔ تورکس بدون قطر اسمی قابل ساخت نیست.");
            if (nominalE / 2.0 >= af / 2.0 - chamfer)
                throw new ArgumentException(
                    "حفرهٔ تورکس از مرز داخلی شش‌گوشِ سر فلنجی بزرگ‌تر است؛ قطر تورکس را کم یا عرض آچارخور را زیاد کنید.");

            var depth = spec.TorxDepthMm!.Value;
            if (depth >= hexHeight)
                throw new ArgumentException("عمق حفرهٔ تورکس باید کمتر از ارتفاع بخش شش‌گوش سر فلنجی باشد.");

            double TorxRadius(double theta, double z) => TorxProfile.RadiusAt(nominalE, theta);
            var pocketRings = RadialSurface.AddInwardSurface(
                mesh, grid, TorxRadius,
                DedupLevels(topZ, topZ + depth),
                capFirst: false,
                capLast: !HasSecurityPin(spec));

            RadialSurface.AddRingCap(mesh, pocketRings[0], hexRings[0], solidBelow: false);

            if (HasSecurityPin(spec))
                AddSecurityPin(mesh, spec, grid, floorZ: topZ + depth, topZ, pocketRings[^1]);
        }

        // دیسک فلنج — استوانهٔ کامل از flangeTop تا سطح اتکا (z=0).
        double FlangeRadius(double theta, double z) => flangeR;
        var flangeRings = RadialSurface.AddOutwardSurface(
            mesh, grid, FlangeRadius,
            DedupLevels(flangeTop, 0.0),
            capFirst: false,
            capLast: false);

        // شانهٔ گذار: درپوش حلقه‌ای بین شش‌گوش (داخلی) و فلنج (خارجی) در flangeTop.
        // ماده در z بزرگ‌تر (دیسک فلنج) است — نرمال −Z؛ یال‌های بیرونی نزولی تا با یال
        // مرزی صعودیِ تراز اول سطح فلنج جهت مخالف بماند (watertight).
        RadialSurface.AddRingCap(mesh, hexRings[^1], flangeRings[0], solidBelow: false);

        // سطح اتکای سر: حلقه بین مرز ساق و مرز فلنج در z=0.
        RadialSurface.AddRingCap(mesh, shankBottomRing, flangeRings[^1], solidBelow: true);
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

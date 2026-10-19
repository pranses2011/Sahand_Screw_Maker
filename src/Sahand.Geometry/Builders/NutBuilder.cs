using Sahand.Core.Parts;
using Sahand.Core.Threads;
using Sahand.Geometry.Meshing;

namespace Sahand.Geometry.Builders;

/// <summary>
/// سازندهٔ مش مهره — منشور شش‌گوش با پخ‌های دو سر و حفرهٔ رزوهٔ داخلی واقعی.
/// </summary>
/// <remarks>
/// قرارداد محور: z=۰ روی سطح اتکای پایین؛ ارتفاع به سمت +Z. حفره با نرمال رو به محور
/// ساخته می‌شود و رویه‌های بالا/پایین به‌صورت درپوش حلقه‌ای بین مرز شش‌گوش و مرز حفره.
/// همهٔ ساختارها روی یک شبکهٔ زاویه‌ای (با گوشه‌های شش‌گوش) می‌نشند تا مش watertight بماند.
/// </remarks>
public static class NutBuilder
{
    /// <summary>
    /// مش کامل مهره را می‌سازد.
    /// </summary>
    /// <param name="spec">مشخصات معتبر مهره.</param>
    /// <param name="quality">preset کیفیت مش.</param>
    public static Mesh Build(NutSpec spec, MeshQualityPreset quality)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var thread = spec.Thread;
        var profile = ThreadProfileFactory.BuildProfile(thread).ToProfileFunction();
        var nominalR = thread.NominalDiameterMm / 2.0;

        // حالت نمایشی: حفرهٔ صاف بدون دندانه — برچسب آن در UI/خروجی الزامی است.
        if (thread.ModelingMode == ThreadModelingMode.Simplified)
        {
            profile = new RadialProfileFunction(
                [new ThreadProfilePoint(nominalR, 0), new ThreadProfilePoint(nominalR, thread.LeadMm)]);
        }

        var zStep = MeshQuality.ZStepFor(quality, thread.PitchMm);
        var angularCount = MeshQuality.AngularSegmentsFor(quality, thread.LeadMm, zStep);

        // گوشه‌های شش‌گوش: ۳۰° + k·۶۰°.
        var specialAngles = new List<double>();
        for (var k = 0; k < 6; k++)
            specialAngles.Add(Math.PI / 6.0 + k * Math.PI / 3.0);

        var grid = AngularGrid.Create(angularCount, specialAngles);
        var mesh = new MeshBuilder();
        var height = spec.HeightMm;
        var chamfer = Math.Min(spec.ChamferMm, height / 2.0);
        var af = spec.AcrossFlatsMm;

        // --- بدنهٔ شش‌گوش با پخ دو سر (۴۵ درجه) ---
        double RadiusAt(double theta, double z) =>
            HexRadius(af, theta) - Math.Max(Math.Max(0.0, chamfer - z), z - (height - chamfer));

        var levels = new List<double>();
        foreach (var z in new[] { 0.0, chamfer, height - chamfer, height }.OrderBy(x => x))
        {
            if (levels.Count == 0 || z - levels[^1] > 1e-9)
                levels.Add(z);
        }

        var outerRings = RadialSurface.AddOutwardSurface(
            mesh, grid, RadiusAt, levels,
            capFirst: false, capLast: false); // رویه‌ها حلقه‌ای‌اند (حفره در میان).

        // --- حفرهٔ رزوهٔ داخلی با پخ دهانه ---
        var cavityRings = ThreadedSurface.AddInternalCavity(
            mesh, thread, profile,
            zBottomMm: 0, zTopMm: height,
            mouthChamferMm: chamfer,
            grid, zStep);

        // --- رویه‌های پایین و بالا ---
        // پایین (z=0): ماده در z بزرگ‌تر → نرمال −Z.
        RadialSurface.AddRingCap(mesh, cavityRings[0], outerRings[0], solidBelow: false);

        // بالا (z=H): ماده در z کوچک‌تر → نرمال +Z.
        RadialSurface.AddRingCap(mesh, cavityRings[^1], outerRings[^1], solidBelow: true);

        return mesh.Build();
    }

    /// <summary>شعاع شش‌گوش در زاویهٔ θ — تخت‌ها روی k·۶۰°، گوشه‌ها روی ۳۰°+k·۶۰°.</summary>
    private static double HexRadius(double acrossFlatsMm, double theta)
    {
        var sector = theta % (Math.PI / 3.0);
        if (sector < 0) sector += Math.PI / 3.0;
        return acrossFlatsMm / 2.0 / Math.Cos(sector - Math.PI / 6.0);
    }
}

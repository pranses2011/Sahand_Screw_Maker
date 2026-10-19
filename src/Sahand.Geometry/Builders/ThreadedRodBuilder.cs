using Sahand.Core.Parts;
using Sahand.Core.Threads;
using Sahand.Geometry.Meshing;

namespace Sahand.Geometry.Builders;

/// <summary>
/// سازندهٔ میلهٔ رزوه‌دار/استادبولت — تمام‌رزوه یا با میان‌بخش صاف و رزوهٔ دو سر.
/// </summary>
/// <remarks>
/// قرارداد محور: z=۰ تا طول کل. برای میان‌بخش صاف، دو ناحیهٔ رزوهٔ هم‌اندازه در دو سر
/// با گذار runout به شعاهٔ اسمی ساخته می‌شود؛ رزوهٔ نامتقارن در نسخهٔ پایه پشتیبانی نمی‌شود.
/// </remarks>
public static class ThreadedRodBuilder
{
    /// <summary>
    /// مش کامل میله را می‌سازد.
    /// </summary>
    /// <param name="spec">مشخصات معتبر میله.</param>
    /// <param name="quality">preset کیفیت مش.</param>
    public static Mesh Build(ThreadedRodSpec spec, MeshQualityPreset quality)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var thread = spec.Thread;
        var profile = ThreadProfileFactory.BuildProfile(thread).ToProfileFunction();
        var nominalR = thread.NominalDiameterMm / 2.0;

        if (thread.ModelingMode == ThreadModelingMode.Simplified)
        {
            profile = new RadialProfileFunction(
                [new ThreadProfilePoint(nominalR, 0), new ThreadProfilePoint(nominalR, thread.LeadMm)]);
        }

        var zStep = MeshQuality.ZStepFor(quality, thread.PitchMm);
        var angularCount = MeshQuality.AngularSegmentsFor(quality, thread.LeadMm, zStep);
        var grid = AngularGrid.Create(angularCount);
        var mesh = new MeshBuilder();

        var length = spec.TotalLengthMm;
        var smooth = spec.SmoothCenterLengthMm ?? 0;

        ThreadedSurface.ThreadZone[] zones;
        if (smooth > 0)
        {
            var threadLengthEach = (length - smooth) / 2.0;
            zones =
            [
                new ThreadedSurface.ThreadZone(0, threadLengthEach),
                new ThreadedSurface.ThreadZone(length - threadLengthEach, length),
            ];
        }
        else
        {
            zones = [new ThreadedSurface.ThreadZone(0, length)];
        }

        ThreadedSurface.AddThreadedShank(
            mesh, thread, profile,
            zStartMm: 0, zEndMm: length,
            nominalRadiusMm: nominalR,
            zones,
            tipChamferMm: spec.EndChamferMm,
            endChamferMm: spec.EndChamferMm,
            grid, zStep,
            capStart: true, capEnd: true);

        return mesh.Build();
    }
}

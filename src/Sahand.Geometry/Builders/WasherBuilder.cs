using Sahand.Core.Parts;
using Sahand.Geometry.Meshing;

namespace Sahand.Geometry.Builders;

/// <summary>
/// سازندهٔ مش واشر تخت — جامد دورانی از پروفیل مستطیلی با پخ‌های اختیاری لبهٔ بیرونی.
/// </summary>
/// <remarks>
/// قرارداد محور: z=۰ روی رویهٔ پایین واشر؛ ضخامت به سمت +Z. واشر «بدون رزوه» است و
/// در UI از پیچ/مهره تفکیک می‌شود (مرز محصول سند ۱-۴).
/// </remarks>
public static class WasherBuilder
{
    /// <summary>
    /// مش کامل واشر را می‌سازد.
    /// </summary>
    /// <param name="spec">مشخصات معتبر واشر.</param>
    /// <param name="quality">preset کیفیت مش (برای تعداد بخش زاویه‌ای).</param>
    public static Mesh Build(WasherSpec spec, MeshQualityPreset quality)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var innerR = spec.InnerDiameterMm / 2.0;
        var outerR = spec.OuterDiameterMm / 2.0;
        var thickness = spec.ThicknessMm;
        var chamfer = Math.Min(spec.EdgeChamferMm, thickness / 2.0, (outerR - innerR) / 2.0);

        // پخ فقط اگر جا داشته باشد؛ در غیر این صورت پروفیل مستطیلی ساده.
        var hasChamfer = chamfer > 1e-9;

        // پروفیل بسته (پادساعتگرد در صفحهٔ r-z): پایین → بیرون → بالا → درون.
        var profile = new List<ProfilePoint>
        {
            new(innerR, 0),
        };
        if (hasChamfer)
        {
            profile.Add(new(outerR - chamfer, 0));
            profile.Add(new(outerR, chamfer));
            profile.Add(new(outerR, thickness - chamfer));
        }
        else
        {
            profile.Add(new(outerR, 0));
        }
        profile.Add(new(outerR, thickness));
        profile.Add(new(innerR, thickness));

        var angularCount = MeshQuality.AngularSegmentsFor(quality, 1.0, 0.5);
        var grid = AngularGrid.Create(angularCount);
        var mesh = new MeshBuilder();

        RevolvedSolid.AddRevolved(mesh, profile, grid);
        return mesh.Build();
    }
}

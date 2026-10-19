using Sahand.Core.Parts;
using Sahand.Geometry.Meshing;

namespace Sahand.Geometry.Builders;

/// <summary>
/// سازندهٔ مجموعهٔ جفت (پیچ + مهره + واشر) — اجزای مجزا و قابل انتخاب (سند ۳-۹).
/// </summary>
/// <remarks>
/// هیچ Boolean union انجام نمی‌شود؛ هر جزء مش مستقل خود را دارد و با جابه‌جایی محوری
/// در کنار هم قرار می‌گیرند. چیدمان نمایشی: سر پیچ پایین، نوک بالا؛ واشر بالای نوک؛
/// مهره بالاتر — با فاصلهٔ نمایشی قابل تنظیم. این چیدمان «تحلیل تماس» نیست.
/// </remarks>
public static class AssemblyBuilder
{
    /// <summary>
    /// مش مجموعهٔ جفت را می‌سازد.
    /// </summary>
    /// <param name="spec">مشخصات معتبر مجموعه.</param>
    /// <param name="quality">preset کیفیت مش.</param>
    public static Mesh Build(AssemblySpec spec, MeshQualityPreset quality)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var mesh = new MeshBuilder();
        var gap = Math.Max(0, spec.DisplayGapMm);

        var boltMesh = BoltBuilder.Build(spec.Bolt, quality);
        mesh.Append(boltMesh, 0, 0, 0);

        var boltTop = spec.Bolt.TotalLengthMm;

        if (spec.Washer is not null)
        {
            var washerMesh = WasherBuilder.Build(spec.Washer, quality);
            mesh.Append(washerMesh, 0, 0, boltTop + gap);
            boltTop += gap + spec.Washer.ThicknessMm;
        }

        if (spec.Nut is not null)
        {
            var nutMesh = NutBuilder.Build(spec.Nut, quality);
            mesh.Append(nutMesh, 0, 0, boltTop + gap);
        }

        return mesh.Build();
    }
}

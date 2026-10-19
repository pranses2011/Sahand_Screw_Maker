using Sahand.Core.Parts;
using Sahand.Geometry.Meshing;

namespace Sahand.Geometry.Builders;

/// <summary>
/// سازندهٔ مجموعهٔ جفت (پیچ + مهره + واشر) — اجزای مجزا و قابل انتخاب (سند ۳-۹).
/// </summary>
/// <remarks>
/// هیچ Boolean union انجام نمی‌شود؛ هر جزء مش مستقل خود را دارد و با جابه‌جایی محوری
/// در کنار هم قرار می‌گیرند. چیدمان نمایشی: سر پیچ پایین (z منفی)؛ نوک در z=L؛ واشر و
/// مهره بالای نوک با فاصلهٔ نمایشی. حداقل فاصلهٔ اجزا ۰٫۲mm نگه داشته می‌شود تا سطوح
/// هم‌مکان (که برای اسلایسر عیب مش است) هرگز ساخته نشود. این چیدمان «تحلیل تماس» نیست.
/// </remarks>
public static class AssemblyBuilder
{
    /// <summary>حداقل فاصلهٔ اجزا — جلوگیری از سطوح هم‌مکان.</summary>
    public const double MinPartGapMm = 0.2;

    /// <summary>
    /// مش مجموعهٔ جفت را می‌سازد.
    /// </summary>
    /// <param name="spec">مشخصات معتبر مجموعه.</param>
    /// <param name="quality">preset کیفیت مش.</param>
    public static Mesh Build(AssemblySpec spec, MeshQualityPreset quality)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var mesh = new MeshBuilder();
        // فاصلهٔ مؤثر: مقدار کاربر با کف ۰٫۲mm (سطوح هم‌مکان = عیب چاپ).
        var gap = Math.Max(Math.Max(spec.DisplayGapMm, 0), MinPartGapMm);

        var boltMesh = BoltBuilder.Build(spec.Bolt, quality);
        mesh.Append(boltMesh, 0, 0, 0);

        // نوک پیچ در z = LengthUnderHeadMm (سر در z منفی است؛ TotalLength شامل سر است و
        // برای چیدمان نباید به کار رود — خطای رایج که اینجا صریح مستند می‌شود).
        var stackTop = spec.Bolt.LengthUnderHeadMm;

        if (spec.Washer is not null)
        {
            var washerMesh = WasherBuilder.Build(spec.Washer, quality);
            mesh.Append(washerMesh, 0, 0, stackTop + gap);
            stackTop += gap + spec.Washer.ThicknessMm;
        }

        if (spec.Nut is not null)
        {
            var nutMesh = NutBuilder.Build(spec.Nut, quality);
            mesh.Append(nutMesh, 0, 0, stackTop + gap);
        }

        return mesh.Build();
    }
}

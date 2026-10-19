using Sahand.Core.Parts;
using Sahand.Core.Project;

namespace Sahand.Geometry.Builders;

/// <summary>
/// کارخانهٔ مش قطعه — نگاشت نوع قطعه به سازندهٔ متناظر با اعتبارسنجی پیش از ساخت.
/// </summary>
/// <remarks>
/// این نقطهٔ ورود یکتای UI/CLI به موتور هندسه است؛ اعتبارسنجی با PartValidator انجام
/// می‌شود و خطاها با پیام فارسی قبل از ساخت مدل صادر می‌شوند (الزام GEO-07).
/// خروجی «آزمایش همیشه» نیست: اعتبارسنجی مش جداگانه (MeshValidator) در اختیار فراخوان است.
/// </remarks>
public static class PartMeshFactory
{
    /// <summary>
    /// مش قطعه را از روی سند پروژه می‌سازد.
    /// </summary>
    /// <param name="document">سند پروژه با دست‌کم یک قطعهٔ معتبر.</param>
    /// <param name="quality">preset کیفیت.</param>
    /// <exception cref="ArgumentException">اگر سند قطعهٔ معتبر نداشته باشد.</exception>
    public static Meshing.Mesh Build(ProjectDocument document, Meshing.MeshQualityPreset quality)
    {
        ArgumentNullException.ThrowIfNull(document);

        return document.Kind switch
        {
            PartKind.Bolt when document.Bolt is not null => BuildPart(document.Bolt, quality),
            PartKind.Nut when document.Nut is not null => BuildPart(document.Nut, quality),
            PartKind.Washer when document.Washer is not null => BuildPart(document.Washer, quality),
            PartKind.ThreadedRod when document.ThreadedRod is not null => BuildPart(document.ThreadedRod, quality),
            PartKind.Assembly when document.Assembly is not null => BuildPart(document.Assembly, quality),
            _ => throw new ArgumentException(
                $"سند پروژه برای نوع «{document.Kind}» قطعهٔ معتبر ندارد؛ فایل ناقص است."),
        };
    }

    /// <summary>مش پیچ با اعتبارسنجی کامل پیش از ساخت.</summary>
    public static Meshing.Mesh BuildPart(Core.Parts.BoltSpec spec, Meshing.MeshQualityPreset quality)
    {
        EnsureValid(Core.Validation.PartValidator.Validate(spec), spec.Name);
        return BoltBuilder.Build(spec, quality);
    }

    /// <summary>مش مهره با اعتبارسنجی کامل.</summary>
    public static Meshing.Mesh BuildPart(Core.Parts.NutSpec spec, Meshing.MeshQualityPreset quality)
    {
        EnsureValid(Core.Validation.PartValidator.Validate(spec), spec.Name);
        return NutBuilder.Build(spec, quality);
    }

    /// <summary>مش واشر با اعتبارسنجی کامل.</summary>
    public static Meshing.Mesh BuildPart(Core.Parts.WasherSpec spec, Meshing.MeshQualityPreset quality)
    {
        EnsureValid(Core.Validation.PartValidator.Validate(spec), spec.Name);
        return WasherBuilder.Build(spec, quality);
    }

    /// <summary>مش میله با اعتبارسنجی کامل.</summary>
    public static Meshing.Mesh BuildPart(Core.Parts.ThreadedRodSpec spec, Meshing.MeshQualityPreset quality)
    {
        EnsureValid(Core.Validation.PartValidator.Validate(spec), spec.Name);
        return ThreadedRodBuilder.Build(spec, quality);
    }

    /// <summary>مش مجموعه با اعتبارسنجی کامل شامل سازگاری رزوهٔ جفت.</summary>
    public static Meshing.Mesh BuildPart(Core.Parts.AssemblySpec spec, Meshing.MeshQualityPreset quality)
    {
        EnsureValid(Core.Validation.PartValidator.Validate(spec), spec.Name);
        return AssemblyBuilder.Build(spec, quality);
    }

    /// <summary>رد ساخت در صورت خطای اعتبارسنجی با اولین پیام فارسی.</summary>
    private static void EnsureValid(Core.Validation.ValidationResult result, string partName)
    {
        if (result.HasErrors)
        {
            var first = result.Messages.First(m => m.Severity == Core.Validation.ValidationSeverity.Error);
            throw new ArgumentException($"ساخت «{partName}» انجام نشد: {first.Message}");
        }
    }
}

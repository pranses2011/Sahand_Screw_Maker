using Sahand.Core.Parts;
using Sahand.Core.Threads;
using Sahand.Geometry.Builders;
using Sahand.Geometry.Meshing;
using Xunit;

namespace Sahand.Geometry.Tests;

/// <summary>
/// آزمون‌های رزوهٔ داخلی مهره — رگرسیون باگ ۰٫۵٫۰:
/// پخ دهانه، تمام دندانه‌های داخلی را می‌تراشید و مهره بدون رزوه ساخته می‌شد.
/// </summary>
public class NutInternalThreadTests
{
    private static readonly ThreadSpec M6Internal = new()
    {
        Family = ThreadFamily.IsoMetric,
        Kind = ThreadKind.Internal,
        NominalDiameterMm = 6.0,
        PitchMm = 1.0,
        ThreadLengthMm = 4.8,
    };

    /// <summary>
    /// هستهٔ رگرسیون: دیوارهٔ حفره در «بدنهٔ میانی» باید نوسان شعاعی واقعی داشته باشد
    /// (تاج دندانه به سمت محور؛ دره روی دیواره). پیش از اصلاح، شعاع در تمام طول ۳٫۰ ثابت بود.
    /// </summary>
    [Fact]
    public void Nut_InternalThread_HoleWallVariesInMiddle()
    {
        var mesh = NutBuilder.Build(new NutSpec
        {
            Thread = M6Internal,
            AcrossFlatsMm = 9.3,
            HeightMm = 4.8,
            ChamferMm = 0.4,
        }, MeshQualityPreset.Fine);

        // پنجرهٔ میانی: خارج از پخ دهانه (۰٫۴ در هر سر) — z ∈ [۱٫۲، ۳٫۶].
        const double zLow = 1.2, zHigh = 3.6;
        var nominalR = M6Internal.NominalDiameterMm / 2.0; // دیوارهٔ حفره (دره) = ۳٫۰.

        var minR = double.MaxValue;
        var maxR = double.MinValue;
        foreach (var v in mesh.Vertices)
        {
            var r = Math.Sqrt(v.X * v.X + v.Y * v.Y);
            var inMiddle = v.Z > zLow && v.Z < zHigh;
            if (!inMiddle || r > nominalR + 1e-6) continue; // فقط دیوارهٔ حفره.
            minR = Math.Min(minR, r);
            maxR = Math.Max(maxR, r);
        }

        // دره روی دیواره (r=3) و تاج به سمت محور: عمق پروفیل ISO ≈ ۰٫۶۲۵×۰٫۸۶۶×P = ۰٫۵۴.
        Assert.True(maxR > nominalR - 1e-9, $"بیشینهٔ شعاع دیواره {maxR:0.###} باید روی اسمی باشد.");
        Assert.True(minR < nominalR - 0.3,
            $"کمینهٔ شعاع {minR:0.###} باید به‌اندازهٔ عمق پروفیل به محور نزدیک شود (باگ رزوه‌چینی حفره).");
        Assert.True(maxR - minR > 0.4, "نوسان شعاع دیوارهٔ حفره باید قابل‌توجه باشد (دندانهٔ واقعی).");
    }

    /// <summary>رزوهٔ داخلی باید با گام تناوب داشته باشد: در ستون زاویه‌ای ثابت، شعاع نوسان می‌کند.</summary>
    [Fact]
    public void Nut_InternalThread_HasMultipleTeethAlongHeight()
    {
        var mesh = NutBuilder.Build(new NutSpec
        {
            Thread = M6Internal,
            AcrossFlatsMm = 9.3,
            HeightMm = 4.8,
            ChamferMm = 0.4,
        }, MeshQualityPreset.Fine);

        // ستون θ≈0 (رأس‌های حلقه روی زاویهٔ صفر): r(z) = ρ(z − Δ(θ)) با Δ ثابت —
        // نوسان با دورهٔ lead=1mm. بیرون پخ دهانه z ∈ [۰٫۵، ۴٫۳].
        const double eps = 1e-9;
        var column = new List<(double Z, double R)>();
        foreach (var v in mesh.Vertices)
        {
            var r = Math.Sqrt(v.X * v.X + v.Y * v.Y);
            var isAtThetaZero = v.X > 0.5 && Math.Abs(v.Y) < 1e-6;
            if (isAtThetaZero && v.Z > 0.5 && v.Z < 4.3 && r < 3.05)
                column.Add((v.Z, r));
        }

        Assert.True(column.Count >= 8, $"ستون زاویه‌ای تنها {column.Count} رأس دارد.");

        // نیم‌عمق پروفیل: میان دره (۳٫۰) و تاج (~۲٫۴۶) — آستانهٔ عبور.
        const double midR = 2.73;
        var transitions = 0;
        bool? prev = null;
        foreach (var (_, r) in column.OrderBy(c => c.Z))
        {
            var below = r < midR;
            if (prev is { } p && p != below) transitions++;
            prev = below;
        }

        // ~۴ دورهٔ lead در ۴mm → دست‌کم ۵ گذر؛ با سخت‌گیری سقف هم می‌گذاریم (نه موج سینوسی ریز).
        Assert.True(transitions >= 5,
            $"فقط {transitions} گذر تاج/دره در ستون زاویه‌ای؛ رزوهٔ پیوسته با گام ۱mm نیست.");
        Assert.True(transitions <= 12, $"{transitions} گذر بیش از حد زیاد — نوسان کاذب/رقمی.");
    }

    /// <summary>حالت نمایشی همچنان سوراخ صاف می‌سازد (رفتار مستند و عمدی).</summary>
    [Fact]
    public void Nut_Simplified_StillBuildsSmoothHole()
    {
        var mesh = NutBuilder.Build(new NutSpec
        {
            Thread = M6Internal with { ModelingMode = ThreadModelingMode.Simplified },
            AcrossFlatsMm = 9.3,
            HeightMm = 4.8,
            ChamferMm = 0.4,
        }, MeshQualityPreset.Draft);

        Assert.True(MeshValidator.Validate(mesh).IsWatertight);
        Assert.True(mesh.SignedVolumeMm3() > 100);
    }

    /// <summary>مش رزوه‌دار داخلی همچنان سالم است: بسته، هم‌جهت، بدون خود-تقاطع.</summary>
    [Fact]
    public void Nut_InternalThread_RemainsWatertightAndOriented()
    {
        var mesh = NutBuilder.Build(new NutSpec
        {
            Thread = M6Internal,
            AcrossFlatsMm = 9.3,
            HeightMm = 4.8,
            ChamferMm = 0.4,
        }, MeshQualityPreset.Fine);

        var validation = MeshValidator.Validate(mesh, checkSelfIntersection: true);
        Assert.True(validation.IsWatertight, string.Join("; ", validation.Issues));
        Assert.True(validation.HasConsistentOrientation);
        Assert.Equal(0, validation.SelfIntersections);
        Assert.True(mesh.SignedVolumeMm3() > 100);
    }

    /// <summary>رزوهٔ چپ‌گرد مهره هم می‌سازد و سالم می‌ماند (پوشش handedness حفره).</summary>
    [Fact]
    public void Nut_LeftHandedInternalThread_BuildsWatertight()
    {
        var mesh = NutBuilder.Build(new NutSpec
        {
            Thread = M6Internal with { Handedness = ThreadHandedness.Left },
            AcrossFlatsMm = 9.3,
            HeightMm = 4.8,
            ChamferMm = 0.4,
        }, MeshQualityPreset.Balanced);

        var validation = MeshValidator.Validate(mesh, checkSelfIntersection: true);
        Assert.True(validation.IsWatertight, string.Join("; ", validation.Issues));
        Assert.True(mesh.SignedVolumeMm3() > 100);
    }
}

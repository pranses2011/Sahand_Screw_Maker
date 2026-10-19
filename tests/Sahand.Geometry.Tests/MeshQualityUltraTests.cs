using Sahand.Core.Parts;
using Sahand.Core.Threads;
using Sahand.Core.Validation;
using Sahand.Geometry.Builders;
using Sahand.Geometry.Meshing;
using Xunit;

namespace Sahand.Geometry.Tests;

/// <summary>
/// آزمون‌های کیفیت‌های فوق‌دقیق Ultra و Extreme (گسترش ۰٫۴٫۰).
/// </summary>
/// <remarks>
/// پوشش: مقادیر عددی گام z/بخش زاویه‌ای، سقف زاویه‌ای جدید ۱۴۴۰، رشد یکنواخت تعداد وجه
/// در زنجیرهٔ preset ها، و ساخت watertight پیچ با دو کیفیت سنگین جدید.
/// </remarks>
public class MeshQualityUltraTests
{
    private static void AssertValidMesh(Mesh mesh, double minExpectedVolume)
    {
        // مش‌های سنگین (> سقف بررسی تقاطع): گارد صادقانه فعال می‌شود —watertight/جهت کامل چک،
        // تقاطع خودی «انجام‌نشده» گزارش می‌شود نه عدد صفر جعلی.
        var heavy = mesh.Triangles.Length > MeshValidator.MaxSelfIntersectionFaceCount;
        var validation = MeshValidator.Validate(mesh, checkSelfIntersection: true);
        Assert.True(validation.IsWatertight, $"مش بسته نیست: {string.Join("; ", validation.Issues)}");
        Assert.True(validation.HasConsistentOrientation, "جهت‌دهی ناسازگار است.");
        if (heavy)
        {
            Assert.Equal(-1, validation.SelfIntersections);
            Assert.Contains(validation.Issues, i => i.Contains("انجام نشد"));
        }
        else
        {
            Assert.Equal(0, validation.SelfIntersections);
        }
        Assert.True(mesh.SignedVolumeMm3() > minExpectedVolume,
            $"حجم {mesh.SignedVolumeMm3():0.##} غیرمنتظره کم است.");
    }

    // ---------- ریاضی کیفیت ----------

    [Theory]
    [InlineData(MeshQualityPreset.Ultra, 5.0)]     // pitch بزرگ: نسبت خالص
    [InlineData(MeshQualityPreset.Extreme, 5.0)]
    [InlineData(MeshQualityPreset.Ultra, 0.5)]     // pitch ریز: سقف کف گام
    [InlineData(MeshQualityPreset.Extreme, 0.5)]
    public void ZStepFor_UltraExtreme_IsFinerThanVeryFine(MeshQualityPreset preset, double pitch)
    {
        var ultraStep = MeshQuality.ZStepFor(preset, pitch);
        var veryFineStep = MeshQuality.ZStepFor(MeshQualityPreset.VeryFine, pitch);
        Assert.True(ultraStep < veryFineStep,
            $"گام z {preset} ({ultraStep:0.####}) باید از VeryFine ({veryFineStep:0.####}) ریزتر باشد.");
        Assert.True(ultraStep > 0, "گام z باید مثبت باشد.");
    }

    [Fact]
    public void ZStepFor_UltraExtreme_ExactValues()
    {
        // pitch=3.6: نسبت‌های خالص بدون برخورد با کف.
        Assert.Equal(3.6 / 36.0, MeshQuality.ZStepFor(MeshQualityPreset.Ultra, 3.6), 12);
        Assert.Equal(3.6 / 48.0, MeshQuality.ZStepFor(MeshQualityPreset.Extreme, 3.6), 12);
    }

    [Fact]
    public void AngularSegmentsFor_UltraExtreme_BaseCounts()
    {
        // lead ریز → base خالص preset.
        Assert.Equal(512, MeshQuality.AngularSegmentsFor(MeshQualityPreset.Ultra, 0.1, 0.5));
        Assert.Equal(768, MeshQuality.AngularSegmentsFor(MeshQualityPreset.Extreme, 0.1, 0.5));
    }

    [Fact]
    public void AngularSegmentsFor_NewCeiling_Is1440()
    {
        // lead نسبت به گام خیلی بزرگ → سقف ۱۴۴۰ (پیش‌تر ۷۲۰ بود).
        var segments = MeshQuality.AngularSegmentsFor(MeshQualityPreset.Ultra, 1000.0, 0.5);
        Assert.Equal(1440, segments);
    }

    [Fact]
    public void AngularSegmentsFor_DraftUnchanged()
    {
        // رفتار preset های قدیمی نباید تغییر کند (base کوچک).
        Assert.Equal(48, MeshQuality.AngularSegmentsFor(MeshQualityPreset.Draft, 0.1, 0.5));
        Assert.Equal(96, MeshQuality.AngularSegmentsFor(MeshQualityPreset.Balanced, 0.1, 0.5));
        Assert.Equal(192, MeshQuality.AngularSegmentsFor(MeshQualityPreset.Fine, 0.1, 0.5));
    }

    // ---------- ساخت مش ----------

    public static IEnumerable<object[]> QualityPresets()
    {
        yield return new object[] { MeshQualityPreset.Ultra };
        yield return new object[] { MeshQualityPreset.Extreme };
    }

    [Theory]
    [MemberData(nameof(QualityPresets))]
    public void Bolt_UltraExtreme_BuildsWatertight(MeshQualityPreset preset)
    {
        // پیچ آلن M6×12 — کوچک نگه داشته شد تا آزمون سنگین طولانی نشود.
        var spec = new BoltSpec
        {
            Head = HeadStyle.SocketCap,
            Drive = DriveStyle.HexSocket,
            Thread = new ThreadSpec
            {
                Family = ThreadFamily.IsoMetric,
                Kind = ThreadKind.External,
                NominalDiameterMm = 6,
                PitchMm = 1,
                ThreadLengthMm = 12,
            },
            LengthUnderHeadMm = 12,
            HeadHeightMm = 3.9,
            HeadDiameterMm = 9.3,
            HexSocketAcrossFlatsMm = 3.3,
            HexSocketDepthMm = 2.0,
            TipChamferMm = 0.6,
        };

        var validation = PartValidator.Validate(spec);
        Assert.False(validation.HasErrors, string.Join("; ", validation.Messages.Select(m => m.Message)));

        var mesh = BoltBuilder.Build(spec, preset);
        AssertValidMesh(mesh, minExpectedVolume: 250);

        // حجم با کیفیت ریزتر به مقدار تحلیلی نزدیک‌تر/پایدار می‌ماند (اختلاف نسبی کوچک با Draft).
        var draftMesh = BoltBuilder.Build(spec, MeshQualityPreset.Draft);
        var relative = Math.Abs(mesh.SignedVolumeMm3() - draftMesh.SignedVolumeMm3()) /
                       Math.Abs(draftMesh.SignedVolumeMm3());
        Assert.True(relative < 0.05,
            $"اختلاف حجم نسبی {relative:P1} زیاد است — کیفیت ریزتر نباید شکل را عوض کند.");
    }

    [Fact]
    public void Bolt_ExtremeHasFarMoreTrianglesThanFine_SmootherThread()
    {
        // هستهٔ درخواست کارفرما: «مش های خیلی زیاد و قطعه خیلی نرم‌تر».
        var spec = new BoltSpec
        {
            Head = HeadStyle.Hex,
            Drive = DriveStyle.None,
            Thread = new ThreadSpec
            {
                Family = ThreadFamily.IsoMetric,
                Kind = ThreadKind.External,
                NominalDiameterMm = 6,
                PitchMm = 1,
                ThreadLengthMm = 10,
            },
            LengthUnderHeadMm = 10,
            HeadHeightMm = 3.9,
            HeadAcrossFlatsMm = 9.3,
            TipChamferMm = 0.6,
        };

        var fine = BoltBuilder.Build(spec, MeshQualityPreset.Fine);
        var ultra = BoltBuilder.Build(spec, MeshQualityPreset.Ultra);
        var extreme = BoltBuilder.Build(spec, MeshQualityPreset.Extreme);

        Assert.True(ultra.Triangles.Length > fine.Triangles.Length,
            $"Ultra ({ultra.Triangles.Length:N0} وجه) باید از Fine ({fine.Triangles.Length:N0} وجه) وجه بیشتری داشته باشد.");
        Assert.True(extreme.Triangles.Length > ultra.Triangles.Length,
            $"Extreme ({extreme.Triangles.Length:N0} وجه) باید از Ultra ({ultra.Triangles.Length:N0} وجه) وجه بیشتری داشته باشد.");
    }
}

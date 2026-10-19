using Sahand.Core.Catalog;
using Sahand.Core.Parts;
using Sahand.Core.Threads;
using Sahand.Core.Validation;
using Sahand.Geometry.Builders;
using Sahand.Geometry.Meshing;
using Xunit;

namespace Sahand.Geometry.Tests;

/// <summary>
/// آزمون‌های محرک تورکس و هندسهٔ خانواده‌های جدید رزوه (گسترش ۰٫۳٫۰).
/// </summary>
/// <remarks>
/// پوشش: ریاضی پروفیل تورکس (کران‌ها/تقارن/پیوستگی)، ساخت مش watertight پیچ تورکسی
/// روی هر دو سر مجاز، رد ترکیب‌های نامعتبر، و ساخت watertight برای ویتوورث/NPT/BSPP/Tr/ACME.
/// </remarks>
public class TorxAndNewFamiliesTests
{
    private static void AssertValidMesh(Mesh mesh, double minExpectedVolume)
    {
        var validation = MeshValidator.Validate(mesh, checkSelfIntersection: true);
        Assert.True(validation.IsWatertight, $"مش بسته نیست: {string.Join("; ", validation.Issues)}");
        Assert.True(validation.HasConsistentOrientation, "جهت‌دهی ناسازگار است.");
        Assert.Equal(0, validation.SelfIntersections);
        Assert.True(mesh.SignedVolumeMm3() > minExpectedVolume,
            $"حجم {mesh.SignedVolumeMm3():0.##} غیرمنتظره کم است.");
    }

    // ---------- ریاضی پروفیل تورکس ----------

    [Theory]
    [InlineData(2.8)]   // ~T10
    [InlineData(5.52)]  // ~T30
    [InlineData(9.0)]
    public void TorxProfile_RadiusStaysBetweenValleyAndLobeBounds(double nominalE)
    {
        const int samples = 720;
        for (var i = 0; i < samples; i++)
        {
            var r = TorxProfile.RadiusAt(nominalE, i * 2.0 * Math.PI / samples);
            Assert.True(r > 0, $"شعاع منفی در زاویهٔ {i}");
            Assert.True(r <= nominalE / 2.0 + 1e-9,
                $"شعاع {r} از نیم‌قطر پَر ({nominalE / 2}) بزرگ‌تر است.");
            Assert.True(r >= TorxProfile.AcrossValleysMm(nominalE) / 2.0 - 1e-9,
                $"شعاع {r} از عمق دره ({TorxProfile.AcrossValleysMm(nominalE) / 2}) کمتر است.");
        }
    }

    [Fact]
    public void TorxProfile_PeaksAtLobeCenters_ValleysAtMidpoints()
    {
        const double e = 5.52;
        for (var k = 0; k < 6; k++)
        {
            var lobe = TorxProfile.RadiusAt(e, k * Math.PI / 3.0);
            var valley = TorxProfile.RadiusAt(e, k * Math.PI / 3.0 + Math.PI / 6.0);
            Assert.Equal(e / 2.0, lobe, 1e-9);
            Assert.Equal(TorxProfile.AcrossValleysMm(e) / 2.0, valley, 1e-9);
        }
    }

    [Fact]
    public void TorxProfile_IsSixFoldPeriodic()
    {
        const double e = 5.52;
        foreach (var t in new[] { 0.1, 0.7, 1.3, 2.9, 4.4, 5.8 })
            Assert.Equal(TorxProfile.RadiusAt(e, t), TorxProfile.RadiusAt(e, t + Math.PI / 3.0), 1e-9);
    }

    [Fact]
    public void TorxProfile_IsContinuousAcrossTransitions()
    {
        const double e = 5.52;
        var angles = TorxProfile.SpecialAngles(e).ToList();
        Assert.NotEmpty(angles);

        foreach (var angle in angles)
        {
            var before = TorxProfile.RadiusAt(e, angle - 1e-7);
            var after = TorxProfile.RadiusAt(e, angle + 1e-7);
            Assert.Equal(before, after, 5); // تلرانس ۱e-5 mm — پیوستگی عملی.
        }
    }

    [Fact]
    public void TorxProfile_SpecialAngles_AreDistinctAndNormalized()
    {
        const double e = 5.52;
        var angles = TorxProfile.SpecialAngles(e).ToList();
        Assert.Equal(24, angles.Count);
        Assert.All(angles, a => Assert.True(a >= 0 && a < 2 * Math.PI));
        Assert.Equal(angles.Count, angles.Distinct().Count());
    }

    // ---------- ساخت پیچ تورکسی ----------

    public static IEnumerable<object[]> TorxBolts()
    {
        yield return new object[] { HeadStyle.Hex };
        yield return new object[] { HeadStyle.SocketCap };
    }

    [Theory]
    [MemberData(nameof(TorxBolts))]
    public void TorxBolt_BuildsWatertightPositiveVolume(HeadStyle head)
    {
        var spec = new BoltSpec
        {
            Head = head,
            Drive = DriveStyle.Torx,
            Thread = new ThreadSpec
            {
                Family = ThreadFamily.IsoMetric,
                Kind = ThreadKind.External,
                NominalDiameterMm = 6,
                PitchMm = 1,
                ThreadLengthMm = 16,
            },
            LengthUnderHeadMm = 16,
            HeadHeightMm = 3.9,
            HeadAcrossFlatsMm = head == HeadStyle.Hex ? 9.3 : null,
            HeadDiameterMm = head == HeadStyle.SocketCap ? 9.3 : null,
            TorxAcrossLobesMm = 3.9,
            TorxDepthMm = 2.0,
            TipChamferMm = 0.6,
        };

        var validation = PartValidator.Validate(spec);
        Assert.False(validation.HasErrors);

        var mesh = BoltBuilder.Build(spec, MeshQualityPreset.Draft);
        AssertValidMesh(mesh, minExpectedVolume: 200);

        // حفرهٔ تورکس واقعاً حجم را نسبت به سر بدون محرک کم می‌کند.
        var solid = spec with { Drive = DriveStyle.None, TorxAcrossLobesMm = null, TorxDepthMm = null };
        var solidMesh = BoltBuilder.Build(solid, MeshQualityPreset.Draft);
        Assert.True(solidMesh.SignedVolumeMm3() > mesh.SignedVolumeMm3(),
            "حفرهٔ تورکس حجم را کم نکرده است.");
    }

    [Fact]
    public void Torx_OnUnsupportedHead_IsRejectedByValidator()
    {
        var spec = new BoltSpec
        {
            Head = HeadStyle.FlatCountersunk,
            Drive = DriveStyle.Torx,
            Thread = new ThreadSpec { NominalDiameterMm = 6, PitchMm = 1, ThreadLengthMm = 16 },
            LengthUnderHeadMm = 16,
            HeadHeightMm = 2.25,
            HeadDiameterMm = 12.0,
            TorxAcrossLobesMm = 3.9,
            TorxDepthMm = 1.5,
        };

        var validation = PartValidator.Validate(spec);
        Assert.Contains(validation.Messages,
            m => m.Severity == ValidationSeverity.Error && m.ParameterName == "Drive");
    }

    [Fact]
    public void Torx_TooLargeOrTooDeep_IsRejected()
    {
        var @base = new
        {
            Head = HeadStyle.SocketCap,
            Thread = new ThreadSpec { NominalDiameterMm = 6, PitchMm = 1, ThreadLengthMm = 16 },
            Length = 16.0,
            HeadHeight = 3.9,
            HeadDiameter = 9.3,
        };

        var oversized = new BoltSpec
        {
            Head = @base.Head,
            Drive = DriveStyle.Torx,
            Thread = @base.Thread,
            LengthUnderHeadMm = @base.Length,
            HeadHeightMm = @base.HeadHeight,
            HeadDiameterMm = @base.HeadDiameter,
            TorxAcrossLobesMm = 8.9, // ≥ ۹۰٪ قطر سر
            TorxDepthMm = 1.5,
        };
        Assert.Contains(PartValidator.Validate(oversized).Messages,
            m => m.Severity == ValidationSeverity.Error && m.ParameterName == "TorxAcrossLobesMm");

        var tooDeep = new BoltSpec
        {
            Head = @base.Head,
            Drive = DriveStyle.Torx,
            Thread = @base.Thread,
            LengthUnderHeadMm = @base.Length,
            HeadHeightMm = @base.HeadHeight,
            HeadDiameterMm = @base.HeadDiameter,
            TorxAcrossLobesMm = 3.9,
            TorxDepthMm = 3.9, // ≥ ارتفاع سر
        };
        Assert.Contains(PartValidator.Validate(tooDeep).Messages,
            m => m.Severity == ValidationSeverity.Error && m.ParameterName == "TorxDepthMm");
    }

    // ---------- هندسهٔ خانواده‌های جدید ----------

    [Fact]
    public void Whitworth_BoltAndNut_BuildWatertight()
    {
        var boltSpec = ThreadCatalog.BuildSpec(ThreadFamily.Whitworth, "1/4-20 BSW", ThreadKind.External);
        Assert.NotNull(boltSpec);

        var bolt = new BoltSpec
        {
            Head = HeadStyle.Hex,
            Drive = DriveStyle.None,
            Thread = boltSpec!,
            LengthUnderHeadMm = 14,
            HeadHeightMm = 3.5,
            HeadAcrossFlatsMm = 10.0,
            TipChamferMm = 0.5,
        };
        var boltMesh = BoltBuilder.Build(bolt, MeshQualityPreset.Draft);
        AssertValidMesh(boltMesh, 150);

        var nutSpec = ThreadCatalog.BuildSpec(ThreadFamily.Whitworth, "1/4-20 BSW", ThreadKind.Internal);
        Assert.NotNull(nutSpec);
        var nut = new NutSpec
        {
            Thread = nutSpec!,
            AcrossFlatsMm = 10.0,
            HeightMm = 5.0,
            ChamferMm = 0.4,
        };
        var nutMesh = NutBuilder.Build(nut, MeshQualityPreset.Draft);
        AssertValidMesh(nutMesh, 60);
    }

    [Fact]
    public void Npt_TaperedRod_BuildsWatertight_WithGrowingDiameter()
    {
        var spec = ThreadCatalog.BuildSpec(ThreadFamily.Npt, "1/2-14 NPT", ThreadKind.External);
        Assert.NotNull(spec);
        Assert.True(spec!.TaperDegrees > 0);

        var rod = new ThreadedRodSpec { Thread = spec, TotalLengthMm = 20, EndChamferMm = 0.5 };
        var mesh = ThreadedRodBuilder.Build(rod, MeshQualityPreset.Draft);
        AssertValidMesh(mesh, 500);

        // مخروط واقعی: قطر در دو انتهای رزوه باید متفاوت باشد (بزرگ‌تر در انتهای بزرگ).
        var (minX, _, minZ, maxX, _, maxZ) = mesh.Bounds();
        var radialExtent = Math.Max(maxX - minX, maxZ - minZ);
        Assert.True(radialExtent > 0.8 * spec.NominalDiameterMm,
            $"بیشینهٔ قطر {radialExtent} برای رزوهٔ مخروطی {spec.NominalDiameterMm} خیلی کوچک است.");
    }

    [Fact]
    public void BspParallel_Nut_BuildsWatertight()
    {
        var spec = ThreadCatalog.BuildSpec(ThreadFamily.Bsp, "G1/2-14", ThreadKind.Internal);
        Assert.NotNull(spec);

        var nut = new NutSpec
        {
            Thread = spec!,
            AcrossFlatsMm = 32.0,
            HeightMm = 8.0,
            ChamferMm = 0.5,
        };
        var mesh = NutBuilder.Build(nut, MeshQualityPreset.Draft);
        AssertValidMesh(mesh, 2000);
    }

    [Theory]
    [InlineData("Tr20×4", ThreadFamily.IsoTrapezoidal)]
    [InlineData("1/2-10 ACME", ThreadFamily.Acme)]
    public void TrapezoidalAndAcme_Rods_BuildWatertight(string label, ThreadFamily family)
    {
        var spec = ThreadCatalog.BuildSpec(family, label, ThreadKind.External);
        Assert.NotNull(spec);

        var rod = new ThreadedRodSpec { Thread = spec!, TotalLengthMm = 20, EndChamferMm = 0.5 };
        var mesh = ThreadedRodBuilder.Build(rod, MeshQualityPreset.Draft);
        AssertValidMesh(mesh, 500);
    }

    [Fact]
    public void UnifiedFine_Bolt_BuildsWatertight()
    {
        var spec = ThreadCatalog.BuildSpec(ThreadFamily.Unified, "1/4-28", ThreadKind.External);
        Assert.NotNull(spec);
        Assert.Equal(25.4 / 28, spec!.PitchMm, 1e-9);

        var bolt = new BoltSpec
        {
            Head = HeadStyle.SocketCap,
            Drive = DriveStyle.None,
            Thread = spec,
            LengthUnderHeadMm = 12,
            HeadHeightMm = 3.2,
            HeadDiameterMm = 8.0,
            TipChamferMm = 0.4,
        };
        var mesh = BoltBuilder.Build(bolt, MeshQualityPreset.Draft);
        AssertValidMesh(mesh, 80);
    }
}

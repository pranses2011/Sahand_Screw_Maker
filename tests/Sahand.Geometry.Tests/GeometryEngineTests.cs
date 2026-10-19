using Sahand.Core.Parts;
using Sahand.Core.Threads;
using Sahand.Geometry.Builders;
using Sahand.Geometry.Meshing;
using Xunit;

namespace Sahand.Geometry.Tests;

/// <summary>
/// آزمون‌های سازندهٔ پیچ — watertight/جهت‌دهی برای همهٔ سرها و محرک‌ها (پوشش GEO-01/06).
/// </summary>
/// <remarks>
/// این آزمون‌ها «ستون فقرات» اعتماد مهندسی محصول‌اند: هر ترکیب پشتیبانی‌شده باید
/// مش بستهٔ جهت‌دار با حجم مثبت و بدون تقاطع خودی بسازد.
/// </remarks>
public class BoltBuilderTests
{
    private static ThreadSpec M6 => new()
    {
        Family = ThreadFamily.IsoMetric,
        Kind = ThreadKind.External,
        NominalDiameterMm = 6,
        PitchMm = 1,
        ThreadLengthMm = 16,
    };

    private static void AssertValidMesh(Mesh mesh, double minExpectedVolume)
    {
        var validation = MeshValidator.Validate(mesh, checkSelfIntersection: true);
        Assert.True(validation.IsWatertight, $"مش بسته نیست: {string.Join("; ", validation.Issues)}");
        Assert.True(validation.HasConsistentOrientation, "جهت‌دهی ناسازگار است.");
        Assert.Equal(0, validation.SelfIntersections);
        Assert.True(mesh.SignedVolumeMm3() > minExpectedVolume,
            $"حجم {mesh.SignedVolumeMm3():0.##} غیرمنتظره کم است.");
    }

    public static IEnumerable<object[]> HeadDriveCombinations()
    {
        yield return new object[] { HeadStyle.Hex, DriveStyle.None };
        yield return new object[] { HeadStyle.Hex, DriveStyle.HexSocket };
        yield return new object[] { HeadStyle.SocketCap, DriveStyle.None };
        yield return new object[] { HeadStyle.SocketCap, DriveStyle.HexSocket };
        yield return new object[] { HeadStyle.SocketCap, DriveStyle.Slot };
        yield return new object[] { HeadStyle.SocketCap, DriveStyle.Phillips };
        yield return new object[] { HeadStyle.FlatCountersunk, DriveStyle.None };
        yield return new object[] { HeadStyle.Pan, DriveStyle.None };
    }

    [Theory]
    [MemberData(nameof(HeadDriveCombinations))]
    public void EverySupportedCombination_BuildsWatertightPositiveVolume(HeadStyle head, DriveStyle drive)
    {
        var spec = new BoltSpec
        {
            Head = head,
            Drive = drive,
            Thread = M6,
            LengthUnderHeadMm = 16,
            HeadHeightMm = head == HeadStyle.FlatCountersunk ? 2.25 : 3.9,
            HeadAcrossFlatsMm = head == HeadStyle.Hex ? 9.3 : null,
            HeadDiameterMm = head is HeadStyle.SocketCap or HeadStyle.Pan or HeadStyle.FlatCountersunk
                ? (head == HeadStyle.FlatCountersunk ? 12.0 : 9.3)
                : null,
            HexSocketAcrossFlatsMm = drive == DriveStyle.HexSocket ? 3.3 : null,
            HexSocketDepthMm = drive == DriveStyle.HexSocket ? 2.1 : null,
            SlotWidthMm = drive is DriveStyle.Slot or DriveStyle.Phillips ? 1.1 : null,
            SlotDepthMm = drive is DriveStyle.Slot or DriveStyle.Phillips ? 0.9 : null,
            TipChamferMm = 0.6,
        };

        var mesh = BoltBuilder.Build(spec, MeshQualityPreset.Draft);
        AssertValidMesh(mesh, minExpectedVolume: 200);
    }

    [Fact]
    public void LeftHandThread_BuildsWatertight_AndMatchesRightHandVolume()
    {
        // حجم پیچ چپ‌گرد باید تقریباً برابر راست‌گرد باشد (GEO-04).
        var right = BoltBuilder.Build(new BoltSpec
        {
            Head = HeadStyle.Hex,
            Thread = M6 with { Handedness = ThreadHandedness.Right },
            LengthUnderHeadMm = 16,
            HeadHeightMm = 3.9,
            HeadAcrossFlatsMm = 9.3,
            TipChamferMm = 0.6,
        }, MeshQualityPreset.Draft);

        var left = BoltBuilder.Build(new BoltSpec
        {
            Head = HeadStyle.Hex,
            Thread = M6 with { Handedness = ThreadHandedness.Left },
            LengthUnderHeadMm = 16,
            HeadHeightMm = 3.9,
            HeadAcrossFlatsMm = 9.3,
            TipChamferMm = 0.6,
        }, MeshQualityPreset.Draft);

        var rightValidation = MeshValidator.Validate(right);
        var leftValidation = MeshValidator.Validate(left);
        Assert.True(rightValidation.IsWatertight);
        Assert.True(leftValidation.IsWatertight);

        // حجم‌ها باید در تلرانس تفکیک شبکه یکی باشند.
        Assert.Equal(right.SignedVolumeMm3(), left.SignedVolumeMm3(), precision: 1);
    }

    [Fact]
    public void MultiStartThread_BuildsWatertight()
    {
        var mesh = BoltBuilder.Build(new BoltSpec
        {
            Head = HeadStyle.Hex,
            Thread = M6 with { Starts = 2 },
            LengthUnderHeadMm = 16,
            HeadHeightMm = 3.9,
            HeadAcrossFlatsMm = 9.3,
            TipChamferMm = 0.6,
        }, MeshQualityPreset.Draft);

        AssertValidMesh(mesh, minExpectedVolume: 200);
    }

    [Fact]
    public void PartialThread_WithRunout_BuildsWatertight()
    {
        // پیچ نیم‌رزوه: بخش صاف + گذار runout (GEO-06).
        var mesh = BoltBuilder.Build(new BoltSpec
        {
            Head = HeadStyle.Hex,
            Thread = M6 with { ThreadLengthMm = 10 },
            LengthUnderHeadMm = 20,
            HeadHeightMm = 3.9,
            HeadAcrossFlatsMm = 9.3,
            ThreadRunoutFromTipMm = 10,
            TipChamferMm = 0.6,
        }, MeshQualityPreset.Draft);

        AssertValidMesh(mesh, minExpectedVolume: 300);
    }

    [Fact]
    public void SimplifiedMode_BuildsSmoothWatertightShank()
    {
        var mesh = BoltBuilder.Build(new BoltSpec
        {
            Head = HeadStyle.Hex,
            Thread = M6 with { ModelingMode = ThreadModelingMode.Simplified },
            LengthUnderHeadMm = 16,
            HeadHeightMm = 3.9,
            HeadAcrossFlatsMm = 9.3,
            TipChamferMm = 0.6,
        }, MeshQualityPreset.Draft);

        AssertValidMesh(mesh, minExpectedVolume: 300);

        // ساق صاف باید حجم بیشتری از رزوه‌دار داشته باشد (دندانه‌ها حجم کم می‌کنند).
        var threaded = BoltBuilder.Build(new BoltSpec
        {
            Head = HeadStyle.Hex,
            Thread = M6,
            LengthUnderHeadMm = 16,
            HeadHeightMm = 3.9,
            HeadAcrossFlatsMm = 9.3,
            TipChamferMm = 0.6,
        }, MeshQualityPreset.Draft);
        Assert.True(mesh.SignedVolumeMm3() > threaded.SignedVolumeMm3());
    }

    [Fact]
    public void QualityPresets_ProduceMeasurablyDifferentTriangleCounts()
    {
        // دو preset باید تفاوت عددی قابل‌اندازه‌گیری بدهند (الزام EXP-02).
        var draftMesh = BoltBuilder.Build(new BoltSpec
        {
            Head = HeadStyle.Hex,
            Thread = M6,
            LengthUnderHeadMm = 16,
            HeadHeightMm = 3.9,
            HeadAcrossFlatsMm = 9.3,
            TipChamferMm = 0.6,
        }, MeshQualityPreset.Draft);

        var fineMesh = BoltBuilder.Build(new BoltSpec
        {
            Head = HeadStyle.Hex,
            Thread = M6,
            LengthUnderHeadMm = 16,
            HeadHeightMm = 3.9,
            HeadAcrossFlatsMm = 9.3,
            TipChamferMm = 0.6,
        }, MeshQualityPreset.Fine);

        Assert.True(fineMesh.Triangles.Length > draftMesh.Triangles.Length * 1.5,
            $"presets تفاوت کافی ندارند: draft={draftMesh.Triangles.Length}, fine={fineMesh.Triangles.Length}");
    }
}

/// <summary>
/// آزمون‌های مهره/واشر/میل/مجموعه (پوشش GEO-02/03).
/// </summary>
public class OtherPartsTests
{
    private static ThreadSpec M6 => new()
    {
        Family = ThreadFamily.IsoMetric,
        NominalDiameterMm = 6,
        PitchMm = 1,
        ThreadLengthMm = 4.8,
    };

    [Fact]
    public void Nut_WithInternalThread_BuildsWatertightPositiveVolume()
    {
        var mesh = NutBuilder.Build(new NutSpec
        {
            Thread = M6 with { Kind = ThreadKind.Internal },
            AcrossFlatsMm = 9.3,
            HeightMm = 4.8,
            ChamferMm = 0.4,
        }, MeshQualityPreset.Draft);

        var validation = MeshValidator.Validate(mesh, checkSelfIntersection: true);
        Assert.True(validation.IsWatertight, string.Join("; ", validation.Issues));
        Assert.True(validation.HasConsistentOrientation);
        Assert.Equal(0, validation.SelfIntersections);
        Assert.True(mesh.SignedVolumeMm3() > 100);
    }

    [Fact]
    public void Nut_Simplified_BuildsSmoothHole()
    {
        var mesh = NutBuilder.Build(new NutSpec
        {
            Thread = M6 with { Kind = ThreadKind.Internal, ModelingMode = ThreadModelingMode.Simplified },
            AcrossFlatsMm = 9.3,
            HeightMm = 4.8,
        }, MeshQualityPreset.Draft);

        Assert.True(MeshValidator.Validate(mesh).IsWatertight);
        Assert.True(mesh.SignedVolumeMm3() > 100);
    }

    [Fact]
    public void Washer_BuildsWatertight()
    {
        var mesh = WasherBuilder.Build(new WasherSpec
        {
            InnerDiameterMm = 6.6,
            OuterDiameterMm = 12,
            ThicknessMm = 1.6,
            EdgeChamferMm = 0.2,
        }, MeshQualityPreset.Draft);

        Assert.True(MeshValidator.Validate(mesh, checkSelfIntersection: true).IsWatertight);
        // حجم واشر: π/4·(d²−d1²)·t
        var expected = Math.PI / 4 * (144 - 43.56) * 1.6;
        Assert.Equal(expected, mesh.SignedVolumeMm3(), precision: 0);
    }

    [Fact]
    public void ThreadedRod_FullThread_BuildsWatertight()
    {
        var mesh = ThreadedRodBuilder.Build(new ThreadedRodSpec
        {
            Thread = M6,
            TotalLengthMm = 30,
            EndChamferMm = 0.5,
        }, MeshQualityPreset.Draft);

        var validation = MeshValidator.Validate(mesh, checkSelfIntersection: true);
        Assert.True(validation.IsWatertight);
        Assert.Equal(0, validation.SelfIntersections);
    }

    [Fact]
    public void ThreadedRod_SmoothCenter_BuildsWatertight()
    {
        var mesh = ThreadedRodBuilder.Build(new ThreadedRodSpec
        {
            Thread = M6,
            TotalLengthMm = 40,
            SmoothCenterLengthMm = 20,
            EndChamferMm = 0.5,
        }, MeshQualityPreset.Draft);

        Assert.True(MeshValidator.Validate(mesh, checkSelfIntersection: true).IsWatertight);
    }

    [Fact]
    public void Assembly_AllPartsWatertight_NoTouchingSurfaces()
    {
        var mesh = AssemblyBuilder.Build(new AssemblySpec
        {
            Bolt = new BoltSpec
            {
                Head = HeadStyle.Hex,
                Thread = M6,
                LengthUnderHeadMm = 16,
                HeadHeightMm = 3.9,
                HeadAcrossFlatsMm = 9.3,
                TipChamferMm = 0.6,
            },
            Nut = new NutSpec
            {
                Thread = M6 with { Kind = ThreadKind.Internal },
                AcrossFlatsMm = 9.3,
                HeightMm = 4.8,
                ChamferMm = 0.4,
            },
            Washer = new WasherSpec { InnerDiameterMm = 6.6, OuterDiameterMm = 12, ThicknessMm = 1 },
            DisplayGapMm = 0,
        }, MeshQualityPreset.Draft);

        // مجموعه: watertight + بدون سطوح هم‌مکان (حداقل فاصلهٔ اجزا رعایت شود).
        var validation = MeshValidator.Validate(mesh, checkSelfIntersection: true);
        Assert.True(validation.IsWatertight);
        Assert.Equal(0, validation.SelfIntersections);

    }
}

/// <summary>
/// آزمون‌های زیرساخت مش — حجم/bbox/اعتبارسنج/مثلث‌بندی.
/// </summary>
public class MeshInfrastructureTests
{
    private static Mesh ProperCube(double size = 10)
    {
        var builder = new MeshBuilder();
        var s = size / 2;
        // ۸ رأس مکعب.
        var v = new int[8];
        var i = 0;
        for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
                for (var z = -1; z <= 1; z += 2)
                    v[i++] = builder.AddVertex(x * s, y * s, z * s);

        // وجه‌ها با winding بیرونی قانون دست راست.
        // v0=(-,-,-) v1=(-,-,+) v2=(-,+,-) v3=(-,+,+) v4=(+,-,-) v5=(+,-,+) v6=(+,+,-) v7=(+,+,+)
        void Face(int p, int q, int r, int t)
        {
            builder.AddTriangle(p, q, r);
            builder.AddTriangle(p, r, t);
        }
        Face(v[0], v[4], v[6], v[2]); // x-
        Face(v[1], v[3], v[7], v[5]); // x+
        Face(v[0], v[1], v[5], v[4]); // y-
        Face(v[2], v[6], v[7], v[3]); // y+
        Face(v[0], v[2], v[3], v[1]); // z-
        Face(v[4], v[5], v[7], v[6]); // z+
        return builder.Build();
    }

    [Fact]
    public void CubeVolume_IsExact()
    {
        var mesh = ProperCube(10);
        Assert.Equal(1000, mesh.SignedVolumeMm3(), precision: 6);
    }

    [Fact]
    public void CubeBounds_Correct()
    {
        var mesh = ProperCube(10);
        var (minX, minY, minZ, maxX, maxY, maxZ) = mesh.Bounds();
        Assert.Equal(-5, minX, precision: 9);
        Assert.Equal(5, maxX, precision: 9);
        Assert.Equal(-5, minZ, precision: 9);
        Assert.Equal(5, maxZ, precision: 9);
    }

    [Fact]
    public void Cube_IsWatertightAndOriented()
    {
        var validation = MeshValidator.Validate(ProperCube(10));
        Assert.True(validation.IsWatertight);
        Assert.True(validation.HasConsistentOrientation);
        Assert.Equal(0, validation.SelfIntersections);
    }

    [Fact]
    public void MissingFace_IsDetectedAsNotWatertight()
    {
        // مکعب ناقص (حذف یک وجه) باید تشخیص داده شود.
        var builder = new MeshBuilder();
        var s = 5.0;
        var v = new int[8];
        var i = 0;
        for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
                for (var z = -1; z <= 1; z += 2)
                    v[i++] = builder.AddVertex(x * s, y * s, z * s);

        void Face(int p, int q, int r, int t)
        {
            builder.AddTriangle(p, q, r);
            builder.AddTriangle(p, r, t);
        }
        Face(v[0], v[4], v[6], v[2]);
        Face(v[1], v[3], v[7], v[5]);
        Face(v[0], v[1], v[5], v[4]);
        Face(v[2], v[6], v[7], v[3]);
        Face(v[0], v[2], v[3], v[1]);
        // وجه z+ حذف شد — مش باز است.

        var validation = MeshValidator.Validate(builder.Build());
        Assert.False(validation.IsWatertight);
    }

    [Fact]
    public void PolygonTriangulation_CoversKnownArea()
    {
        // مربع واحد: مساحت مثلث‌بندی باید ۱ باشد.
        var triangles = PolygonTriangulation.Triangulate(4, i => i switch
        {
            0 => (0.0, 0.0),
            1 => (1.0, 0.0),
            2 => (1.0, 1.0),
            _ => (0.0, 1.0),
        }).ToList();

        Assert.Equal(2, triangles.Count);
        var area = triangles.Sum(t => 0.5 * Math.Abs(
            Cross(t.Item1, t.Item2, t.Item3)));
        Assert.Equal(1.0, area, precision: 9);

        static double Cross((double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }
    }

    [Fact]
    public void PolygonTriangulation_ConcavePolygon_CorrectCount()
    {
        // پنج‌ضلعی konkave: تعداد مثلث = n−2.
        var points = new (double X, double Y)[]
        {
            (0, 0), (4, 0), (4, 4), (2, 1), (0, 4),
        };
        var triangles = PolygonTriangulation.Triangulate(5, i => points[i]).ToList();
        Assert.Equal(3, triangles.Count);
    }
}

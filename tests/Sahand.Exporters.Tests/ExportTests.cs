using Sahand.Core.Threads;
using Sahand.Exporters;
using Sahand.Geometry.Builders;
using Sahand.Geometry.Meshing;
using Xunit;

namespace Sahand.Exporters.Tests;

/// <summary>
/// آزمون‌های round-trip خروجی‌گیرها با خوانندهٔ مستقل (پوشش EXP-01/02/03).
/// </summary>
/// <remarks>
/// قطعهٔ آزمون: پیچ M6 شش‌گوش با رزوهٔ واقعی — همان مسیر واقعی کاربر.
/// </remarks>
public class ExportRoundTripTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "ssnd-export-tests-" + Guid.NewGuid().ToString("N"));

    private Mesh SampleBoltMesh { get; } = BoltBuilder.Build(new Core.Parts.BoltSpec
    {
        Head = Core.Parts.HeadStyle.Hex,
        Thread = new ThreadSpec
        {
            Family = ThreadFamily.IsoMetric,
            NominalDiameterMm = 6,
            PitchMm = 1,
            ThreadLengthMm = 16,
        },
        LengthUnderHeadMm = 16,
        HeadHeightMm = 3.9,
        HeadAcrossFlatsMm = 9.3,
        TipChamferMm = 0.6,
    }, MeshQualityPreset.Draft);

    private string TempFile(string name) => Path.Combine(_tempDir, name);

    public ExportRoundTripTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void StlBinary_RoundTrip_CountAndBoundsPreserved()
    {
        var path = TempFile("bolt.stl");
        StlExporter.Export(SampleBoltMesh, path, new ExportOptions());

        // خواندن با خوانندهٔ مستقل (نه همان مسیر exporter — الزام سند ۶-۳).
        var read = StlReader.ReadBinary(path);
        Assert.Equal(SampleBoltMesh.Triangles.Length, read.TriangleCount);

        var expected = SampleBoltMesh.Bounds();
        Assert.Equal(expected.MaxZ - expected.MinZ, read.Size.Item3, 1); // ابعاد z با تلرانس float.
        Assert.Equal(expected.MaxX - expected.MinX, read.Size.Item1, 1);
    }

    [Fact]
    public void StlAscii_RoundTrip_ParsesBack()
    {
        var path = TempFile("bolt-ascii.stl");
        StlExporter.Export(SampleBoltMesh, path, new ExportOptions { AsciiStl = true });

        var read = StlReader.ReadAscii(path);
        Assert.Equal(SampleBoltMesh.Triangles.Length, read.TriangleCount);
    }

    [Fact]
    public void Stl_TruncatedFile_RejectedByReader()
    {
        // فایل ناقص باید رد شود (اعتبارسنجی پس از نوشتن — سند ۴-۵).
        var path = TempFile("truncated.stl");
        StlExporter.Export(SampleBoltMesh, path, new ExportOptions());
        var bytes = File.ReadAllBytes(path);
        Array.Resize(ref bytes, bytes.Length / 2);
        File.WriteAllBytes(path, bytes);

        Assert.Throws<InvalidDataException>(() => StlReader.ReadBinary(path));
    }

    [Fact]
    public void ThreeMf_RoundTrip_StructureAndUnitValidated()
    {
        var path = TempFile("bolt.3mf");
        ThreeMfExporter.Export(SampleBoltMesh, path, new ExportOptions { PartName = "پیچ آزمون" });

        var read = ThreeMfReader.Read(path);
        Assert.Equal(SampleBoltMesh.Vertices.Length, read.VertexCount);
        Assert.Equal(SampleBoltMesh.Triangles.Length, read.TriangleCount);
        Assert.Equal("millimeter", read.Unit);

        // ابعاد باید با مش مبنا بخواند (تحویل‌پذیری هندسی).
        var expected = SampleBoltMesh.Bounds();
        Assert.Equal(expected.MaxZ - expected.MinZ, read.Size.Item3, 5);
    }

    [Fact]
    public void Obj_ProducesValidVertexAndFaceLines()
    {
        var path = TempFile("bolt.obj");
        ObjExporter.Export(SampleBoltMesh, path, new ExportOptions());

        var lines = File.ReadAllLines(path);
        var vertexCount = lines.Count(l => l.StartsWith("v "));
        var faceCount = lines.Count(l => l.StartsWith("f "));

        Assert.Equal(SampleBoltMesh.Vertices.Length, vertexCount);
        Assert.Equal(SampleBoltMesh.Triangles.Length, faceCount);

        // اندیس‌های OBJ از ۱ شروع می‌شوند و در بازهٔ معتبرند.
        foreach (var line in lines.Where(l => l.StartsWith("f ")))
        {
            var indices = line[2..].Split(' ').Select(int.Parse);
            Assert.All(indices, i => Assert.InRange(i, 1, SampleBoltMesh.Vertices.Length));
        }
    }

    [Fact]
    public void PlyBinary_HeaderMatchesActualCounts()
    {
        var path = TempFile("bolt.ply");
        PlyExporter.Export(SampleBoltMesh, path, new ExportOptions());

        var text = File.ReadAllText(path);
        Assert.Contains("format binary_little_endian 1.0", text);
        Assert.Contains($"element vertex {SampleBoltMesh.Vertices.Length}", text);
        Assert.Contains($"element face {SampleBoltMesh.Triangles.Length}", text);
        Assert.Contains("end_header", text);
    }

    [Fact]
    public void Glb_WritesValidHeaderAndLength()
    {
        var path = TempFile("bolt.glb");
        GlbExporter.Export(SampleBoltMesh, path, new ExportOptions());

        var bytes = File.ReadAllBytes(path);
        using var reader = new BinaryReader(new MemoryStream(bytes));

        var magic = reader.ReadInt32();
        var version = reader.ReadInt32();
        var totalLength = reader.ReadInt32();

        Assert.Equal(0x46546C67, magic); // "glTF"
        Assert.Equal(2, version);
        Assert.Equal(bytes.Length, totalLength); // طول سربرگ با فایل واقعی می‌خواند.
    }

    [Fact]
    public void DxfThreadProfile_ContainsEntitiesAndEof()
    {
        var thread = new ThreadSpec
        {
            Family = ThreadFamily.IsoMetric,
            NominalDiameterMm = 6,
            PitchMm = 1,
            ThreadLengthMm = 10,
        };
        var path = TempFile("profile.dxf");
        DxfExporter.ExportThreadProfile(thread, path, "آزمون");

        var content = File.ReadAllText(path);
        Assert.Contains("SECTION", content);
        Assert.Contains("ENTITIES", content);
        Assert.Contains("LINE", content);
        Assert.Contains("EOF", content);
        Assert.Contains("AC1009", content); // R12.
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* بهترین‌تلاش. */ }
    }
}

/// <summary>
/// آزمون‌های مدیر خروجی — صداقت فرمت‌ها و جلوگیری از overwrite (پوشش EXP-04/05/09).
/// </summary>
public class ExportManagerTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "ssnd-manager-tests-" + Guid.NewGuid().ToString("N"));

    private Mesh TinyMesh
    {
        get
        {
            var builder = new MeshBuilder();
            var a = builder.AddVertex(0, 0, 0);
            var b = builder.AddVertex(1, 0, 0);
            var c = builder.AddVertex(0, 1, 0);
            builder.AddTriangle(a, b, c);
            return builder.Build();
        }
    }

    private string TempFile(string name) => Path.Combine(_tempDir, name);

    public ExportManagerTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void StepFormat_IsHonestlyRejected()
    {
        // STEP در برنامهٔ توسعه است و باید با دلیل رد شود — نه فایل جعلی (EXP-09).
        var manager = new ExportManager();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            manager.ExportMesh(TinyMesh, TempFile("x.step"), new ExportOptions { FormatKey = "STEP" }));

        Assert.Contains("برنامهٔ توسعه", ex.Message);
        Assert.False(File.Exists(TempFile("x.step"))); // هیچ فایلی ساخته نمی‌شود.
    }

    [Fact]
    public void UnknownFormat_IsRejected()
    {
        var manager = new ExportManager();
        Assert.Throws<InvalidOperationException>(() =>
            manager.ExportMesh(TinyMesh, TempFile("x.xyz"), new ExportOptions { FormatKey = "XYZ" }));
    }

    [Fact]
    public void ExistingFile_PreventsOverwriteWithoutConsent()
    {
        // جلوگیری از overwrite بی‌اجازه (EXP-05).
        var path = TempFile("exists.stl");
        File.WriteAllText(path, "محتوای قدیمی");

        var manager = new ExportManager();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            manager.ExportMesh(TinyMesh, path, new ExportOptions()));

        Assert.Contains("تأیید", ex.Message);
        Assert.Equal("محتوای قدیمی", File.ReadAllText(path)); // فایل قبلی دست‌نخورده.
    }

    [Fact]
    public void EmptyMesh_IsRejected()
    {
        var manager = new ExportManager();
        var empty = new Mesh(Array.Empty<MeshVertex>(), Array.Empty<MeshTriangle>());
        Assert.Throws<InvalidOperationException>(() =>
            manager.ExportMesh(empty, TempFile("empty.stl"), new ExportOptions()));
        Assert.False(File.Exists(TempFile("empty.stl")));
    }

    [Fact]
    public void SimplifiedThreadWarning_IsReported()
    {
        // خروجی بدون رزوهٔ واقعی باید هشدار روشن بدهد (سند ۳-۵).
        var manager = new ExportManager();
        var thread = new ThreadSpec
        {
            Family = ThreadFamily.IsoMetric,
            NominalDiameterMm = 6,
            PitchMm = 1,
            ModelingMode = ThreadModelingMode.Simplified,
        };
        var result = manager.ExportMesh(TinyMesh, TempFile("warn.stl"), new ExportOptions(), thread);

        Assert.Contains(result.Warnings, w => w.Contains("رزوهٔ نمایشی"));
    }

    [Fact]
    public void StlUnitWarning_IsReported()
    {
        // STL واحد ندارد — هشدار واحد الزامی است (سند ۴-۳).
        var manager = new ExportManager();
        var result = manager.ExportMesh(TinyMesh, TempFile("unit.stl"), new ExportOptions());
        Assert.Contains(result.Warnings, w => w.Contains("واحد"));
    }

    [Fact]
    public void GlbUnitConversionWarning_IsReported()
    {
        var manager = new ExportManager();
        var result = manager.ExportMesh(TinyMesh, TempFile("unit.glb"), new ExportOptions { FormatKey = "GLB" });
        Assert.Contains(result.Warnings, w => w.Contains("متر"));
    }

    [Fact]
    public void CapabilityMatrix_DoesNotClaimStepSupport()
    {
        // ماتریس قابلیت: STEP نباید «پشتیبانی‌شده» ادعا شود (صداقت سند ۴-۳).
        var step = ExportCapabilityMatrix.Find("STEP");
        Assert.NotNull(step);
        Assert.NotEqual(ExportCapabilityStatus.Supported, step!.Status);
        Assert.False(ExportCapabilityMatrix.IsExportable("STEP"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* بهترین‌تلاش. */ }
    }
}

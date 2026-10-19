using Sahand.Core.Catalog;
using Sahand.Core.Parts;
using Sahand.Core.Printing;
using Sahand.Core.Project;
using Sahand.Core.Threads;
using Sahand.Core.Validation;
using Xunit;

namespace Sahand.Core.Tests;

/// <summary>
/// آزمون سازگاری رزوهٔ جفت (پوشش GEO-03).
/// </summary>
public class ThreadCompatibilityTests
{
    private static ThreadSpec Bolt(double d = 6, double p = 1) => new()
    {
        Family = ThreadFamily.IsoMetric,
        Kind = ThreadKind.External,
        NominalDiameterMm = d,
        PitchMm = p,
    };

    private static ThreadSpec Nut(double d = 6, double p = 1) => new()
    {
        Family = ThreadFamily.IsoMetric,
        Kind = ThreadKind.Internal,
        NominalDiameterMm = d,
        PitchMm = p,
    };

    [Fact]
    public void MatchingPair_IsCompatible()
    {
        var result = ThreadCompatibility.Check(Bolt(), Nut());
        Assert.True(result.IsCompatible);
    }

    [Fact]
    public void PitchMismatch_DetectedWithPersianMessage()
    {
        var result = ThreadCompatibility.Check(Bolt(p: 1), Nut(p: 1.25));
        Assert.False(result.IsCompatible);
        Assert.Contains(result.Issues, i => i.Contains("گام"));
    }

    [Fact]
    public void HandednessMismatch_Detected()
    {
        // پیچ چپ‌گرد فقط با مهرهٔ چپ‌گرد جفت می‌شود.
        var result = ThreadCompatibility.Check(Bolt(), Nut() with { Handedness = ThreadHandedness.Left });
        Assert.False(result.IsCompatible);
        Assert.Contains(result.Issues, i => i.Contains("جهت"));
    }

    [Fact]
    public void StartsMismatch_Detected()
    {
        var result = ThreadCompatibility.Check(Bolt(), Nut() with { Starts = 2 });
        Assert.False(result.IsCompatible);
        Assert.Contains(result.Issues, i => i.Contains("شروع"));
    }

    [Fact]
    public void FamilyMismatch_Detected()
    {
        var result = ThreadCompatibility.Check(Bolt(), Nut() with { Family = ThreadFamily.Unified });
        Assert.False(result.IsCompatible);
        Assert.Contains(result.Issues, i => i.Contains("خانواده"));
    }

    [Fact]
    public void KindMismatch_Rejected()
    {
        // دو رزوهٔ خارجی با هم جفت نیستند.
        var result = ThreadCompatibility.Check(Bolt(), Bolt());
        Assert.False(result.IsCompatible);
    }
}

/// <summary>
/// آزمون کاتالوگ رزوه — داده و منبع (الزام ردیابی سند ۳-۶).
/// </summary>
public class CatalogTests
{
    [Fact]
    public void MetricCatalog_HasKnownEntriesWithSourceNote()
    {
        var entries = ThreadCatalog.GetEntries(ThreadFamily.IsoMetric);
        Assert.Contains(entries, e => e.SizeLabel == "M6");
        Assert.Contains(entries, e => e.SizeLabel == "M20");

        // هر مدخل باید یادداشت منبع داشته باشد.
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.SourceNote)));
    }

    [Fact]
    public void MetricM6_HasCoarsePitchOne()
    {
        // گام درشت M6 = 1.0mm — دادهٔ عمومی شناخته‌شده.
        var spec = ThreadCatalog.BuildSpec(ThreadFamily.IsoMetric, "M6", ThreadKind.External);
        Assert.NotNull(spec);
        Assert.Equal(6.0, spec!.NominalDiameterMm, precision: 9);
        Assert.Equal(1.0, spec.PitchMm, precision: 9);
    }

    [Fact]
    public void UnifiedCatalog_QuarterTwenty_HasCorrectTpi()
    {
        var spec = ThreadCatalog.BuildSpec(ThreadFamily.Unified, "1/4-20", ThreadKind.External);
        Assert.NotNull(spec);
        // ۲۰ رزوه در اینچ → گام = ۲۵٫۴/۲۰.
        Assert.Equal(25.4 / 20, spec!.PitchMm, precision: 9);
        Assert.Equal(0.25 * 25.4, spec.NominalDiameterMm, precision: 9);
    }

    [Fact]
    public void UnsupportedFamilyCatalog_IsEmptyByDesign()
    {
        // خانواده‌های بدون دادهٔ کاتالوگ: فهرست خالی — ادعای پوشش داده نمی‌شود.
        var entries = ThreadCatalog.GetEntries(ThreadFamily.Whitworth);
        Assert.Empty(entries);
    }

    [Fact]
    public void UnknownSize_ReturnsNull()
    {
        Assert.Null(ThreadCatalog.BuildSpec(ThreadFamily.IsoMetric, "M999", ThreadKind.External));
    }
}

/// <summary>
/// آزمون فایل پروژهٔ ssnd — ذخیره/بازکردن/دفاع امنیتی (پوشش GEO-10/EXP-08/SEC-01).
/// </summary>
public class SsndFileTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "ssnd-tests-" + Guid.NewGuid().ToString("N"));

    private string TempFile(string name) => Path.Combine(_tempDir, name);

    public SsndFileTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void SaveLoad_RoundTrip_PreservesParameters()
    {
        var document = new ProjectDocument
        {
            ProjectName = "پروژهٔ فارسی با فاصله و نویسهٔ خاص",
            Kind = PartKind.Bolt,
            Bolt = new BoltSpec
            {
                Head = HeadStyle.SocketCap,
                Drive = DriveStyle.Phillips,
                Thread = new ThreadSpec
                {
                    Family = ThreadFamily.IsoMetric,
                    NominalDiameterMm = 8,
                    PitchMm = 1.25,
                    Handedness = ThreadHandedness.Left,
                    Starts = 2,
                    StandardId = "CAT-M8",
                    DataSourceNote = "آزمون",
                },
                LengthUnderHeadMm = 16.5,
                SlotWidthMm = 1.4,
                SlotDepthMm = 1.1,
            },
        };

        var path = TempFile("test.ssnd");
        SsndFile.Save(document, path);
        var loaded = SsndFile.Load(path);

        Assert.Equal(document.ProjectName, loaded.ProjectName);
        Assert.Equal(PartKind.Bolt, loaded.Kind);
        Assert.Equal(document.Bolt!.Thread.Handedness, loaded.Bolt!.Thread.Handedness);
        Assert.Equal(document.Bolt.Thread.Starts, loaded.Bolt.Thread.Starts);
        Assert.Equal(document.Bolt.Thread.PitchMm, loaded.Bolt.Thread.PitchMm, precision: 9);
        Assert.Equal(document.Bolt.SlotWidthMm, loaded.Bolt.SlotWidthMm);
    }

    [Fact]
    public void Load_MissingFile_ThrowsPersian()
    {
        var ex = Assert.Throws<InvalidDataException>(() => SsndFile.Load(TempFile("missing.ssnd")));
        Assert.Contains("یافت نشد", ex.Message);
    }

    [Fact]
    public void Load_CorruptZip_ThrowsSafely()
    {
        // فایل ZIP نامعتبر باید پیام امن بدهد نه کرش (سند ۴-۶).
        var path = TempFile("corrupt.ssnd");
        File.WriteAllText(path, "این فایل zip نیست");
        Assert.Throws<InvalidDataException>(() => SsndFile.Load(path));
    }

    [Fact]
    public void Save_AtomicOperation_LeavesNoTempOnSuccess()
    {
        var document = new ProjectDocument { ProjectName = "اتمیک", Kind = PartKind.Washer, Washer = new WasherSpec { InnerDiameterMm = 6.6, OuterDiameterMm = 12, ThicknessMm = 1 } };
        var path = TempFile("atomic.ssnd");
        SsndFile.Save(document, path);

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp-save"));
        Assert.True(new FileInfo(path).Length > 0);
    }

    [Fact]
    public void ProjectCarries_NoSecretFields()
    {
        // فایل پروژه نباید فیلد راز/کارت داشته باشد — بررسی با بازکردن بستهٔ ZIP.
        var document = new ProjectDocument
        {
            ProjectName = "بدون راز",
            Kind = PartKind.Bolt,
            Bolt = new BoltSpec { Thread = new ThreadSpec { NominalDiameterMm = 6, PitchMm = 1 } },
        };
        var path = TempFile("nosecret.ssnd");
        SsndFile.Save(document, path);

        var zipContent = System.IO.Compression.ZipFile.OpenRead(path);
        using (zipContent)
        {
            foreach (var entry in zipContent.Entries)
            {
                using var reader = new StreamReader(entry.Open());
                var content = reader.ReadToEnd();
                Assert.DoesNotContain("62198619", content);
                Assert.DoesNotContain("password", content, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* پاک‌سازی بهترین‌تلاش. */ }
    }
}

/// <summary>
/// آزمون نسخهٔ چاپی — جدول تفاوت و دست‌نخوردگی مدل اسمی (پوشش EXP-06).
/// </summary>
public class PrintAdjusterTests
{
    private static BoltSpec SampleBolt() => new()
    {
        Head = HeadStyle.Hex,
        Thread = new ThreadSpec
        {
            Family = ThreadFamily.IsoMetric,
            NominalDiameterMm = 6,
            PitchMm = 1,
            ThreadLengthMm = 20,
        },
        LengthUnderHeadMm = 20,
        HeadHeightMm = 3.9,
        HeadAcrossFlatsMm = 9.3,
    };

    [Fact]
    public void Adjust_LeavesNominalBoltUntouched()
    {
        var bolt = SampleBolt();
        var profile = new PrintProfile { FitMode = PrintFitMode.Loose };

        PrintAdjuster.Adjust(bolt, null, profile);

        // مدل اسمی باید دست‌نخورده بماند (اصل غیرقابل‌مذاکرهٔ سند ۴-۱).
        Assert.Equal(6.0, bolt.Thread.NominalDiameterMm, precision: 9);
        Assert.Equal("پیچ", bolt.Name); // نام پیش‌فرض بدون پسوند چاپ.
    }

    [Fact]
    public void Adjust_ProducesSmallerPrintDiameter_WithLooseFit()
    {
        var profile = new PrintProfile { FitMode = PrintFitMode.Loose };
        var result = PrintAdjuster.Adjust(SampleBolt(), null, profile);

        // لقی لغزش‌دار باید قطر چاپی را کم کند.
        Assert.True(result.Bolt.Thread.NominalDiameterMm < 6.0);
        Assert.Contains(result.Diff, r => r.ParameterLabel.Contains("قطر"));
    }

    [Fact]
    public void Adjust_ProducesDiffTableWithReasons()
    {
        var profile = new PrintProfile
        {
            FitMode = PrintFitMode.Custom,
            CustomClearanceMm = 0.3,
            ExternalThreadRadialCompensationMm = 0.05,
            ApplyShrinkage = true,
            ShrinkagePercent = 0.5,
        };
        var result = PrintAdjuster.Adjust(SampleBolt(), null, profile);

        // هر ردیف تفاوت باید علت داشته باشد (جدول سند ۴-۲).
        Assert.NotEmpty(result.Diff);
        Assert.All(result.Diff, r => Assert.False(string.IsNullOrWhiteSpace(r.Reason) || string.IsNullOrWhiteSpace(r.ParameterLabel)));
    }

    [Fact]
    public void Adjust_WithNut_WarnsWhenCompensationExceedsProfileDepth()
    {
        var nut = new NutSpec
        {
            Thread = new ThreadSpec
            {
                Family = ThreadFamily.IsoMetric,
                Kind = ThreadKind.Internal,
                NominalDiameterMm = 6,
                PitchMm = 1,
            },
            AcrossFlatsMm = 9.3,
            HeightMm = 4.8,
        };
        var profile = new PrintProfile
        {
            FitMode = PrintFitMode.Custom,
            CustomClearanceMm = 1.2, // لقی بزرگ‌تر از عمق پروفیل.
        };

        var result = PrintAdjuster.Adjust(SampleBolt(), nut, profile);

        // هشدار عمق درگیری باید در جدول تفاوت ظاهر شود (EXP-07).
        Assert.Contains(result.Diff, r => r.Reason.Contains("عمق"));
    }
}

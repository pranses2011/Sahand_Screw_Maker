using Sahand.Core.Catalog;
using Sahand.Core.Threads;
using Sahand.Core.Validation;
using Xunit;

namespace Sahand.Core.Tests;

/// <summary>
/// آزمون‌های کاتالوگ هفت خانوادهٔ رزوه (گسترش ۰٫۳٫۰) — صحت داده و ساخت spec.
/// </summary>
/// <remarks>
/// پوشش: غیرخالی‌بودن فهرست‌ها، برچسب یکتا در هر خانواده، تناسب قطر/گام،
/// نیم‌زاویه و مخروط BuildSpec، و اعتبارسنجی ThreadValidator برای همهٔ مدخل‌ها.
/// </remarks>
public class CatalogFamiliesTests
{
    public static IEnumerable<object[]> AllCatalogFamilies()
    {
        yield return new object[] { ThreadFamily.IsoMetric };
        yield return new object[] { ThreadFamily.Unified };
        yield return new object[] { ThreadFamily.Whitworth };
        yield return new object[] { ThreadFamily.Npt };
        yield return new object[] { ThreadFamily.Bsp };
        yield return new object[] { ThreadFamily.IsoTrapezoidal };
        yield return new object[] { ThreadFamily.Acme };
    }

    [Theory]
    [MemberData(nameof(AllCatalogFamilies))]
    public void Family_HasEntries_WithValidGeometry(ThreadFamily family)
    {
        var entries = ThreadCatalog.GetEntries(family);
        Assert.NotEmpty(entries);

        foreach (var entry in entries)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.SizeLabel));
            Assert.True(entry.NominalDiameterMm > 0.5, $"{entry.SizeLabel}: قطر {entry.NominalDiameterMm}");
            Assert.True(entry.PitchMm > 0.05, $"{entry.SizeLabel}: گام {entry.PitchMm}");
            Assert.False(string.IsNullOrWhiteSpace(entry.Series));
            Assert.False(string.IsNullOrWhiteSpace(entry.SourceNote));

            // دندانهٔ معقول: گام نباید از قطر بزرگ‌تر شود (نسبت عمق به قطر پایدار بماند).
            Assert.True(entry.PitchMm < entry.NominalDiameterMm,
                $"{entry.SizeLabel}: گام {entry.PitchMm} ≥ قطر {entry.NominalDiameterMm}");
        }

        // برچسب‌ها در هر خانواده یکتا باشند (BuildSpec با برچسب جستجو می‌کند).
        // — متریک استثناست: برچسبِ اندازه (M6…) در سری درشت/ریز مشترک است و گام تمایز می‌دهد؛
        // invariant واقعی: یکتایی جفت (برچسب، گام).
        Assert.Equal(
            entries.Count,
            entries.Select(e => (e.SizeLabel, Math.Round(e.PitchMm, 6))).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(AllCatalogFamilies))]
    public void Family_EveryEntry_BuildsValidExternalSpec(ThreadFamily family)
    {
        foreach (var entry in ThreadCatalog.GetEntries(family))
        {
            var spec = ThreadCatalog.BuildSpec(family, entry.SizeLabel, ThreadKind.External);
            Assert.NotNull(spec);

            // نیم‌زاویهٔ اسمی خانواده.
            Assert.Equal(ThreadProfileFactory.FamilyHalfAngleDegrees(family), spec!.HalfAngleDegrees, 1e-9);

            // مخروط: فقط NPT.
            if (family == ThreadFamily.Npt)
                Assert.Equal(ThreadCatalog.NptTaperDegrees, spec.TaperDegrees, 1e-9);
            else
                Assert.Equal(0.0, spec.TaperDegrees, 1e-12);

            // شناسهٔ استاندارد و یادداشت منبع حمل می‌شوند.
            Assert.StartsWith("CAT-", spec.StandardId);
            Assert.False(string.IsNullOrWhiteSpace(spec.DataSourceNote));

            // اعتبارسنجی بدون خطا (هشدارها مجازند — مثل NPT taper که همینجا مقدار درست دارد).
            var validation = ThreadValidator.Validate(spec);
            Assert.False(validation.HasErrors,
                $"{entry.SizeLabel}: {string.Join("; ", validation.Messages.Where(m => m.Severity == ValidationSeverity.Error).Select(m => m.Message))}");
        }
    }

    [Fact]
    public void Unified_IncludesBothUncAndUnfSeries()
    {
        var entries = ThreadCatalog.GetEntries(ThreadFamily.Unified);
        Assert.Contains(entries, e => e.Series == "UNC");
        Assert.Contains(entries, e => e.Series == "UNF");

        // مدخل مشهور UNF: ۱/۴-۲۸ با گام دقیق ۲۵٫۴/۲۸.
        var quarter = ThreadCatalog.BuildSpec(ThreadFamily.Unified, "1/4-28", ThreadKind.External);
        Assert.NotNull(quarter);
        Assert.Equal(25.4 / 28, quarter!.PitchMm, 1e-9);
        Assert.Equal(0.250 * 25.4, quarter.NominalDiameterMm, 1e-9);
    }

    [Fact]
    public void Whitworth_HasBothBswAndBsf_And55DegreeProfile()
    {
        var entries = ThreadCatalog.GetEntries(ThreadFamily.Whitworth);
        Assert.Contains(entries, e => e.Series == "BSW");
        Assert.Contains(entries, e => e.Series == "BSF");

        var spec = ThreadCatalog.BuildSpec(ThreadFamily.Whitworth, "1/4-20 BSW", ThreadKind.External);
        Assert.NotNull(spec);
        Assert.Equal(27.5, spec!.HalfAngleDegrees, 1e-9);

        // پروفیل ساخته می‌شود (تناوب/صعودی‌بودن نقاط).
        var profile = ThreadProfileFactory.BuildProfile(spec);
        Assert.True(profile.Points.Count >= 5);
    }

    [Fact]
    public void Npt_Entries_CarryTaper_AndProfileBuilds()
    {
        var spec = ThreadCatalog.BuildSpec(ThreadFamily.Npt, "1/2-14 NPT", ThreadKind.External);
        Assert.NotNull(spec);
        Assert.Equal(ThreadCatalog.NptTaperDegrees, spec!.TaperDegrees, 1e-9);
        Assert.Equal(1.7899, ThreadCatalog.NptTaperDegrees, 1e-9);

        var profile = ThreadProfileFactory.BuildProfile(spec);
        Assert.True(profile.Points.Count >= 5);
    }

    [Fact]
    public void BspParallel_Entries_AreParallel_AndValidateWithoutTaperWarning()
    {
        var spec = ThreadCatalog.BuildSpec(ThreadFamily.Bsp, "G1/2-14", ThreadKind.External);
        Assert.NotNull(spec);
        Assert.Equal(0.0, spec!.TaperDegrees, 1e-12);

        // BSPP موازی نباید هشدار مخروط بگیرد (رفع ۰٫۳٫۰).
        var validation = ThreadValidator.Validate(spec);
        Assert.DoesNotContain(validation.Messages, m => m.ParameterName == "TaperDegrees");
    }

    [Fact]
    public void TrapezoidalAndAcme_Entries_BuildProfiles()
    {
        var tr = ThreadCatalog.BuildSpec(ThreadFamily.IsoTrapezoidal, "Tr20×4", ThreadKind.External);
        Assert.NotNull(tr);
        Assert.Equal(30.0, tr!.HalfAngleDegrees, 1e-9);
        Assert.NotNull(ThreadProfileFactory.BuildProfile(tr));

        var acme = ThreadCatalog.BuildSpec(ThreadFamily.Acme, "1/2-10 ACME", ThreadKind.External);
        Assert.NotNull(acme);
        Assert.Equal(14.5, acme!.HalfAngleDegrees, 1e-9);
        Assert.NotNull(ThreadProfileFactory.BuildProfile(acme));
    }

    [Fact]
    public void BuildSpec_UnknownLabel_ReturnsNull()
    {
        Assert.Null(ThreadCatalog.BuildSpec(ThreadFamily.Whitworth, "M6", ThreadKind.External));
        Assert.Null(ThreadCatalog.BuildSpec(ThreadFamily.Npt, "غایموجود", ThreadKind.External));
    }

    [Fact]
    public void InternalSpec_FromSameEntry_MatesWithExternal()
    {
        // سازگاری جفت: مدخل یکسان خانواده، داخلی/خارجی باید سازگار باشد (برای مجموعه‌ها).
        var external = ThreadCatalog.BuildSpec(ThreadFamily.Whitworth, "1/4-20 BSW", ThreadKind.External);
        var internalThread = ThreadCatalog.BuildSpec(ThreadFamily.Whitworth, "1/4-20 BSW", ThreadKind.Internal);
        Assert.NotNull(external);
        Assert.NotNull(internalThread);

        var compatibility = ThreadCompatibility.Check(external!, internalThread!);
        Assert.True(compatibility.IsCompatible,
            string.Join("; ", compatibility.Issues));
    }
}

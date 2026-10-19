using System.Diagnostics;
using Xunit;

namespace Sahand.IntegrationTests;

/// <summary>
/// آزمون‌های یکپارچگی end-to-end — اجرای واقعی ssnd-cli و راستی‌آزمایی خروجی (سند ۶-۱).
/// </summary>
/// <remarks>
/// CLI همان مسیر روان را می‌گذراند؛ بنابراین این آزمون‌ها «برش عمودی کامل» محصول‌اند:
/// ورودی → اعتبارسنجی → هندسه → فایل. یافتن باینری build شده با PackageReference
/// (output path مربوط به پروژهٔ CLI در پوشهٔ build آزمون کپی می‌شود).
/// </remarks>
public class CliEndToEndTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "ssnd-cli-tests-" + Guid.NewGuid().ToString("N"));

    public CliEndToEndTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    private string TempFile(string name) => Path.Combine(_tempDir, name);

    private static string FindCliBinary()
    {
        // باینری در پوشهٔ build آزمون کنار وابستگی‌ها کپی شده است.
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "ssnd-cli"),
            Path.Combine(AppContext.BaseDirectory, "ssnd-cli.exe"),
        };
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException("باینری ssnd-cli در پوشهٔ آزمون یافت نشد.");
    }

    private (int ExitCode, string Output, string Error) RunCli(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = FindCliBinary(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("فرایند CLI اجرا نشد.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(120_000);

        return (process.ExitCode, output, error);
    }

    [Fact]
    public void GenerateBolt_CreatesSsndAndExportsWatertightStl()
    {
        var ssndPath = TempFile("bolt.ssnd");
        var stlPath = Path.ChangeExtension(ssndPath, ".stl");

        var (code, output, error) = RunCli(
            "generate", "--part", "bolt", "--size", "M6", "--length", "16",
            "--export", "STL", "--out", ssndPath);

        Assert.Equal(0, code);
        Assert.True(File.Exists(ssndPath), "فایل پروژه ساخته نشد.");
        Assert.True(File.Exists(stlPath), "فایل STL ساخته نشد.");
        Assert.Contains("ذخیره شد", output);
        Assert.False(string.IsNullOrWhiteSpace(error));

        // خواندن مستقل STL: تعداد مثلث و ابعاد باید منطقی باشد.
        var read = Sahand.Exporters.StlReader.ReadBinary(stlPath);
        Assert.True(read.TriangleCount > 1000, $"وجه‌ها خیلی کم: {read.TriangleCount}");
        Assert.Equal(16 + 3.9, read.Size.Item3, 1); // طول کل: ساق + ارتفاع سر.
    }

    [Fact]
    public void ValidateProject_RoundTripsThroughSsndFile()
    {
        var ssndPath = TempFile("roundtrip.ssnd");
        var (generateCode, _, _) = RunCli(
            "generate", "--part", "nut", "--size", "M8", "--out", ssndPath);
        Assert.Equal(0, generateCode);

        var (validateCode, output, _) = RunCli("validate", "--in", ssndPath);
        Assert.Equal(0, validateCode);
        Assert.Contains("سالم", output);
    }

    [Fact]
    public void InvalidParameters_ExitCodeOneWithPersianError()
    {
        var (code, _, error) = RunCli(
            "generate", "--part", "bolt", "--size", "M6", "--length", "-5",
            "--out", TempFile("invalid.ssnd"));

        Assert.Equal(1, code);
        Assert.Contains("خطا", error);
        Assert.False(File.Exists(TempFile("invalid.ssnd")), "پروژهٔ نامعتبر نباید ذخیره شود.");
    }

    [Fact]
    public void UnknownPartKind_IsRejected()
    {
        var (code, _, error) = RunCli("generate", "--part", "rocket", "--out", TempFile("x.ssnd"));
        Assert.Equal(1, code);
        Assert.Contains("ناشناخته", error);
    }

    [Fact]
    public void ExportCommand_ValidatesMeshAndReportsStats()
    {
        var ssndPath = TempFile("stats.ssnd");
        RunCli("generate", "--part", "bolt", "--size", "M6", "--length", "12", "--out", ssndPath);

        var exportPath = TempFile("stats.stl");
        var (code, output, _) = RunCli(
            "export", "--in", ssndPath, "--format", "STL", "--out", exportPath);

        Assert.Equal(0, code);
        Assert.Contains("watertight=True", output);
        Assert.Contains("حجم", output);
        Assert.True(File.Exists(exportPath));
    }

    [Fact]
    public void ThreeMfExport_EndToEnd_ReadsBack()
    {
        var ssndPath = TempFile("threemf.ssnd");
        RunCli("generate", "--part", "washer", "--inner", "6.6", "--outer", "12", "--thickness", "1.6",
            "--out", ssndPath);

        var exportPath = TempFile("threemf.3mf");
        var (code, _, _) = RunCli("export", "--in", ssndPath, "--format", "3MF", "--out", exportPath);
        Assert.Equal(0, code);

        // خوانندهٔ مستقل: ساختار 3MF معتبر.
        var read = Sahand.Exporters.ThreeMfReader.Read(exportPath);
        Assert.True(read.TriangleCount > 0);
        Assert.Equal("millimeter", read.Unit);
    }

    [Fact]
    public void CouponCommand_CreatesLabeledTestPiece()
    {
        var ssndPath = TempFile("coupon.ssnd");
        var (code, output, _) = RunCli("coupon", "--size", "M6", "--out", ssndPath);

        Assert.Equal(0, code);
        Assert.Contains("نمونهٔ آزمون", output);
        Assert.Contains("استاندارد نیست", output); // برچسب صریح الزامی سند ۴-۲.
        Assert.True(File.Exists(ssndPath));
    }

    [Fact]
    public void CatalogCommand_ListsEntriesWithSource()
    {
        var (code, output, _) = RunCli("catalog", "--family", "metric");
        Assert.Equal(0, code);
        Assert.Contains("M6", output);
        Assert.Contains("ISO 261", output); // یادداشت منبع.
    }

    [Fact]
    public void HelpCommand_ShowsPersianGuide()
    {
        var (code, output, _) = RunCli("--help");
        Assert.Equal(0, code);
        Assert.Contains("طراح پیچ و مهره سهند", output);
        Assert.Contains("generate", output);
    }

    [Fact]
    public void LeftHandThread_GeneratesValidMesh()
    {
        var ssndPath = TempFile("left.ssnd");
        var (code, _, _) = RunCli(
            "generate", "--part", "bolt", "--size", "M6", "--length", "10", "--left",
            "--out", ssndPath);
        Assert.Equal(0, code);

        var exportPath = TempFile("left.stl");
        var (exportCode, output, _) = RunCli(
            "export", "--in", ssndPath, "--format", "STL", "--out", exportPath);

        Assert.Equal(0, exportCode);
        Assert.Contains("watertight=True", output);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* بهترین‌تلاش. */ }
    }
}

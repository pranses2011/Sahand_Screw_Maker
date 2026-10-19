using Sahand.Core.Catalog;
using Sahand.Core.Catalog;
using Sahand.Core.Parts;
using Sahand.Core.Printing;
using Sahand.Core.Project;
using Sahand.Core.Threads;
using Sahand.Core.Units;
using Sahand.Core.Validation;
using Sahand.Exporters;
using Sahand.Geometry.Builders;
using Sahand.Geometry.Meshing;

namespace Sahand.Cli;

/// <summary>
/// دستورهای خط فرمان — بدون وابستگی بیرونی؛ همهٔ مسیرها از همان لایه‌های محصول می‌گذرند.
/// </summary>
/// <remarks>
/// خروجی کنسول فارسی است؛ کد بازگشت: ۰ موفق، ۱ خطای کاربر، ۲ خطای غیرمنتظره.
/// این ابزار همان اعتبارسنجی/هشدارهای UI را اجرا می‌کند تا «موفق» کاذب ممکن نباشد.
/// </remarks>
public static class Commands
{
    /// <summary>دستور generate: ساخت قطعهٔ نمونه/پارامتریک و ذخیرهٔ ssnd (و خروجی اختیاری).</summary>
    public static int Generate(Dictionary<string, string> args)
    {
        var kind = args.GetValueOrDefault("--part", "bolt").ToLowerInvariant();

        var document = kind switch
        {
            "bolt" => BuildBoltDocument(args),
            "nut" => BuildNutDocument(args),
            "washer" => BuildWasherDocument(args),
            "rod" => BuildRodDocument(args),
            "assembly" => BuildAssemblyDocument(args),
            _ => Fail($"نوع قطعهٔ ناشناخته: «{kind}» (گزینه‌ها: bolt/nut/washer/rod/assembly)"),
        };

        if (document is null) return 1;

        // اعتبارسنجی پیش از ذخیره — همان مسیر UI.
        var validation = ValidateDocument(document);
        if (validation.HasErrors)
        {
            foreach (var error in validation.Messages.Where(m => m.Severity == ValidationSeverity.Error))
                Console.Error.WriteLine($"خطا [{error.ParameterName}]: {error.Message}");
            return 1;
        }

        foreach (var warning in validation.Messages.Where(m => m.Severity == ValidationSeverity.Warning))
            Console.WriteLine($"هشدار: {warning.Message}");

        var outPath = args.GetValueOrDefault("--out", "part.ssnd");
        SsndFile.Save(document, outPath);
        Console.WriteLine($"ذخیره شد: {outPath}");

        // خروجی اختیاری مش.
        if (args.TryGetValue("--export", out var format))
        {
            var mesh = PartMeshFactory.Build(document, ParseQuality(args));
            var exporter = new ExportManager();
            var meshPath = Path.ChangeExtension(outPath, format.ToLowerInvariant());
            if (File.Exists(meshPath))
            {
                Console.Error.WriteLine($"فایل خروجی وجود دارد: {meshPath}");
                return 1;
            }

            var result = exporter.ExportMesh(mesh, meshPath, new ExportOptions
            {
                FormatKey = format,
                Quality = ParseQuality(args),
                PartName = document.ProjectName,
                AsciiStl = args.ContainsKey("--ascii"),
            }, FirstThread(document));
            Console.WriteLine($"خروجی {result.Format}: {result.FileBytes} بایت → {result.FilePath}");
            foreach (var warning in result.Warnings)
                Console.WriteLine($"هشدار خروجی: {warning}");
        }

        return 0;
    }

    /// <summary>دستور export: بازکردن ssnd و خروجی مش.</summary>
    public static int Export(Dictionary<string, string> args)
    {
        if (!args.TryGetValue("--in", out var inPath))
            return FailRequired("--in");

        try
        {
            var document = SsndFile.Load(inPath);
            var mesh = PartMeshFactory.Build(document, ParseQuality(args));
            var exporter = new ExportManager();
            var format = args.GetValueOrDefault("--format", "stl");
            var outPath = args.GetValueOrDefault("--out",
                Path.ChangeExtension(inPath, format.ToLowerInvariant()));

            if (File.Exists(outPath))
                return Fail($"فایل مقصد وجود دارد: {outPath}");

            var validation = MeshValidator.Validate(mesh, checkSelfIntersection: true);
            if (!validation.IsWatertight || !validation.HasConsistentOrientation)
            {
                foreach (var issue in validation.Issues)
                    Console.Error.WriteLine($"هشدار مش: {issue}");
            }

            var result = exporter.ExportMesh(mesh, outPath, new ExportOptions
            {
                FormatKey = format,
                Quality = ParseQuality(args),
                PartName = document.ProjectName,
                AsciiStl = args.ContainsKey("--ascii"),
            }, FirstThread(document));

            Console.WriteLine($"خروجی {result.Format} ({mesh.Triangles.Length} وجه، {mesh.Vertices.Length} رأس): {result.FileBytes} بایت");
            Console.WriteLine($"اعتبارسنجی مش: watertight={validation.IsWatertight}, جهت‌دهی={validation.HasConsistentOrientation}, تقاطع‌خودی={validation.SelfIntersections}");
            Console.WriteLine($"حجم تقریبی: {mesh.SignedVolumeMm3():0.##} mm³");
            foreach (var warning in result.Warnings)
                Console.WriteLine($"هشدار خروجی: {warning}");
            return 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or IOException)
        {
            return Fail(ex.Message);
        }
    }

    /// <summary>دستور validate: اعتبارسنجی فایل پروژه.</summary>
    public static int ValidateProject(Dictionary<string, string> args)
    {
        if (!args.TryGetValue("--in", out var inPath))
            return FailRequired("--in");

        try
        {
            var document = SsndFile.Load(inPath);
            var validation = ValidateDocument(document);
            Console.WriteLine($"پروژه: {document.ProjectName} ({document.Kind}) — schema v{CurrentSchemaVersion(document)}");

            if (validation.IsValid)
            {
                Console.WriteLine("اعتبارسنجی: سالم — بدون خطا و هشدار.");
                return 0;
            }

            foreach (var message in validation.Messages)
            {
                var label = message.Severity == ValidationSeverity.Error ? "خطا" : "هشدار";
                Console.WriteLine($"{label} [{message.ParameterName}]: {message.Message}");
            }
            return validation.HasErrors ? 1 : 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            return Fail(ex.Message);
        }
    }

    /// <summary>دستور catalog: فهرست کاتالوگ رزوه.</summary>
    public static int CatalogList(Dictionary<string, string> args)
    {
        var family = args.GetValueOrDefault("--family", "metric").ToLowerInvariant() switch
        {
            "metric" or "iso" => ThreadFamily.IsoMetric,
            "unc" or "unified" => ThreadFamily.Unified,
            _ => ThreadFamily.IsoMetric,
        };

        Console.WriteLine($"کاتالوگ {family}:");
        foreach (var entry in ThreadCatalog.GetEntries(family))
        {
            var tpi = entry.Tpi is { } t ? $"{t} TPI" : $"{entry.PitchMm:0.###}mm";
            Console.WriteLine($"  {entry.SizeLabel,-8} قطر={entry.NominalDiameterMm:0.###}mm گام={tpi} سری={entry.Series}");
        }
        Console.WriteLine($"منبع: {(ThreadCatalog.GetEntries(family).FirstOrDefault()?.SourceNote ?? "—")})");
        return 0;
    }

    /// <summary>دستور coupon: نمونهٔ آزمون رزوهٔ کوتاه (سند ۴-۲).</summary>
    public static int Coupon(Dictionary<string, string> args)
    {
        var sizeLabel = args.GetValueOrDefault("--size", "M6");
        var spec = ThreadCatalog.BuildSpec(ThreadFamily.IsoMetric, sizeLabel, ThreadKind.External)
            ?? ThreadCatalog.BuildSpec(ThreadFamily.Unified, sizeLabel, ThreadKind.External);

        if (spec is null)
            return Fail($"اندازهٔ «{sizeLabel}» در کاتالوگ نیست؛ دستور catalog را ببینید.");

        // کوپن: ۴ گام رزوه با همان پروفیل — «نمونهٔ آزمون» است نه قطعهٔ استاندارد.
        var couponLength = Math.Max(4.0 * spec.PitchMm, 4.0);
        var rod = new ThreadedRodSpec
        {
            Name = $"کوپن آزمون رزوه {sizeLabel} (استاندارد تولیدی نیست)",
            Thread = spec with { ThreadLengthMm = couponLength },
            TotalLengthMm = couponLength,
            EndChamferMm = spec.PitchMm * 0.5,
        };

        var validation = PartValidator.Validate(rod);
        if (validation.HasErrors)
        {
            foreach (var error in validation.Messages.Where(m => m.Severity == ValidationSeverity.Error))
                Console.Error.WriteLine($"خطا: {error.Message}");
            return 1;
        }

        var outPath = args.GetValueOrDefault("--out", $"coupon-{sizeLabel}.ssnd");
        SsndFile.Save(new ProjectDocument
        {
            ProjectName = rod.Name,
            Kind = PartKind.ThreadedRod,
            ThreadedRod = rod,
        }, outPath);

        if (args.TryGetValue("--export", out var format))
        {
            var mesh = ThreadedRodBuilder.Build(rod, ParseQuality(args));
            var exporter = new ExportManager();
            var meshPath = Path.ChangeExtension(outPath, format.ToLowerInvariant());
            var result = exporter.ExportMesh(mesh, meshPath, new ExportOptions
            {
                FormatKey = format,
                Quality = ParseQuality(args),
                PartName = rod.Name,
            }, rod.Thread);
            Console.WriteLine($"کوپن {result.Format}: {result.FileBytes} بایت → {result.FilePath}");
        }

        Console.WriteLine($"کوپن آزمون ذخیره شد: {outPath} (برچسب: نمونهٔ آزمون — استاندارد نیست)");
        return 0;
    }

    // --- سازنده‌های سند از آرگومان‌ها ---

    private static ProjectDocument? BuildBoltDocument(Dictionary<string, string> args)
    {
        var size = args.GetValueOrDefault("--size", "M6");
        var spec = ThreadCatalog.BuildSpec(ThreadFamily.IsoMetric, size, ThreadKind.External)
            ?? ThreadCatalog.BuildSpec(ThreadFamily.Unified, size, ThreadKind.External);
        if (spec is null)
        {
            Fail($"اندازهٔ «{size}» در کاتالوگ نیست.");
            return null;
        }

        var diameter = ParseDouble(args, "--diameter", spec.NominalDiameterMm);
        var pitch = ParseDouble(args, "--pitch", spec.PitchMm);
        var length = ParseDouble(args, "--length", 20.0);

        var bolt = new BoltSpec
        {
            Name = args.GetValueOrDefault("--name", $"پیچ {size}×{length:0.#}"),
            Head = ParseEnum<HeadStyle>(args, "--head", HeadStyle.Hex),
            Drive = ParseEnum<DriveStyle>(args, "--drive", DriveStyle.None),
            Thread = spec with
            {
                NominalDiameterMm = diameter,
                PitchMm = pitch,
                ThreadLengthMm = ParseDouble(args, "--thread", length),
                Handedness = args.ContainsKey("--left") ? ThreadHandedness.Left : ThreadHandedness.Right,
                ModelingMode = args.ContainsKey("--simplified") ? ThreadModelingMode.Simplified : ThreadModelingMode.RealHelical,
            },
            LengthUnderHeadMm = length,
            HeadHeightMm = ParseDouble(args, "--head-height", diameter * 0.65),
            HeadAcrossFlatsMm = ParseDouble(args, "--across-flats", diameter * 1.55),
            TipChamferMm = ParseDouble(args, "--tip-chamfer", pitch * 0.6),
        };

        return new ProjectDocument { ProjectName = bolt.Name, Kind = PartKind.Bolt, Bolt = bolt };
    }

    private static ProjectDocument? BuildNutDocument(Dictionary<string, string> args)
    {
        var size = args.GetValueOrDefault("--size", "M6");
        var spec = ThreadCatalog.BuildSpec(ThreadFamily.IsoMetric, size, ThreadKind.Internal)
            ?? ThreadCatalog.BuildSpec(ThreadFamily.Unified, size, ThreadKind.Internal);
        if (spec is null)
        {
            Fail($"اندازهٔ «{size}» در کاتالوگ نیست.");
            return null;
        }

        var diameter = ParseDouble(args, "--diameter", spec.NominalDiameterMm);
        var pitch = ParseDouble(args, "--pitch", spec.PitchMm);
        var nut = new NutSpec
        {
            Name = args.GetValueOrDefault("--name", $"مهره {size}"),
            Thread = spec with
            {
                NominalDiameterMm = diameter,
                PitchMm = pitch,
                ThreadLengthMm = diameter * 0.8,
                ModelingMode = args.ContainsKey("--simplified") ? ThreadModelingMode.Simplified : ThreadModelingMode.RealHelical,
            },
            AcrossFlatsMm = ParseDouble(args, "--across-flats", diameter * 1.55),
            HeightMm = ParseDouble(args, "--height", diameter * 0.8),
            ChamferMm = ParseDouble(args, "--chamfer", Math.Min(0.4, diameter * 0.08)),
        };

        return new ProjectDocument { ProjectName = nut.Name, Kind = PartKind.Nut, Nut = nut };
    }

    private static ProjectDocument? BuildWasherDocument(Dictionary<string, string> args)
    {
        var inner = ParseDouble(args, "--inner", 6.6);
        var washer = new WasherSpec
        {
            Name = args.GetValueOrDefault("--name", "واشر تخت"),
            InnerDiameterMm = inner,
            OuterDiameterMm = ParseDouble(args, "--outer", inner * 2.0),
            ThicknessMm = ParseDouble(args, "--thickness", 1.6),
        };
        return new ProjectDocument { ProjectName = washer.Name, Kind = PartKind.Washer, Washer = washer };
    }

    private static ProjectDocument? BuildRodDocument(Dictionary<string, string> args)
    {
        var size = args.GetValueOrDefault("--size", "M6");
        var spec = ThreadCatalog.BuildSpec(ThreadFamily.IsoMetric, size, ThreadKind.External);
        if (spec is null)
        {
            Fail($"اندازهٔ «{size}» در کاتالوگ نیست.");
            return null;
        }

        var rod = new ThreadedRodSpec
        {
            Name = args.GetValueOrDefault("--name", $"میل {size}"),
            Thread = spec with { ThreadLengthMm = ParseDouble(args, "--length", 60.0) },
            TotalLengthMm = ParseDouble(args, "--length", 60.0),
            SmoothCenterLengthMm = args.TryGetValue("--smooth", out var smooth) ? ParseDouble(args, "--smooth", 0) : null,
            EndChamferMm = ParseDouble(args, "--chamfer", spec.PitchMm * 0.5),
        };
        return new ProjectDocument { ProjectName = rod.Name, Kind = PartKind.ThreadedRod, ThreadedRod = rod };
    }

    private static ProjectDocument? BuildAssemblyDocument(Dictionary<string, string> args)
    {
        var size = args.GetValueOrDefault("--size", "M6");
        var boltDoc = BuildBoltDocument(args);
        var nutDoc = BuildNutDocument(args);
        if (boltDoc?.Bolt is null || nutDoc?.Nut is null) return null;

        var washer = args.ContainsKey("--washer")
            ? new WasherSpec
            {
                InnerDiameterMm = boltDoc.Bolt.Thread.NominalDiameterMm * 1.1,
                OuterDiameterMm = boltDoc.Bolt.Thread.NominalDiameterMm * 2.0,
                ThicknessMm = 1.0,
            }
            : null;

        var assembly = new AssemblySpec
        {
            Name = args.GetValueOrDefault("--name", $"مجموعه {size}"),
            Bolt = boltDoc.Bolt,
            Nut = nutDoc.Nut,
            Washer = washer,
        };
        return new ProjectDocument { ProjectName = assembly.Name, Kind = PartKind.Assembly, Assembly = assembly };
    }

    // --- کمک‌تابع‌ها ---

    private static ValidationResult ValidateDocument(ProjectDocument document) => document.Kind switch
    {
        PartKind.Bolt => PartValidator.Validate(document.Bolt!),
        PartKind.Nut => PartValidator.Validate(document.Nut!),
        PartKind.Washer => PartValidator.Validate(document.Washer!),
        PartKind.ThreadedRod => PartValidator.Validate(document.ThreadedRod!),
        PartKind.Assembly => PartValidator.Validate(document.Assembly!),
        _ => new ValidationResult(),
    };

    private static ThreadSpec? FirstThread(ProjectDocument document) => document.Kind switch
    {
        PartKind.Bolt => document.Bolt?.Thread,
        PartKind.Nut => document.Nut?.Thread,
        PartKind.ThreadedRod => document.ThreadedRod?.Thread,
        PartKind.Assembly => document.Assembly?.Bolt.Thread,
        _ => null,
    };

    private static MeshQualityPreset ParseQuality(Dictionary<string, string> args) =>
        args.GetValueOrDefault("--quality", "balanced").ToLowerInvariant() switch
        {
            "draft" => MeshQualityPreset.Draft,
            "fine" => MeshQualityPreset.Fine,
            "veryfine" or "very-fine" => MeshQualityPreset.VeryFine,
            _ => MeshQualityPreset.Balanced,
        };

    private static double ParseDouble(Dictionary<string, string> args, string key, double fallback)
    {
        if (args.TryGetValue(key, out var raw) &&
            double.TryParse(PersianDigits.NormalizeDigits(raw).Replace('٫', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }
        return fallback;
    }

    private static T ParseEnum<T>(Dictionary<string, string> args, string key, T fallback)
        where T : struct, Enum
    {
        if (args.TryGetValue(key, out var raw) && Enum.TryParse<T>(raw, ignoreCase: true, out var value))
            return value;
        return fallback;
    }

    private static int CurrentSchemaVersion(ProjectDocument _) => ProjectDocument.CurrentSchemaVersion;

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static int FailRequired(string option)
    {
        Console.Error.WriteLine($"گزینهٔ الزام بدانید: {option}");
        return 1;
    }
}

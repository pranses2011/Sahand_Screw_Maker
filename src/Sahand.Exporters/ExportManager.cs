using Sahand.Core.Threads;
using Sahand.Core.Units;
using Sahand.Geometry.Meshing;

namespace Sahand.Exporters;

/// <summary>
/// خلاصهٔ نتیجهٔ خروجی — برای نمایش صادقانه در UI (سند ۴-۵: مسیر/نوع/واحد/کیفیت/هشدار).
/// </summary>
public sealed record ExportResult
{
    /// <summary>مسیر فایل نوشته‌شده.</summary>
    public required string FilePath { get; init; }

    /// <summary>فرمت.</summary>
    public required string Format { get; init; }

    /// <summary>واحد ثبت‌شده برای فایل.</summary>
    public required string UnitNote { get; init; }

    /// <summary>preset کیفیت.</summary>
    public required ExportQualityPreset Quality { get; init; }

    /// <summary>هشدارهای خروجی (مثل «رزوهٔ نمایشی» یا «واحد در STL ذخیره نمی‌شود»).</summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>اندازهٔ فایل بایت.</summary>
    public required long FileBytes { get; init; }
}

/// <summary>
/// مدیر خروجی — نقطهٔ ورود یکتای UI/CLI به خروجی‌گیرها با اعتبارسنجی پیش و پس از نوشتن.
/// </summary>
/// <remarks>
/// قواعد سند ۴-۵: برخورد نام فایل فقط با تأیید (فراخوان overwrite را کنترل می‌کند)؛
/// خروجی اتمیک؛ پس از نوشتن، وجود/اندازهٔ غیرصفر بررسی و «موفق» بدون کنترل گفته نمی‌شود.
/// قواعد سند ۴-۳: فرمت غیرفعال/برنامهٔ توسعه با دلیل رد می‌شود نه با فایل جعلی.
/// </remarks>
public sealed class ExportManager
{
    /// <summary>
    /// مش را با فرمت/کیفیت/واحد انتخابی به مسیر می‌نویسد.
    /// </summary>
    /// <param name="mesh">مش معتبر.</param>
    /// <param name="filePath">مسیر کامل مقصد.</param>
    /// <param name="options">تنظیمات.</param>
    /// <param name="threadSpecForWarnings">مشخصات رزوه برای هشدار رزوهٔ نمایشی (اختیاری).</param>
    /// <exception cref="InvalidOperationException">فرمت پشتیبانی‌نشده یا مسیر نامعتبر.</exception>
    public ExportResult ExportMesh(
        Mesh mesh,
        string filePath,
        ExportOptions options,
        ThreadSpec? threadSpecForWarnings = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var info = ExportCapabilityMatrix.Find(options.FormatKey)
            ?? throw new InvalidOperationException($"فرمت «{options.FormatKey}» شناخته نمی‌شود.");

        if (!ExportCapabilityMatrix.IsExportable(options.FormatKey))
            throw new InvalidOperationException(
                $"فرمت {info.PersianName} فعال نیست (وضعیت: {StatusLabel(info.Status)}) — {info.StatusReason}");

        if (File.Exists(filePath))
            throw new InvalidOperationException(
                "فایل مقصد وجود دارد؛ برای بازنویسی باید صریحاً تأیید شود (جلوگیری از overwrite بی‌اجازه).");

        var warnings = new List<string>();

        // هشدار واحد (سند ۴-۳: STL/OBJ/PLY واحد ندارند).
        if (options.FormatKey is "STL" or "OBJ" or "PLY")
            warnings.Add(
                $"فرمت {options.FormatKey} واحد را در فایل ذخیره نمی‌کند؛ واحد انتخابی «{UnitLabel(options.TargetUnit)}» فقط در نام/گزارش ثبت شد.");

        // هشدار رزوهٔ نمایشی — خروجی بدون هندسهٔ رزوه باید برچسب روشن داشته باشد (سند ۳-۵).
        if (threadSpecForWarnings is { ModelingMode: ThreadModelingMode.Simplified })
            warnings.Add(
                "رزوهٔ نمایشی: هندسهٔ واقعی رزوه در این خروجی نیست (مدل ساده انتخاب شده است).");

        // کنترل پایه مش پیش از خروجی.
        if (mesh.Triangles.Length == 0)
            throw new InvalidOperationException("مش بدون وجه است؛ خروجی گرفته نمی‌شود.");

        switch (options.FormatKey.ToUpperInvariant())
        {
            case "STL":
                StlExporter.Export(mesh, filePath, options);
                break;
            case "OBJ":
                ObjExporter.Export(mesh, filePath, options);
                break;
            case "PLY":
                PlyExporter.Export(mesh, filePath, options);
                break;
            case "3MF":
                ThreeMfExporter.Export(mesh, filePath, options);
                break;
            case "GLB":
                GlbExporter.Export(mesh, filePath, options);
                warnings.Add("مختصات glTF به متر تبدیل شد (واحد استاندارد glTF)؛ در نرم‌افزار مقصد متر مبنا است.");
                break;
            default:
                throw new InvalidOperationException($"فرمت «{options.FormatKey}» خروجی‌گیر ندارد.");
        }

        // کنترل پس از نوشتن: فایل باید وجود داشته و غیرصفر باشد (سند ۴-۵).
        var written = new FileInfo(filePath);
        if (!written.Exists || written.Length == 0)
            throw new InvalidOperationException(
                "خروجی نوشته شد اما فایل وجود ندارد یا صفر بایت است؛ عملیات ناموفق گزارش می‌شود.");

        return new ExportResult
        {
            FilePath = filePath,
            Format = info.PersianName,
            UnitNote = UnitLabel(options.TargetUnit),
            Quality = options.Quality,
            Warnings = warnings,
            FileBytes = written.Length,
        };
    }

    /// <summary>
    /// نقشهٔ دوبعدی پروفیل رزوه را به DXF می‌نویسد.
    /// </summary>
    public ExportResult ExportThreadProfileDxf(
        ThreadSpec spec,
        string filePath,
        string title)
    {
        if (File.Exists(filePath))
            throw new InvalidOperationException(
                "فایل مقصد وجود دارد؛ برای بازنویسی باید صریحاً تأیید شود.");

        DxfExporter.ExportThreadProfile(spec, filePath, title);
        var written = new FileInfo(filePath);
        if (!written.Exists || written.Length == 0)
            throw new InvalidOperationException("خروجی DXF ناموفق بود.");

        return new ExportResult
        {
            FilePath = filePath,
            Format = "DXF دوبعدی (R12)",
            UnitNote = "میلی‌متر",
            Quality = ExportQualityPreset.Balanced,
            Warnings = ["خروجی دوبعدی است — جانشین مدل سه‌بعدی نیست."],
            FileBytes = written.Length,
        };
    }

    private static string UnitLabel(LengthUnit unit) => unit switch
    {
        LengthUnit.Millimeter => "میلی‌متر",
        LengthUnit.Inch => "اینچ",
        _ => "?",
    };

    /// <summary>برچسب فارسی وضعیت — در پیام‌های رد فرمت استفاده می‌شود.</summary>
    private static string StatusLabel(ExportCapabilityStatus status) => status switch
    {
        ExportCapabilityStatus.Supported => "پشتیبانی‌شده",
        ExportCapabilityStatus.Limited => "محدود",
        ExportCapabilityStatus.Planned => "در برنامهٔ توسعه",
        ExportCapabilityStatus.RequiresLicense => "وابسته به مجوز",
        _ => "—",
    };
}

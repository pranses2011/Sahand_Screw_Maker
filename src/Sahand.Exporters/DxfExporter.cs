using Sahand.Core.Threads;

namespace Sahand.Exporters;

/// <summary>
/// خروجی‌گیر DXF دوبعدی (R12 ASCII) — نقشهٔ پروفیل رزوه و نمای جانبی قطعه.
/// </summary>
/// <remarks>
/// این خروجی «مکمل دوبعدی» است و جدا از فهرست سه‌بعدی ارائه می‌شود (سند ۴-۳).
/// مقیاس: میلی‌متر (واحد معمول DXF). موجودیت‌ها فقط LINE/CIRCLE/ARC هستند که بیشترین
/// سازگاری را با نرم‌افزارهای CAD دارند. نقشه شامل: پروفیل یک گام رزوه در صفحهٔ (r,z) و
/// خطوط راهنمای قطرها؛ نه نقشهٔ تلرانس‌گذاری رسمی (خارج از تعهد سند ۱-۶).
/// </remarks>
public static class DxfExporter
{
    /// <summary>
    /// نقشهٔ پروفیل رزوه را به فایل DXF می‌نویسد.
    /// </summary>
    /// <param name="spec">مشخصات رزوه.</param>
    /// <param name="filePath">مسیر مقصد.</param>
    /// <param name="title">عنوان نقشه برای متن.</param>
    public static void ExportThreadProfile(ThreadSpec spec, string filePath, string title)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var geometry = ThreadProfileFactory.BuildProfile(spec);
        var sb = new StringBuilder();
        AppendHeader(sb, title);

        // محور نمونه: r به‌عنوان X و z به‌عنوان Y؛ یک گام کامل از پروفیل ترسیم می‌شود.
        var points = geometry.Points;
        for (var i = 0; i + 1 < points.Count; i++)
        {
            AppendLine(sb,
                points[i].RadiusMm, points[i].AxialMm,
                points[i + 1].RadiusMm, points[i + 1].AxialMm);
        }

        // خط راهنمای قطر اسمی (نمایش مرجع؛ نه تلرانس رسمی).
        AppendLine(sb, 0, -0.5, spec.NominalDiameterMm / 2.0, -0.5);

        AppendFooter(sb);

        var tempPath = filePath + ".tmp-export";
        try
        {
            File.WriteAllText(tempPath, sb.ToString(), new UTF8Encoding(false));
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            throw new IOException($"نوشتن فایل DXF ممکن نشد ({ex.Message}).", ex);
        }
    }

    private static void AppendHeader(StringBuilder sb, string title)
    {
        sb.AppendLine("0");
        sb.AppendLine("SECTION");
        sb.AppendLine("2");
        sb.AppendLine("HEADER");
        sb.AppendLine("9");
        sb.AppendLine("$ACADVER");
        sb.AppendLine("1");
        sb.AppendLine("AC1009"); // R12 — بیشترین سازگاری.
        sb.AppendLine("9");
        sb.AppendLine("$INSUNITS");
        sb.AppendLine("70");
        sb.AppendLine("4"); // میلی‌متر.
        sb.AppendLine("0");
        sb.AppendLine("ENDSEC");
        sb.AppendLine("0");
        sb.AppendLine("SECTION");
        sb.AppendLine("2");
        sb.AppendLine("ENTITIES");
        sb.AppendLine($"999");
        sb.AppendLine(Escape($"{title} — Sahand Screw & Nut Designer — واحد: میلی‌متر"));
    }

    private static void AppendFooter(StringBuilder sb)
    {
        sb.AppendLine("0");
        sb.AppendLine("ENDSEC");
        sb.AppendLine("0");
        sb.AppendLine("EOF");
    }

    private static void AppendLine(StringBuilder sb, double x1, double y1, double x2, double y2)
    {
        sb.AppendLine("0");
        sb.AppendLine("LINE");
        sb.AppendLine("8");
        sb.AppendLine("0");
        sb.AppendLine("10");
        sb.AppendLine(Fmt(x1));
        sb.AppendLine("20");
        sb.AppendLine(Fmt(y1));
        sb.AppendLine("30");
        sb.AppendLine("0.0");
        sb.AppendLine("11");
        sb.AppendLine(Fmt(x2));
        sb.AppendLine("21");
        sb.AppendLine(Fmt(y2));
        sb.AppendLine("31");
        sb.AppendLine("0.0");
    }

    private static string Fmt(double v) => v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

    private static string Escape(string t) => t.Replace('\n', ' ');
}

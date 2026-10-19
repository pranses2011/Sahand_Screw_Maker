using System.Text;
using Sahand.Geometry.Meshing;

namespace Sahand.Exporters;

/// <summary>
/// خروجی‌گیر OBJ (+MTL نمایشی) — مش با گروه/متریال پایه.
/// </summary>
/// <remarks>
/// ایندکس‌گذاری OBJ از ۱ شروع می‌شود. متریال MTL فقط «نمایشی» است (سند ۴-۳: اگر متریال
/// واقعی صادر نمی‌شود وعده داده نمی‌شود — برچسب «نمایشی» در نام متریال هست). نوشتن اتمیک.
/// </remarks>
public static class ObjExporter
{
    /// <summary>
    /// مش را به فایل OBJ و در صورت رنگ، MTL کنارش می‌نویسد.
    /// </summary>
    public static void Export(Mesh mesh, string filePath, ExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var hasMaterial = !string.IsNullOrWhiteSpace(options.MaterialHexColor);
        var mtlFileName = hasMaterial
            ? Path.GetFileNameWithoutExtension(filePath) + ".mtl"
            : null;

        var sb = new StringBuilder(mesh.Vertices.Length * 24 + mesh.Triangles.Length * 16);
        sb.AppendLine("# Sahand Screw & Nut Designer — خروجی OBJ");
        sb.AppendLine($"# قطعه: {options.PartName} — واحد مبنا: میلی‌متر (OBJ واحد استاندارد ندارد)");
        if (hasMaterial)
        {
            sb.AppendLine($"mtllib {mtlFileName}");
            sb.AppendLine($"usemtl {SanitizeName(options.PartName)}");
        }

        sb.AppendLine($"o {SanitizeName(options.PartName)}");
        foreach (var v in mesh.Vertices)
        {
            sb.AppendLine($"v {Fmt(v.X * options.ScaleFactor)} {Fmt(v.Y * options.ScaleFactor)} {Fmt(v.Z * options.ScaleFactor)}");
        }

        foreach (var t in mesh.Triangles)
        {
            sb.AppendLine($"f {t.A + 1} {t.B + 1} {t.C + 1}");
        }

        try
        {
            var tempPath = filePath + ".tmp-export";
            File.WriteAllText(tempPath, sb.ToString(), new UTF8Encoding(false));
            File.Move(tempPath, filePath, overwrite: true);

            if (hasMaterial && mtlFileName is not null && options.MaterialHexColor is { } hex)
            {
                var (r, g, b) = ParseHexColor(hex);
                var mtlPath = Path.Combine(Path.GetDirectoryName(filePath) ?? ".", mtlFileName);
                var mtlTemp = mtlPath + ".tmp-export";
                var mtl = new StringBuilder();
                mtl.AppendLine("# Sahand Screw & Nut Designer — متریال نمایشی (خواص مکانیکی نیست)");
                mtl.AppendLine($"newmtl {SanitizeName(options.PartName)}");
                mtl.AppendLine($"Kd {Fmt(r)} {Fmt(g)} {Fmt(b)}");
                mtl.AppendLine("Ka 0.1 0.1 0.1");
                mtl.AppendLine("Ks 0.25 0.25 0.25");
                mtl.AppendLine("Ns 32");
                File.WriteAllText(mtlTemp, mtl.ToString(), new UTF8Encoding(false));
                File.Move(mtlTemp, mtlPath, overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            throw new IOException($"نوشتن فایل OBJ ممکن نشد ({ex.Message}).", ex);
        }
    }

    private static (double R, double G, double B) ParseHexColor(string hex)
    {
        var clean = hex.TrimStart('#');
        if (clean.Length != 6 || !uint.TryParse(clean, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return (0.72, 0.74, 0.76); // خاکستری پیش‌فرض در صورت رنگ نامعتبر.

        var r = (rgb >> 16) & 0xFF;
        var g = (rgb >> 8) & 0xFF;
        var b = rgb & 0xFF;
        return (r / 255.0, g / 255.0, b / 255.0);
    }

    private static string Fmt(double v) => v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

    private static string SanitizeName(string name) =>
        new(name.Where(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-').ToArray());
}

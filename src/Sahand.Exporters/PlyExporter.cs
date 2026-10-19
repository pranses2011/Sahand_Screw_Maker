using System.Buffers.Binary;
using System.Text;
using Sahand.Geometry.Meshing;

namespace Sahand.Exporters;

/// <summary>
/// خروجی‌گیر PLY (ASCII و باینری little-endian) — بدون وابستگی بیرونی.
/// </summary>
/// <remarks>
/// PLY واحد استاندارد ندارد؛ میلی‌متر مبنا است و در کامنت سربرگ ثبت می‌شود.
/// رنگ رأس نمایشی اختیاری است. نوشتن اتمیک.
/// </remarks>
public static class PlyExporter
{
    /// <summary>
    /// مش را به فایل PLY می‌نویسد.
    /// </summary>
    public static void Export(Mesh mesh, string filePath, ExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        try
        {
            if (options.AsciiPly)
                ExportAscii(mesh, filePath, options);
            else
                ExportBinary(mesh, filePath, options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            throw new IOException($"نوشتن فایل PLY ممکن نشد ({ex.Message}).", ex);
        }
    }

    private static string BuildHeader(Mesh mesh, bool ascii, ExportOptions options)
    {
        var hasColor = !string.IsNullOrWhiteSpace(options.MaterialHexColor);
        var sb = new StringBuilder();
        sb.AppendLine("ply");
        sb.AppendLine(ascii ? "format ascii 1.0" : "format binary_little_endian 1.0");
        sb.AppendLine($"comment Sahand Screw & Nut Designer — {options.PartName} — واحد مبنا: میلی‌متر");
        sb.AppendLine($"element vertex {mesh.Vertices.Length}");
        sb.AppendLine("property float x");
        sb.AppendLine("property float y");
        sb.AppendLine("property float z");
        if (hasColor)
        {
            sb.AppendLine("property uchar red");
            sb.AppendLine("property uchar green");
            sb.AppendLine("property uchar blue");
        }
        sb.AppendLine($"element face {mesh.Triangles.Length}");
        sb.AppendLine("property list uchar int vertex_indices");
        sb.AppendLine("end_header");
        return sb.ToString();
    }

    private static void ExportAscii(Mesh mesh, string filePath, ExportOptions options)
    {
        var (r, g, b) = ParseHexColor(options.MaterialHexColor);
        var sb = new StringBuilder(mesh.Vertices.Length * 32);
        sb.Append(BuildHeader(mesh, ascii: true, options));

        foreach (var v in mesh.Vertices)
        {
            var line = $"{Fmt(v.X * options.ScaleFactor)} {Fmt(v.Y * options.ScaleFactor)} {Fmt(v.Z * options.ScaleFactor)}";
            if (!string.IsNullOrWhiteSpace(options.MaterialHexColor))
                line += $" {r} {g} {b}";
            sb.AppendLine(line);
        }

        foreach (var t in mesh.Triangles)
            sb.AppendLine($"3 {t.A} {t.B} {t.C}");

        var tempPath = filePath + ".tmp-export";
        File.WriteAllText(tempPath, sb.ToString(), new UTF8Encoding(false));
        File.Move(tempPath, filePath, overwrite: true);
    }

    private static void ExportBinary(Mesh mesh, string filePath, ExportOptions options)
    {
        var hasColor = !string.IsNullOrWhiteSpace(options.MaterialHexColor);
        var (r, g, b) = ParseHexColor(options.MaterialHexColor);

        var tempPath = filePath + ".tmp-export";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write(Encoding.ASCII.GetBytes(BuildHeader(mesh, ascii: false, options)));

                Span<byte> vertexRow = hasColor ? stackalloc byte[15] : stackalloc byte[12];
                foreach (var v in mesh.Vertices)
                {
                    BinaryPrimitives.WriteSingleLittleEndian(vertexRow.Slice(0, 4), (float)(v.X * options.ScaleFactor));
                    BinaryPrimitives.WriteSingleLittleEndian(vertexRow.Slice(4, 4), (float)(v.Y * options.ScaleFactor));
                    BinaryPrimitives.WriteSingleLittleEndian(vertexRow.Slice(8, 4), (float)(v.Z * options.ScaleFactor));
                    if (hasColor)
                    {
                        vertexRow[12] = r; vertexRow[13] = g; vertexRow[14] = b;
                    }
                    writer.Write(vertexRow);
                }

                Span<byte> faceRow = stackalloc byte[13];
                foreach (var t in mesh.Triangles)
                {
                    faceRow[0] = 3;
                    BinaryPrimitives.WriteInt32LittleEndian(faceRow.Slice(1, 4), t.A);
                    BinaryPrimitives.WriteInt32LittleEndian(faceRow.Slice(5, 4), t.B);
                    BinaryPrimitives.WriteInt32LittleEndian(faceRow.Slice(9, 4), t.C);
                    writer.Write(faceRow);
                }
            }

            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch { /* نادیده. */ }
            throw;
        }
    }

    private static (byte R, byte G, byte B) ParseHexColor(string? hex)
    {
        var clean = hex?.TrimStart('#');
        if (clean is { Length: 6 } && uint.TryParse(clean, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
        {
            return ((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
        }
        return (0xB8, 0xBC, 0xC2);
    }

    private static string Fmt(double v) => v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
}

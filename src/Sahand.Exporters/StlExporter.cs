using System.Buffers.Binary;
using System.Text;
using Sahand.Geometry.Meshing;

namespace Sahand.Exporters;

/// <summary>
/// خروجی‌گیر STL باینری و ASCII — بدون هیچ وابستگی بیرونی.
/// </summary>
/// <remarks>
/// قرارداد: مثلث‌ها با نرمال محاسبه‌شده از winding (قانون دست راست) نوشته می‌شوند؛
/// STL واحد ندارد (سند ۴-۳): واحد انتخابی کاربر در گزارش خروجی و نام پیشنهادی ثبت
/// می‌شود و هشدار مرتبط در UI نمایش داده می‌شود. نوشتن اتمیک است (نوشتن به فایل موقت
/// و جابه‌جایی) تا خروجی نیمه‌کاره با پسوند معتبر روی دیسک نماند (EXP-04).
/// </remarks>
public static class StlExporter
{
    /// <summary>
    /// مش را به فایل STL می‌نویسد.
    /// </summary>
    /// <param name="mesh">مش با winding بیرونی معتبر.</param>
    /// <param name="filePath">مسیر مقصد.</param>
    /// <param name="options">تنظیمات (AsciiStl، ScaleFactor).</param>
    /// <exception cref="IOException">خطای دیسک با پیام فارسی.</exception>
    public static void Export(Mesh mesh, string filePath, ExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        try
        {
            if (options.AsciiStl)
                ExportAscii(mesh, filePath, options);
            else
                ExportBinary(mesh, filePath, options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            throw new IOException($"نوشتن فایل STL ممکن نشد ({ex.Message}). مسیر و دسترسی نوشتن را بررسی کنید.", ex);
        }
    }

    private static void ExportBinary(Mesh mesh, string filePath, ExportOptions options)
    {
        // سربرگ ۸۰ بایتی: شناسه برنامه + واحد (اطلاعاتی؛ STL واحد استاندارد ندارد).
        var headerText = $"Sahand Screw & Nut Designer - unit:{options.TargetUnit} - {options.PartName}";
        var header = new byte[80];
        Encoding.ASCII.GetBytes(headerText, 0, Math.Min(headerText.Length, 78), header, 0);

        var tempPath = filePath + ".tmp-export";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write(header);
                writer.Write((uint)mesh.Triangles.Length);

                Span<byte> triangleBuffer = stackalloc byte[50];
                foreach (var t in mesh.Triangles)
                {
                    var (a, b, c) = (mesh.Vertices[t.A], mesh.Vertices[t.B], mesh.Vertices[t.C]);
                    var normal = ComputeNormal(a, b, c);

                    WriteFloat(triangleBuffer, 0, (float)(normal.X));
                    WriteFloat(triangleBuffer, 4, (float)(normal.Y));
                    WriteFloat(triangleBuffer, 8, (float)(normal.Z));
                    WriteFloat(triangleBuffer, 12, (float)(a.X * options.ScaleFactor));
                    WriteFloat(triangleBuffer, 16, (float)(a.Y * options.ScaleFactor));
                    WriteFloat(triangleBuffer, 20, (float)(a.Z * options.ScaleFactor));
                    WriteFloat(triangleBuffer, 24, (float)(b.X * options.ScaleFactor));
                    WriteFloat(triangleBuffer, 28, (float)(b.Y * options.ScaleFactor));
                    WriteFloat(triangleBuffer, 32, (float)(b.Z * options.ScaleFactor));
                    WriteFloat(triangleBuffer, 36, (float)(c.X * options.ScaleFactor));
                    WriteFloat(triangleBuffer, 40, (float)(c.Y * options.ScaleFactor));
                    WriteFloat(triangleBuffer, 44, (float)(c.Z * options.ScaleFactor));
                    // دو بایت آخر «شمارندهٔ ویژگی» = صفر (استاندارد رایج).
                    triangleBuffer[48] = 0;
                    triangleBuffer[49] = 0;
                    writer.Write(triangleBuffer);
                }
            }

            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void ExportAscii(Mesh mesh, string filePath, ExportOptions options)
    {
        var sb = new StringBuilder(mesh.Triangles.Length * 60);
        sb.AppendLine("solid " + SanitizeName(options.PartName));

        foreach (var t in mesh.Triangles)
        {
            var (a, b, c) = (mesh.Vertices[t.A], mesh.Vertices[t.B], mesh.Vertices[t.C]);
            var n = ComputeNormal(a, b, c);
            sb.AppendLine($"  facet normal {Fmt(n.X)} {Fmt(n.Y)} {Fmt(n.Z)}");
            sb.AppendLine("    outer loop");
            sb.AppendLine($"      vertex {Fmt(a.X * options.ScaleFactor)} {Fmt(a.Y * options.ScaleFactor)} {Fmt(a.Z * options.ScaleFactor)}");
            sb.AppendLine($"      vertex {Fmt(b.X * options.ScaleFactor)} {Fmt(b.Y * options.ScaleFactor)} {Fmt(b.Z * options.ScaleFactor)}");
            sb.AppendLine($"      vertex {Fmt(c.X * options.ScaleFactor)} {Fmt(c.Y * options.ScaleFactor)} {Fmt(c.Z * options.ScaleFactor)}");
            sb.AppendLine("    endloop");
            sb.AppendLine("  endfacet");
        }
        sb.AppendLine("endsolid");

        var tempPath = filePath + ".tmp-export";
        try
        {
            File.WriteAllText(tempPath, sb.ToString(), new UTF8Encoding(false));
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    /// <summary>نرمال واحد مثلث از winding (قانون دست راست).</summary>
    private static (double X, double Y, double Z) ComputeNormal(MeshVertex a, MeshVertex b, MeshVertex c)
    {
        var ux = b.X - a.X; var uy = b.Y - a.Y; var uz = b.Z - a.Z;
        var vx = c.X - a.X; var vy = c.Y - a.Y; var vz = c.Z - a.Z;
        var nx = uy * vz - uz * vy;
        var ny = uz * vx - ux * vz;
        var nz = ux * vy - uy * vx;
        var length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        if (length < 1e-20) return (0, 0, 0); // وجه بی‌مساحت: نرمال صفر.
        return (nx / length, ny / length, nz / length);
    }

    private static void WriteFloat(Span<byte> buffer, int offset, float value) =>
        BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(offset, 4), value);

    private static string Fmt(double v) => v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

    private static string SanitizeName(string name) =>
        new(name.Where(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or ' ').ToArray());

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* نادیده — فایل موقت است. */ }
    }
}

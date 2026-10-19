using System.IO.Compression;
using System.Text;
using Sahand.Geometry.Meshing;

namespace Sahand.Exporters;

/// <summary>
/// خروجی‌گیر 3MF — بستهٔ OPC (ZIP) با manifest و مدل XML واحدِ صریح.
/// </summary>
/// <remarks>
/// ساختار مطابق هستهٔ 3MF: [Content_Types].xml، _rels/.rels و 3D/3dmodel.model با
/// فضای‌نام http://schemas.microsoft.com/3dmanufacturing/core/2015/02 و واحد میلی‌متر.
/// خوانندهٔ مستقل (ThreeMfReader) برای آزمون round-trip همین ساختار را validate می‌کند.
/// نوشتن اتمیک؛ در خطا بستهٔ نیمه‌کاره باقی نمی‌ماند.
/// </remarks>
public static class ThreeMfExporter
{
    private const string ModelNs = "http://schemas.microsoft.com/3dmanufacturing/core/2015/02";

    /// <summary>
    /// مش را به فایل 3MF می‌نویسد.
    /// </summary>
    public static void Export(Mesh mesh, string filePath, ExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var modelXml = BuildModelXml(mesh, options);
        var tempPath = filePath + ".tmp-export";

        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                AddEntry(archive, "[Content_Types].xml",
                    """
                    <?xml version="1.0" encoding="UTF-8"?>
                    <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                      <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                      <Default Extension="model" ContentType="application/vnd.ms-package.3dmanufacturing-3dmodel+xml"/>
                    </Types>
                    """);

                AddEntry(archive, "_rels/.rels",
                    """
                    <?xml version="1.0" encoding="UTF-8"?>
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                      <Relationship Target="/3D/3dmodel.model" Id="rel0" Type="http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel"/>
                    </Relationships>
                    """);

                AddEntry(archive, "3D/3dmodel.model", modelXml);
            }

            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch { /* نادیده. */ }
            throw;
        }
    }

    /// <summary>XML مدل با واحد صریح میلی‌متر و یک مش.</summary>
    private static string BuildModelXml(Mesh mesh, ExportOptions options)
    {
        var sb = new StringBuilder(mesh.Triangles.Length * 24 + 512);
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine($"""<model unit="millimeter" xml:lang="fa" xmlns="{ModelNs}">""");
        sb.AppendLine("  <metadata name=\"Title\">" + Escape(options.PartName) + "</metadata>");
        sb.AppendLine("  <metadata name=\"Application\">Sahand Screw &amp; Nut Designer</metadata>");
        sb.AppendLine("  <resources>");
        sb.AppendLine("    <object id=\"1\" type=\"model\">");
        sb.AppendLine("      <mesh>");
        sb.AppendLine("        <vertices>");

        foreach (var v in mesh.Vertices)
        {
            sb.AppendLine($"          <vertex x=\"{Fmt(v.X * options.ScaleFactor)}\" y=\"{Fmt(v.Y * options.ScaleFactor)}\" z=\"{Fmt(v.Z * options.ScaleFactor)}\"/>");
        }

        sb.AppendLine("        </vertices>");
        sb.AppendLine("        <triangles>");
        foreach (var t in mesh.Triangles)
        {
            sb.AppendLine($"          <triangle v1=\"{t.A}\" v2=\"{t.B}\" v3=\"{t.C}\"/>");
        }
        sb.AppendLine("        </triangles>");
        sb.AppendLine("      </mesh>");
        sb.AppendLine("    </object>");
        sb.AppendLine("  </resources>");
        sb.AppendLine("  <build>");
        sb.AppendLine("    <item objectid=\"1\"/>");
        sb.AppendLine("  </build>");
        sb.AppendLine("</model>");
        return sb.ToString();
    }

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string Fmt(double v) => v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
}

using System.IO.Compression;
using System.Xml.Linq;

namespace Sahand.Exporters;

/// <summary>
/// خوانندهٔ مستقل 3MF — اعتبارسنجی ساختار برای آزمون round-trip (EXP-02).
/// </summary>
/// <remarks>
/// بررسی می‌کند: ورودی‌های الزامی OPC، فضانام هستهٔ 3MF، وجود object/mesh/vertices/triangles،
/// بازهٔ اندیس‌ها و واحد فایل. این خواننده «بارورکننده» نیست و فقط صحت ساختاری را تأیید می‌کند.
/// </remarks>
public sealed record ThreeMfReadResult(
    int VertexCount,
    int TriangleCount,
    string Unit,
    double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ)
{
    /// <summary>ابعاد جعبهٔ محیطی.</summary>
    public (double Dx, double Dy, double Dz) Size => (MaxX - MinX, MaxY - MinY, MaxZ - MinZ);
}

public static class ThreeMfReader
{
    private const string ModelNs = "http://schemas.microsoft.com/3dmanufacturing/core/2015/02";

    /// <summary>
    /// فایل 3MF را باز و ساختار آن را validate می‌کند.
    /// </summary>
    /// <exception cref="InvalidDataException">با پیام فارسی در ساختار نامعتبر.</exception>
    public static ThreeMfReadResult Read(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        if (!File.Exists(filePath))
            throw new InvalidDataException("فایل 3MF یافت نشد.");

        using var archive = ZipFile.OpenRead(filePath);

        var modelEntry = archive.GetEntry("3D/3dmodel.model")
            ?? throw new InvalidDataException("ورودی 3D/3dmodel.model در بستهٔ 3MF نیست.");

        if (archive.GetEntry("[Content_Types].xml") is null)
            throw new InvalidDataException("[Content_Types].xml در بستهٔ 3MF نیست؛ ساختار OPC نامعتبر است.");

        XDocument doc;
        try
        {
            using var stream = modelEntry.Open();
            doc = XDocument.Load(stream);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException($"XML مدل 3MF قابل خواندن نیست: {ex.Message}");
        }

        var root = doc.Root ?? throw new InvalidDataException("مدل 3MF ریشه ندارد.");
        if (root.Name.NamespaceName != ModelNs)
            throw new InvalidDataException("فضانام مدل 3MF با هستهٔ استاندارد نمی‌خواند.");

        var unit = (string?)root.Attribute("unit") ?? "millimeter";

        var meshElement = root.Descendants(XName.Get("mesh", ModelNs)).FirstOrDefault()
            ?? throw new InvalidDataException("هیچ mesh در مدل 3MF نیست.");

        var vertices = meshElement.Descendants(XName.Get("vertex", ModelNs)).ToList();
        var triangles = meshElement.Descendants(XName.Get("triangle", ModelNs)).ToList();
        if (vertices.Count == 0 || triangles.Count == 0)
            throw new InvalidDataException("مش 3MF بدون رأس یا بدون وجه است.");

        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        foreach (var v in vertices)
        {
            var x = (double?)v.Attribute("x") ?? throw new InvalidDataException("رأس بدون مختصات x.");
            var y = (double?)v.Attribute("y") ?? throw new InvalidDataException("رأس بدون مختصات y.");
            var z = (double?)v.Attribute("z") ?? throw new InvalidDataException("رأس بدون مختصات z.");
            minX = Math.Min(minX, x); minY = Math.Min(minY, y); minZ = Math.Min(minZ, z);
            maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); maxZ = Math.Max(maxZ, z);
        }

        foreach (var t in triangles)
        {
            foreach (var attr in new[] { "v1", "v2", "v3" })
            {
                var index = (int?)t.Attribute(attr) ?? throw new InvalidDataException($"وجه بدون اندیس {attr}.");
                if (index < 0 || index >= vertices.Count)
                    throw new InvalidDataException($"اندیس وجه ({attr}={index}) خارج از بازهٔ رأس‌ها است.");
            }
        }

        return new ThreeMfReadResult(vertices.Count, triangles.Count, unit,
            minX, minY, minZ, maxX, maxY, maxZ);
    }
}

using System.Buffers.Binary;
using System.Text;

namespace Sahand.Exporters;

/// <summary>
/// خوانندهٔ مستقل STL — برای آزمون round-trip با مسیری جدا از خروجی‌گیر (الزام EXP-01/سند ۶-۳).
/// </summary>
/// <remarks>
/// این کلاس «بارورکننده» نیست؛ فقط خواندن ساختار را تأیید می‌کند: سربرگ، شمار مثلث،
/// نرمال‌ها و پیوستگی اندازهٔ فایل (۱۲ + ۵۰×N بایت). فایل ناقص/نامعتبر با پیام فارسی رد می‌شود.
/// </remarks>
public sealed record StlReadResult(int TriangleCount, double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ)
{
    /// <summary>ابعاد جعبهٔ محیطی خوانده‌شده.</summary>
    public (double Dx, double Dy, double Dz) Size => (MaxX - MinX, MaxY - MinY, MaxZ - MinZ);
}

/// <summary>خوانندهٔ STL باینری و ASCII.</summary>
public static class StlReader
{
    /// <summary>
    /// فایل STL باینری را می‌خواند و صحت ساختار/ابعاد را برمی‌گرداند.
    /// </summary>
    /// <exception cref="InvalidDataException">با پیام فارسی اگر فایل ناقص یا نامعتبر باشد.</exception>
    public static StlReadResult ReadBinary(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        if (!File.Exists(filePath))
            throw new InvalidDataException("فایل STL یافت نشد.");

        var bytes = File.ReadAllBytes(filePath);
        if (bytes.Length < 84)
            throw new InvalidDataException("فایل STL کوتاه‌تر از سربرگ است؛ ناقص یا نامعتبر.");

        var triangleCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(80, 4));
        var expectedLength = 84L + 50L * triangleCount;
        if (bytes.Length != expectedLength)
            throw new InvalidDataException(
                $"اندازهٔ فایل ({bytes.Length} بایت) با تعداد مثلث ({triangleCount}) نمی‌خواند؛ انتظار {expectedLength} بایت.");

        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

        var offset = 84;
        for (var i = 0; i < triangleCount; i++)
        {
            for (var v = 0; v < 3; v++)
            {
                var x = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(offset + 12 + v * 12, 4));
                var y = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(offset + 16 + v * 12, 4));
                var z = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(offset + 20 + v * 12, 4));
                minX = Math.Min(minX, x); minY = Math.Min(minY, y); minZ = Math.Min(minZ, z);
                maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); maxZ = Math.Max(maxZ, z);
            }
            offset += 50;
        }

        return new StlReadResult((int)triangleCount, minX, minY, minZ, maxX, maxY, maxZ);
    }

    /// <summary>
    /// فایل STL ASCII را می‌خواند و تعداد وجه‌ها و ابعاد را برمی‌گرداند.
    /// </summary>
    public static StlReadResult ReadAscii(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        if (!File.Exists(filePath))
            throw new InvalidDataException("فایل STL یافت نشد.");

        var content = File.ReadAllText(filePath);
        if (!content.StartsWith("solid", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("فایل ASCII با «solid» شروع نمی‌شود.");

        var triangleCount = 0;
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

        foreach (var lineToken in content.Split('\n'))
        {
            var line = lineToken.Trim();
            if (line.StartsWith("vertex", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 4 &&
                    double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) &&
                    double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) &&
                    double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z))
                {
                    minX = Math.Min(minX, x); minY = Math.Min(minY, y); minZ = Math.Min(minZ, z);
                    maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); maxZ = Math.Max(maxZ, z);
                }
            }
            else if (line.StartsWith("endfacet", StringComparison.OrdinalIgnoreCase))
            {
                triangleCount++;
            }
        }

        if (triangleCount == 0)
            throw new InvalidDataException("هیچ وجهی در فایل ASCII پیدا نشد.");

        return new StlReadResult(triangleCount, minX, minY, minZ, maxX, maxY, maxZ);
    }
}

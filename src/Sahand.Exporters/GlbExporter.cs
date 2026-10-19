using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Sahand.Geometry.Meshing;

namespace Sahand.Exporters;

/// <summary>
/// خروجی‌گیر glTF باینری (GLB 2.0) — تک‌مش با متریال پایه؛ بدون وابستگی بیرونی.
/// </summary>
/// <remarks>
/// قرارداد واحد (سند ۴-۳): glTF واحدش «متر» است؛ مختصات میلی‌متری با ضریب ۰٫۰۰۱
/// به متر تبدیل می‌شوند و این تبدیل در نام‌گذاری/گزارش خروجی ثبت می‌شود تا ابهام نماند.
/// ساختار GLB: سربرگ ۱۲ بایتی + JSON chunk + BIN chunk (with 4-byte alignment padding).
/// </remarks>
public static class GlbExporter
{
    /// <summary>ضریب تبدیل میلی‌متر به متر — واحد استاندارد glTF.</summary>
    public const double MillimetersToMeters = 0.001;

    /// <summary>
    /// مش را به فایل GLB می‌نویسد.
    /// </summary>
    public static void Export(Mesh mesh, string filePath, ExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var scaleFactor = options.ScaleFactor * MillimetersToMeters;

        // --- بافر باینری: رأس‌ها (float×۳) سپس اندیس‌ها (uint16 یا uint32 بر اساس اندازهٔ مش) ---
        var vertexCount = mesh.Vertices.Length;
        var use32BitIndices = vertexCount > ushort.MaxValue;
        var vertexBytes = new byte[vertexCount * 12];
        for (var i = 0; i < vertexCount; i++)
        {
            var v = mesh.Vertices[i];
            BinaryPrimitives.WriteSingleLittleEndian(vertexBytes.AsSpan(i * 12, 4), (float)(v.X * scaleFactor));
            BinaryPrimitives.WriteSingleLittleEndian(vertexBytes.AsSpan(i * 12 + 4, 4), (float)(v.Y * scaleFactor));
            BinaryPrimitives.WriteSingleLittleEndian(vertexBytes.AsSpan(i * 12 + 8, 4), (float)(v.Z * scaleFactor));
        }

        var triangleCount = mesh.Triangles.Length;
        var indexBytes = use32BitIndices ? new byte[triangleCount * 12] : new byte[triangleCount * 6];
        for (var i = 0; i < triangleCount; i++)
        {
            var t = mesh.Triangles[i];
            if (use32BitIndices)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(indexBytes.AsSpan(i * 12, 4), (uint)t.A);
                BinaryPrimitives.WriteUInt32LittleEndian(indexBytes.AsSpan(i * 12 + 4, 4), (uint)t.B);
                BinaryPrimitives.WriteUInt32LittleEndian(indexBytes.AsSpan(i * 12 + 8, 4), (uint)t.C);
            }
            else
            {
                BinaryPrimitives.WriteUInt16LittleEndian(indexBytes.AsSpan(i * 6, 2), (ushort)t.A);
                BinaryPrimitives.WriteUInt16LittleEndian(indexBytes.AsSpan(i * 6 + 2, 2), (ushort)t.B);
                BinaryPrimitives.WriteUInt16LittleEndian(indexBytes.AsSpan(i * 6 + 4, 2), (ushort)t.C);
            }
        }

        // ترازبندی ۴ بایتی بافر باینری (الزام GLB).
        var binLengthUnpadded = vertexBytes.Length + indexBytes.Length;
        var binPadding = (4 - (binLengthUnpadded % 4)) % 4;
        var binBytes = new byte[binLengthUnpadded + binPadding];
        Buffer.BlockCopy(vertexBytes, 0, binBytes, 0, vertexBytes.Length);
        Buffer.BlockCopy(indexBytes, 0, binBytes, vertexBytes.Length, indexBytes.Length);

        var indexBufferOffset = vertexBytes.Length;
        var (r, g, b) = ParseHexColor(options.MaterialHexColor);

        // --- سند JSON مربوط به glTF با نام‌گذاری فارسی قطعه ---
        var json = JsonSerializer.Serialize(new
        {
            asset = new { version = "2.0", generator = "Sahand Screw & Nut Designer" },
            scene = 0,
            scenes = new[] { new { nodes = new[] { 0 } } },
            nodes = new[] { new { mesh = 0, name = options.PartName } },
            meshes = new[]
            {
                new
                {
                    primitives = new[]
                    {
                        new
                        {
                            attributes = new { POSITION = 0 },
                            indices = 1,
                            material = 0,
                        },
                    },
                },
            },
            materials = new[]
            {
                new
                {
                    name = options.PartName + " (نمایشی)",
                    pbrMetallicRoughness = new
                    {
                        baseColorFactor = new[] { r, g, b, 1.0 },
                        metallicFactor = 0.1,
                        roughnessFactor = 0.6,
                    },
                },
            },
            buffers = new[] { new { byteLength = binBytes.Length } },
            bufferViews = new object[]
            {
                new { buffer = 0, byteOffset = 0, byteLength = vertexBytes.Length, target = 34962 },
                new { buffer = 0, byteOffset = indexBufferOffset, byteLength = indexBytes.Length, target = 34963 },
            },
            accessors = new object[]
            {
                new
                {
                    bufferView = 0,
                    componentType = 5126, // FLOAT
                    count = vertexCount,
                    type = "VEC3",
                    min = new[]
                    {
                        mesh.Vertices.Min(v => v.X) * scaleFactor,
                        mesh.Vertices.Min(v => v.Y) * scaleFactor,
                        mesh.Vertices.Min(v => v.Z) * scaleFactor,
                    },
                    max = new[]
                    {
                        mesh.Vertices.Max(v => v.X) * scaleFactor,
                        mesh.Vertices.Max(v => v.Y) * scaleFactor,
                        mesh.Vertices.Max(v => v.Z) * scaleFactor,
                    },
                },
                new
                {
                    bufferView = 1,
                    componentType = use32BitIndices ? 5125 : 5123, // UNSIGNED_INT/UNSIGNED_SHORT
                    count = triangleCount * 3,
                    type = "SCALAR",
                },
            },
        });

        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var jsonPadding = (4 - (jsonBytes.Length % 4)) % 4;
        var jsonChunkLength = jsonBytes.Length + jsonPadding;

        var tempPath = filePath + ".tmp-export";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(stream))
            {
                // سربرگ GLB: magic، نسخه، طول کل.
                writer.Write(0x46546C67); // "glTF"
                writer.Write(2u);
                var totalLength = 12 + 8 + jsonChunkLength + 8 + binBytes.Length;
                writer.Write((uint)totalLength);

                // chunk JSON.
                writer.Write((uint)jsonChunkLength);
                writer.Write(0x4E4F534A); // "JSON"
                writer.Write(jsonBytes);
                for (var i = 0; i < jsonPadding; i++) writer.Write((byte)0x20); // پد با فاصله.

                // chunk BIN.
                writer.Write((uint)binBytes.Length);
                writer.Write(0x004E4942); // "BIN\0"
                writer.Write(binBytes);
            }

            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch { /* نادیده. */ }
            throw;
        }
    }

    private static (float R, float G, float B) ParseHexColor(string? hex)
    {
        var clean = hex?.TrimStart('#');
        if (clean is { Length: 6 } && uint.TryParse(clean, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
        {
            return ((rgb >> 16 & 0xFF) / 255f, (rgb >> 8 & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        }
        return (0.72f, 0.74f, 0.76f);
    }
}

using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Sahand.Core.Project;

/// <summary>
/// خواننده/نویسندهٔ فایل پروژهٔ بومی «‎.ssnd» — بستهٔ ZIP با manifest نسخه‌دار (سند ۴-۶).
/// </summary>
/// <remarks>
/// دفاع‌های امنیتی (SEC-01/سند ۵-۶): سقف تعداد ورودی ZIP، سقف اندازهٔ نامتفاوت،
/// رد نام ورودی با مسیر مطلق یا پیمایش مسیر (../)، رد فایل‌های نامعادل بزرگ.
/// عملیات ذخیره اتمیک است: ابتدا در فایل موقت نوشته و سپس جایگزین می‌شود تا
/// قطع برق/کرش فایل قبلی سالم را خراب نکند (سند ۵-۵).
/// فایل حداکثر شامل manifest.json و thumbnail.png اختیاری است؛ هیچ باینری موتور حمل نمی‌شود.
/// </remarks>
public static class SsndFile
{
    /// <summary>نام ورودی manifest داخل بسته.</summary>
    public const string ManifestEntryName = "manifest.json";

    /// <summary>نام ورودی تصویر پیش‌نمایش اختیاری.</summary>
    public const string ThumbnailEntryName = "thumbnail.png";

    /// <summary>حداکثر تعداد ورودی‌های مجاز ZIP — بیشتر از آن نشانهٔ فایل خراب/ساخته‌شده است.</summary>
    public const int MaxZipEntries = 8;

    /// <summary>حداکثر اندازهٔ نامتفاوت هر ورودی — دفاع ZIP-bomb.</summary>
    public const long MaxEntryUncompressedBytes = 2 * 1024 * 1024;

    /// <summary>پسوند استاندارد پروژه.</summary>
    public const string Extension = ".ssnd";

    /// <summary>
    /// سند پروژه را به‌صورت اتمیک در مسیر داده‌شده ذخیره می‌کند.
    /// </summary>
    /// <param name="document">سند پارامتریک.</param>
    /// <param name="filePath">مسیر کامل فایل مقصد (پسوند ssnd).</param>
    /// <param name="thumbnailPng">تصویر پیش‌نمایش اختیاری (بایت‌های PNG).</param>
    /// <exception cref="IOException">با پیام فارسی در خطای دیسک/دسترسی.</exception>
    public static void Save(ProjectDocument document, string filePath, byte[]? thumbnailPng = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var manifestJson = JsonSerializer.Serialize(document, ProjectDocument.JsonOptions);

        // نوشتن اتمیک: ابتدا فایل موقت کنار مقصد، سپس جایگزینی؛ فایل موتی در خطا پاک می‌شود.
        var tempPath = filePath + ".tmp-save";
        try
        {
            using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
                using (var writer = new StreamWriter(manifestEntry.Open(), new UTF8Encoding(false)))
                {
                    writer.Write(manifestJson);
                }

                if (thumbnailPng is { Length: > 0 })
                {
                    var thumbEntry = archive.CreateEntry(ThumbnailEntryName, CompressionLevel.Optimal);
                    using (var stream = thumbEntry.Open())
                    {
                        stream.Write(thumbnailPng, 0, thumbnailPng.Length);
                    }
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

    /// <summary>
    /// فایل پروژه را با اعتبارسنجی کامل امنیتی باز می‌کند.
    /// </summary>
    /// <param name="filePath">مسیر فایل ssnd.</param>
    /// <returns>سند پروژهٔ بازسازی‌شده.</returns>
    /// <exception cref="InvalidDataException">با پیام فارسی اگر فایل خراب/ناامن/ناشناخته باشد.</exception>
    public static ProjectDocument Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        if (!File.Exists(filePath))
            throw new InvalidDataException("فایل پروژه یافت نشد.");

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length is < 1 or > 50 * 1024 * 1024)
            throw new InvalidDataException("اندازهٔ فایل پروژه خارج از محدودهٔ معقول است.");

        using var archive = ZipFile.OpenRead(filePath);
        if (archive.Entries.Count > MaxZipEntries)
            throw new InvalidDataException($"فایل پروژه بیش از {MaxZipEntries} ورودی دارد؛ ساختار نامعتبر است.");

        foreach (var entry in archive.Entries)
        {
            // رد پیمایش مسیر و نام مطلق — ورودی معتبر فقط نام سادهٔ بدون پوشه است.
            if (entry.FullName.Contains("..") || Path.IsPathRooted(entry.FullName) ||
                entry.FullName.Contains('\\') || entry.FullName.Contains('/'))
            {
                throw new InvalidDataException($"نام ورودی «{entry.FullName}» در فایل پروژه مجاز نیست.");
            }

            if (entry.Length > MaxEntryUncompressedBytes)
                throw new InvalidDataException(
                    $"ورودی «{entry.FullName}» بیش از حد بزرگ است ({entry.Length} بایت).");
        }

        var manifest = archive.GetEntry(ManifestEntryName)
            ?? throw new InvalidDataException("فایل پروژه manifest ندارد؛ فایل ناقص یا نامعتبر است.");

        ProjectDocument? document;
        try
        {
            using var stream = manifest.Open();
            document = JsonSerializer.Deserialize<ProjectDocument>(stream, ProjectDocument.JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"محتوای manifest قابل خواندن نیست: {ex.Message}");
        }

        if (document is null)
            throw new InvalidDataException("manifest خالی است.");

        // schema یکسان یا قدیمی‌تر پذیرفته می‌شود؛ نسخهٔ جدیدتر با هشدارِ «دادهٔ ممکن است ناقص باشد».
        // (در نسخهٔ ۱ همهٔ فیلدها شناخته‌شده‌اند؛ مهاجرت آینده اینجا اضافه می‌شود.)
        return document;
    }

    /// <summary>
    /// تصویر پیش‌نمایش پروژه را می‌خواند (اگر وجود داشته باشد).
    /// </summary>
    public static byte[]? TryLoadThumbnail(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        if (!File.Exists(filePath)) return null;

        using var archive = ZipFile.OpenRead(filePath);
        var thumb = archive.GetEntry(ThumbnailEntryName);
        if (thumb is null || thumb.Length <= 0 || thumb.Length > MaxEntryUncompressedBytes) return null;

        using var stream = thumb.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static void TryDelete(string path)
    {
        // پاک‌سازی بهترین‌تلاش: خطای پاک‌سازی نباید خطای اصلی ذخیره را بپوشاند.
        try { File.Delete(path); } catch { /* نادیده گرفته می‌شود — فایل موقت است. */ }
    }
}

using System.Text.Json;
using Sahand.Core.Threads;

namespace Sahand.Core.Catalog;

/// <summary>
/// کاتالوگ رزوهٔ کاربر — درون‌ریزی/برون‌بری JSON با اعتبارسنجی کامل و بدون اجرای هیچ کد (سند ۳-۸).
/// </summary>
/// <remarks>
/// فرمت فایل: آرایه‌ای از اشیای {sizeLabel, nominalDiameterMm, pitchMm, tpi?, series?, sourceNote?}.
/// دادهٔ ورودی غیرقابل اعتماد فرض می‌شود: سقف اندازهٔ فایل، سقف تعداد مدخل، رد مقادیر نامعتبر.
/// نسخهٔ schema در ریشهٔ سند با فیلد «schemaVersion» حمل می‌شود (الزام سند ۳-۸).
/// </remarks>
public static class UserCatalog
{
    /// <summary>نسخهٔ جاری schema کاتالوگ کاربر.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>حداکثر بایت‌های فایل کاتالوگ کاربر — دفاع در برابر فایل‌های غیرعادی بزرگ.</summary>
    public const int MaxFileBytes = 512 * 1024;

    /// <summary>حداکثر تعداد مدخل‌های مجاز.</summary>
    public const int MaxEntries = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// فایل JSON کاتالوگ کاربر را می‌خواند و با اعتبارسنجی کامل به مدخل تبدیل می‌کند.
    /// </summary>
    /// <param name="filePath">مسیر فایل JSON.</param>
    /// <returns>فهرست مدخل‌های معتبر.</returns>
    /// <exception cref="InvalidDataException">با پیام فارسی اگر فایل نامعتبر/خیلی بزرگ/ناشناخته باشد.</exception>
    public static IReadOnlyList<CatalogEntry> Import(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            throw new InvalidDataException("فایل کاتالوگ یافت نشد.");

        if (fileInfo.Length > MaxFileBytes)
            throw new InvalidDataException(
                $"اندازهٔ فایل کاتالوک ({fileInfo.Length} بایت) از سقف مجاز ({MaxFileBytes} بایت) بیشتر است.");

        var json = File.ReadAllText(filePath);
        return Parse(json);
    }

    /// <summary>
    /// متن JSON کاتالوگ را اعتبارسنجی و تبدیل می‌کند (بدون هیچ اجرای کد از داده).
    /// </summary>
    public static IReadOnlyList<CatalogEntry> Parse(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 8, // عمق محدود: دادهٔ کاتالوگ مسطح است؛ عمق بیشتر نشانهٔ ساختار ناشناخته است.
                AllowTrailingCommas = false,
            });
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"فایل کاتالوگ JSON معتبر نیست: {ex.Message}");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("ریشهٔ فایل کاتالوگ باید شیء JSON باشد.");

            if (!root.TryGetProperty("schemaVersion", out var versionEl) ||
                !versionEl.TryGetInt32(out var version) || version != CurrentSchemaVersion)
                throw new InvalidDataException(
                    $"فیلد schemaVersion باید عدد {CurrentSchemaVersion} باشد؛ فایل‌های نسخهٔ دیگر پشتیبانی نمی‌شوند.");

            if (!root.TryGetProperty("entries", out var entriesEl) || entriesEl.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("فیلد entries باید آرایه باشد.");

            if (entriesEl.GetArrayLength() > MaxEntries)
                throw new InvalidDataException($"تعداد مدخل‌ها از سقف {MaxEntries} بیشتر است.");

            var entries = new List<CatalogEntry>();
            var index = 0;
            foreach (var item in entriesEl.EnumerateArray())
            {
                index++;
                entries.Add(ParseEntry(item, index));
            }

            return entries;
        }
    }

    /// <summary>یک مدخل را با رد مقادیر نامعتبر می‌خواند (ورودی غیرقابل اعتماد — SEC-01).</summary>
    private static CatalogEntry ParseEntry(JsonElement item, int index)
    {
        if (item.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"مدخل {index} باید شیء باشد.");

        if (!item.TryGetProperty("sizeLabel", out var labelEl) ||
            labelEl.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(labelEl.GetString()) ||
            labelEl.GetString()!.Length > 32)
            throw new InvalidDataException($"مدخل {index}: فیلد sizeLabel رشتهٔ ۱ تا ۳۲ نویسه لازم دارد.");

        if (!item.TryGetProperty("nominalDiameterMm", out var nominalEl) ||
            nominalEl.ValueKind != JsonValueKind.Number)
            throw new InvalidDataException($"مدخل {index}: فیلد nominalDiameterMm عدد لازم دارد.");

        if (!item.TryGetProperty("pitchMm", out var pitchEl) || pitchEl.ValueKind != JsonValueKind.Number)
            throw new InvalidDataException($"مدخل {index}: فیلد pitchMm عدد لازم دارد.");

        var nominal = nominalEl.GetDouble();
        var pitch = pitchEl.GetDouble();

        if (nominal <= 0 || double.IsNaN(nominal) || double.IsInfinity(nominal))
            throw new InvalidDataException($"مدخل {index}: قطر اسمی باید عددی مثبت و متناهی باشد.");

        if (pitch <= 0 || double.IsNaN(pitch) || double.IsInfinity(pitch))
            throw new InvalidDataException($"مدخل {index}: گام باید عددی مثبت و متناهی باشد.");

        if (pitch > nominal / 2.0)
            throw new InvalidDataException(
                $"مدخل {index}: گام نباید از نصف قطر بیشتر باشد (هندسهٔ نامعقول).");

        int? tpi = null;
        if (item.TryGetProperty("tpi", out var tpiEl))
        {
            if (tpiEl.ValueKind != JsonValueKind.Number || !tpiEl.TryGetInt32(out var t) || t <= 0)
                throw new InvalidDataException($"مدخل {index}: فیلد tpi باید عدد صحیح مثبت باشد.");
            tpi = t;
        }

        var series = "کاربر";
        if (item.TryGetProperty("series", out var seriesEl))
        {
            series = seriesEl.GetString() is { Length: > 0 and <= 32 } s ? s : series;
        }

        var sourceNote = "کاتالوگ کاربر — منبع توسط کاربر تأیید نشده.";
        if (item.TryGetProperty("sourceNote", out var noteEl))
        {
            sourceNote = noteEl.GetString() is { Length: > 0 and <= 200 } n ? n : sourceNote;
        }

        return new CatalogEntry(labelEl.GetString()!, nominal, pitch, tpi, series, sourceNote);
    }

    /// <summary>
    /// کاتالوگ کاربر را به JSON نسخه‌دار برون‌بری می‌کند.
    /// </summary>
    public static string Export(IReadOnlyList<CatalogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var payload = new
        {
            schemaVersion = CurrentSchemaVersion,
            entries = entries.Select(e => new
            {
                sizeLabel = e.SizeLabel,
                nominalDiameterMm = e.NominalDiameterMm,
                pitchMm = e.PitchMm,
                tpi = e.Tpi,
                series = e.Series,
                sourceNote = e.SourceNote,
            }),
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}

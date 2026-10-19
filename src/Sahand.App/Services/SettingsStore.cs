using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sahand.Core.Units;

namespace Sahand.App.Services;

/// <summary>
/// تنظیمات محلی برنامه — JSON ساده در پوشهٔ دادهٔ کاربر (بدون هیچ دادهٔ حساس).
/// </summary>
/// <remarks>
/// راز SMTP هرگز اینجا ذخیره نمی‌شود (DPAPI جدا است — سند ۵-۳). تنظیمات پیش‌فرض
/// «بدون تله‌متری/بدون ارسال خودکار» است (الزام سند ۵-۴).
/// </remarks>
public sealed record AppSettings
{
    /// <summary>واحد ورود/نمایش پیش‌فرض.</summary>
    public LengthUnit Unit { get; init; } = LengthUnit.Millimeter;

    /// <summary>نمایش ارقام فارسی (فقط ظاهر متن؛ مقدار عددی تاثیر نمی‌پذیرد).</summary>
    public bool PersianDigits { get; init; }

    /// <summary>تعداد رقم اعشار نمایش.</summary>
    public int DecimalPlaces { get; init; } = 2;

    /// <summary>بازهٔ ذخیرهٔ خودکار (Autosafe) برحسب دقیقه؛ صفر = خاموش.</summary>
    public int AutosaveMinutes { get; init; } = 3;

    /// <summary>رضایت ارسال خودکار خطا — پیش‌فرض خاموش (الزام سند ۵-۴).</summary>
    public bool AutoReportEnabled { get; init; }

    /// <summary>فعال‌بودن جمع‌آوری داده در گزارش‌های دستی (پیش‌نمایش همیشه قبل از ارسال دیده می‌شود).</summary>
    public bool IncludeDiagnosticData { get; init; } = true;

    /// <summary>آخرین پوشهٔ خروجی.</summary>
    public string LastExportFolder { get; init; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    /// <summary>مادهٔ نمایشی پیش‌فرض (فقط ظاهر).</summary>
    public string MaterialName { get; init; } = "فولاد (نمایشی)";
}

/// <summary>
/// ذخیره/بارگذاری امن تنظیمات — نوشتن اتمیک؛ فایل خراب به تنظیمات پیش‌فرض برمی‌گردد.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _settingsPath;

    /// <summary>ساخت ذخیره‌ساز با مسیر فایل.</summary>
    public SettingsStore(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(settingsPath);
        _settingsPath = settingsPath;
        Current = Load();
    }

    /// <summary>تنظیمات جاری.</summary>
    public AppSettings Current { get; private set; }

    /// <summary>ذخیرهٔ تنظیمات (اتمیک؛ خطا برنامه را نمی‌شکند).</summary>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Current = settings;
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var temp = _settingsPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temp, _settingsPath, overwrite: true);
        }
        catch (IOException)
        {
            // تنظیمات برای اجرای بعد ذخیره نشد؛ برنامهٔ فعلی با مقادیر حافظه ادامه می‌دهد.
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return new AppSettings();
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return new AppSettings();
        }
    }
}

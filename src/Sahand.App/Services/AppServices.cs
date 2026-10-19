using System.IO;
using Sahand.App.Services;
using Sahand.Reporting;
using Sahand.Reporting.Smtp;

namespace Sahand.App.Services;

/// <summary>
/// ریشهٔ ترکیب برنامه — ساخته‌شده در راه‌اندازی و در دسترس همهٔ نماها (DI سبک).
/// </summary>
/// <remarks>
/// همهٔ مسیرها محلی کاربر است (بدون شبکه). تنظیمات SMTP در فایل تنظیماتِ متن ساده
/// «بدون رمز» نگهداری می‌شود؛ رمز فقط با DPAPI. این ساختار روی Windows معنا دارد؛
/// اجزای غیر-Windows (مثل DPAPI) fail-safe رفتار می‌کنند.
/// </remarks>
public sealed class AppServices
{
    /// <summary>پوشهٔ دادهٔ محلی برنامه.</summary>
    public string DataFolder { get; }

    /// <summary>ذخیرهٔ تنظیمات.</summary>
    public SettingsStore Settings { get; }

    /// <summary>مخزن راز DPAPI.</summary>
    public DpapiSecretStore Secrets { get; }

    /// <summary>صف گزارش‌ها.</summary>
    public ReportQueue ReportQueue { get; }

    /// <summary>سرویس گزارش/ارسال.</summary>
    public ReportService Reports { get; }

    /// <summary>Autosafe.</summary>
    public AutosaveService Autosave { get; }

    /// <summary>تنظیمات SMTP (بدون رمز — رمز در SecretStore).</summary>
    public SmtpSettingsStore SmtpSettings { get; }

    /// <summary>نسخهٔ برنامه برای فرادادهٔ گزارش.</summary>
    public string AppVersion { get; } = "0.1.0";

    /// <summary>ساخت سرویس‌ها با پوشهٔ دادهٔ پیش‌فرض کاربر.</summary>
    public AppServices()
    {
        DataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SahandScrewNutDesigner");
        Directory.CreateDirectory(DataFolder);

        Settings = new SettingsStore(Path.Combine(DataFolder, "settings.json"));
        Secrets = new DpapiSecretStore(Path.Combine(DataFolder, "smtp-secret.bin"));
        ReportQueue = new ReportQueue(Path.Combine(DataFolder, "report-queue.json"));
        SmtpSettings = new SmtpSettingsStore(Path.Combine(DataFolder, "smtp-settings.json"));
        Reports = new ReportService(ReportQueue, () => SmtpSettings.Current, Secrets);
        Autosave = new AutosaveService(Path.Combine(DataFolder, "recovery.json"));
    }

    /// <summary>خاموشی تمیز — ذخیرهٔ تنظیمات و پاک‌سازی بازیابی در صورت نیاز.</summary>
    public void Shutdown()
    {
        // صف گزارش‌ها در هر عملیات خودشان ذخیره می‌شوند؛ کاری لازم نیست.
        // تنظیمات تغییر یافته توسط نما در همان لحظه ذخیره می‌شوند.
    }
}

/// <summary>
/// ذخیرهٔ تنظیمات SMTP (بدون رمز) — فایل JSON جدا از تنظیمات عمومی.
/// </summary>
public sealed class SmtpSettingsStore
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _path;

    /// <summary>تنظیمات جاری؛ null تا وقتی کاربر پیکربندی کرده باشد.</summary>
    public SmtpSettings? Current { get; private set; }

    /// <summary>ساخت ذخیره‌ساز.</summary>
    public SmtpSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        _path = path;
        Load();
    }

    /// <summary>ذخیرهٔ تنظیمات (اتمیک) و به‌روزرسانی مقدار جاری.</summary>
    public void Save(SmtpSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Current = settings;
        try
        {
            var temp = _path + ".tmp";
            File.WriteAllText(temp, System.Text.Json.JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temp, _path, overwrite: true);
        }
        catch (IOException)
        {
            // ذخیره نشد؛ مقدار حافظه معتبر است برای جلسهٔ فعلی.
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var json = File.ReadAllText(_path);
            Current = System.Text.Json.JsonSerializer.Deserialize<SmtpSettings>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
        {
            Current = null;
        }
    }
}

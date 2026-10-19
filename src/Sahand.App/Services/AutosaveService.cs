using System.IO;
using System.Text.Json;
using Sahand.Core.Project;

namespace Sahand.App.Services;

/// <summary>
/// ذخیرهٔ خودکار (Autosafe) و بازیابی پس از بسته‌شدن غیرمنتظره (الزام P0 سند ۱-۶).
/// </summary>
/// <remarks>
/// نسخهٔ در حال ویرایش به‌صورت دوره‌ای در فایل بازیابی محلی نوشته می‌شود (اتمیک).
/// در راه‌اندازی بعدی، وجود فایل بازیابی به کاربر پیشنهاد ادامه می‌دهد؛ پذیرش/رد
/// با کنش خود کاربر است. هیچ دادهٔ حساس در بازیابی وجود ندارد (پروژه پارامتریک است).
/// </remarks>
public sealed class AutosaveService
{
    private readonly string _recoveryPath;
    private readonly JsonSerializerOptions _jsonOptions = ProjectDocument.JsonOptions;

    /// <summary>ساخت سرویس با مسیر فایل بازیابی.</summary>
    public AutosaveService(string recoveryPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(recoveryPath);
        _recoveryPath = recoveryPath;
    }

    /// <summary>آیا فایل بازیابی از جلسهٔ قبل وجود دارد؟</summary>
    public bool HasRecovery => File.Exists(_recoveryPath) && new FileInfo(_recoveryPath).Length > 0;

    /// <summary>
    /// نوشتن نسخهٔ بازیابی — هر تغییر مهم پروژه اینجا می‌نشیند.
    /// </summary>
    public void WriteRecovery(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        try
        {
            var directory = Path.GetDirectoryName(_recoveryPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var temp = _recoveryPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(document, _jsonOptions));
            File.Move(temp, _recoveryPath, overwrite: true);
        }
        catch (IOException)
        {
            // خطای بازیابی نباید کار طراحی را متوقف کند؛ جلسه با نسخهٔ قبلی ادامه می‌یابد.
        }
    }

    /// <summary>
    /// خواندن پروژهٔ بازیابی؛ null اگر فایل نبود یا قابل خواندن نبود.
    /// </summary>
    public ProjectDocument? TryReadRecovery()
    {
        try
        {
            if (!HasRecovery) return null;
            var json = File.ReadAllText(_recoveryPath);
            return JsonSerializer.Deserialize<ProjectDocument>(json, _jsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    /// <summary>پاک‌کردن بازیابی (پس از ذخیرهٔ موفق یا رد کاربر).</summary>
    public void ClearRecovery()
    {
        try { File.Delete(_recoveryPath); } catch (IOException) { /* نادیده. */ }
    }
}

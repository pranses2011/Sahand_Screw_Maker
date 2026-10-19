using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sahand.Reporting;

/// <summary>
/// صف محلی گزارش‌ها — ذخیرهٔ JSON ساده و شفاف؛ ابزار آزمون بازخورد و حذف کاربر.
/// </summary>
/// <remarks>
/// صف با دادهٔ پارامتریک پروژه و تنظیمات جدا است (سند ۵-۵). فایل JSON ساده است تا
/// کاربر بتواند آن را باز کند و ببیند (شفافیت)؛ رمزنگاری محتوا در لایهٔ App با DPAPI
/// انجام می‌شود و این لایه مستقل از سیستم‌عامل می‌ماند. نوشتن اتمیک است.
/// </remarks>
public sealed class ReportQueue
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _queuePath;
    private readonly object _sync = new();
    private List<QueuedReport> _reports;

    /// <summary>ایجاد صف با مسیر فایل ذخیره‌سازی.</summary>
    public ReportQueue(string queueFilePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueFilePath);
        _queuePath = queueFilePath;
        _reports = Load();
    }

    /// <summary>گزارش‌های صف (کپی برای ایمنی رشته‌های UI).</summary>
    public IReadOnlyList<QueuedReport> Reports
    {
        get
        {
            lock (_sync) return _reports.ToList();
        }
    }

    /// <summary>
    /// گزارش جدید را می‌افزاید و ذخیره می‌کند.
    /// </summary>
    public QueuedReport Enqueue(QueuedReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        lock (_sync)
        {
            _reports.Add(report);
            SaveLocked();
        }
        return report;
    }

    /// <summary>
    /// گزارش را حذف می‌کند (حق حذف کاربر — MAIL-05/PRIV).
    /// </summary>
    public bool Remove(Guid id)
    {
        lock (_sync)
        {
            var removed = _reports.RemoveAll(r => r.Id == id) > 0;
            if (removed) SaveLocked();
            return removed;
        }
    }

    /// <summary>
    /// ویرایش متن گزارش صف‌شده پیش از ارسال (سند ۲-۷: امکان ویرایش).
    /// </summary>
    public bool UpdateBody(Guid id, string newBody)
    {
        lock (_sync)
        {
            var report = _reports.FirstOrDefault(r => r.Id == id);
            if (report is null) return false;

            var scrubbed = Redaction.Scrub(newBody);
            _reports.Remove(report);
            _reports.Add(report with { Body = scrubbed.Text, RedactionNotes = scrubbed.Notes });
            SaveLocked();
            return true;
        }
    }

    /// <summary>
    /// وضعیت/نتیجهٔ ارسال گزارش را ثبت می‌کند.
    /// </summary>
    public void MarkStatus(Guid id, ReportStatus status, string? error = null)
    {
        lock (_sync)
        {
            var report = _reports.FirstOrDefault(r => r.Id == id);
            if (report is null) return;

            _reports.Remove(report);
            var updated = report with
            {
                LastError = error ?? report.LastError,
                LastAttemptAt = DateTimeOffset.Now,
            };
            updated.Status = status;
            if (status is ReportStatus.Sending or ReportStatus.Sent or ReportStatus.Failed)
                updated.AttemptCount++;

            _reports.Add(updated);
            SaveLocked();
        }
    }

    /// <summary>پاک‌سازی گزارش‌های ارسال‌شده/لغوشده قدیمی — فقط نتیجهٔ نهایی می‌ماند.</summary>
    public void PruneFinishedOlderThan(TimeSpan age)
    {
        lock (_sync)
        {
            var cutoff = DateTimeOffset.Now - age;
            var before = _reports.Count;
            _reports.RemoveAll(r => r.Status is ReportStatus.Sent or ReportStatus.Cancelled && r.CreatedAt < cutoff);
            if (_reports.Count != before) SaveLocked();
        }
    }

    private List<QueuedReport> Load()
    {
        try
        {
            if (!File.Exists(_queuePath)) return [];
            var json = File.ReadAllText(_queuePath);
            var list = JsonSerializer.Deserialize<List<QueuedReport>>(json, JsonOptions);
            return list ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // صف خراب نباید برنامه را از کار بیندازد؛ فایل خراب کنار گذاشته و از نو شروع می‌شود
            // (نسخهٔ خراب با پسوند corrupt نگه داشته می‌شود تا داده ناخواسته حذف نشود).
            try
            {
                if (File.Exists(_queuePath))
                    File.Move(_queuePath, _queuePath + ".corrupt-" + DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss"));
            }
            catch { /* نادیده. */ }
            return [];
        }
    }

    private void SaveLocked()
    {
        var tempPath = _queuePath + ".tmp-save";
        try
        {
            var directory = Path.GetDirectoryName(_queuePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(tempPath, JsonSerializer.Serialize(_reports, JsonOptions));
            File.Move(tempPath, _queuePath, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch { /* نادیده. */ }
            // خطای ذخیرهٔ صف نباید برنامهٔ طراحی را خراب کند (سند ۵-۳: خطای ارسال جدا از هسته).
        }
    }
}

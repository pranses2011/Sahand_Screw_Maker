using Sahand.Reporting;
using Sahand.Reporting.Smtp;

namespace Sahand.App.Services;

/// <summary>
/// سرویس گزارش برنامه — الصاق صف محلی، transport و رضایت کاربر (پوشش MAIL-01..05).
/// </summary>
/// <remarks>
/// ارسال فقط با: (۱) رضایت صریح، (۲) پیکربندی کامل، (۳) شبکه — انجام می‌شود و وضعیت
/// «ارسال شد» فقط پس از پاسخ مثبت واقعی transport ثبت می‌گردد. در نبود هر شرط،
/// گزارش در صف می‌ماند و وضعیت آن برای کاربر شفاف است.
/// </remarks>
public sealed class ReportService
{
    private readonly ReportQueue _queue;
    private readonly Func<SmtpSettings?> _settingsProvider;
    private readonly Reporting.ISecretProvider _secrets;

    /// <summary>ساخت سرویس با صف و منابع تنظیمات/راز.</summary>
    public ReportService(ReportQueue queue, Func<SmtpSettings?> settingsProvider, Reporting.ISecretProvider secrets)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
    }

    /// <summary>صف گزارش‌ها (برای نمایش/ویرایش/حذف کاربر).</summary>
    public ReportQueue Queue => _queue;

    /// <summary>
    /// ثبت گزارش خطا با پاک‌سازی متن و برچسب رضایت.
    /// </summary>
    public QueuedReport ReportError(string module, string message, bool userConsented)
    {
        // پاک‌سازی متن خطا پیش از هر ذخیره — حتی در صف محلی (الزام PRIV-01).
        var scrubbed = Redaction.Scrub(message);
        return _queue.Enqueue(new QueuedReport
        {
            Kind = ReportKind.Error,
            Module = module,
            Body = scrubbed.Text,
            RedactionNotes = scrubbed.Notes,
            UserConsented = userConsented,
        });
    }

    /// <summary>
    /// ثبت بازخورد کاربر (متن/موضوع/تماس داوطلبانه).
    /// </summary>
    public QueuedReport ReportFeedback(string subject, string body, string? contactEmail, bool includeDiagnostics, string appVersion)
    {
        var scrubbed = Redaction.Scrub(body);
        return _queue.Enqueue(new QueuedReport
        {
            Kind = ReportKind.Feedback,
            Subject = subject,
            Body = scrubbed.Text,
            ContactEmail = contactEmail,
            RedactionNotes = scrubbed.Notes,
            UserConsented = true, // بازخورد همیشه با کنش خود کاربر.
            AppVersion = appVersion,
        });
    }

    /// <summary>
    /// وضعیت آمادهٔ ارسال: تنظیمات + راز هر دو موجودند؟
    /// </summary>
    public bool IsTransportConfigured()
    {
        var settings = _settingsProvider();
        return settings is not null && settings.IsConfigured && !string.IsNullOrEmpty(_secrets.GetSmtpPassword());
    }

    /// <summary>
    /// تلاش برای ارسال همهٔ گزارش‌های صف‌شدهٔ دارای رضایت — با رفتار fail-safe.
    /// </summary>
    /// <returns>تعداد ارسال‌شده و خطاهای فارسی (برای نمایش وضعیت).</returns>
    public async Task<(int Sent, IReadOnlyList<string> Errors)> SendQueuedAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settingsProvider();
        var sent = 0;
        var errors = new List<string>();

        if (settings is null || !settings.IsConfigured)
        {
            errors.Add("ارسال نشده — پیکربندی لازم است: تنظیمات SMTP کامل نیست.");
            return (0, errors);
        }

        var password = _secrets.GetSmtpPassword();
        if (string.IsNullOrEmpty(password))
        {
            errors.Add("ارسال نشده — پیکربندی لازم است: رمز سرویس ایمیل ثبت نشده است.");
            return (0, errors);
        }

        var transport = new SmtpTransport(settings, _secrets);

        foreach (var report in _queue.Reports.Where(r => r.Status is ReportStatus.Queued or ReportStatus.Failed && r.UserConsented).ToList())
        {
            _queue.MarkStatus(report.Id, ReportStatus.Sending);

            // محدودیت تلاش (سند ۵-۳): بیش از ۵ تلاش، گزارش ناموفق می‌ماند و دیگر ارسال نمی‌شود خودکار.
            if (report.AttemptCount >= 5)
            {
                _queue.MarkStatus(report.Id, ReportStatus.Failed, "تعداد تلاش‌ها به سقف رسید.");
                errors.Add($"گزارش {report.Id.ToString()[..8]}: تعداد تلاش‌ها به سقف رسید.");
                continue;
            }

            var outcome = await transport.SendAsync(report, cancellationToken);
            if (outcome.Success)
            {
                _queue.MarkStatus(report.Id, ReportStatus.Sent);
                sent++;
            }
            else
            {
                _queue.MarkStatus(report.Id, ReportStatus.Failed, outcome.PersianError);
                errors.Add($"گزارش {report.Id.ToString()[..8]}: {outcome.PersianError}");
            }
        }

        return (sent, errors);
    }
}

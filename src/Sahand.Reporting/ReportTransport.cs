using Sahand.Reporting.Smtp;

namespace Sahand.Reporting;

/// <summary>
/// واسط انتقال گزارش — نقطهٔ تعویض‌پذیر میان SMTP و relay آینده (سند ۵-۳).
/// </summary>
public interface IReportTransport
{
    /// <summary>نام فارسی transport برای UI.</summary>
    string Name { get; }

    /// <summary>آیا برای ارسال پیکربندی شده است؟ (بدون موفقیت کاذب)</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// گزارش را ارسال می‌کند — فقط پس از پاسخ مثبت واقعی موفق گزارش می‌شود.
    /// </summary>
    Task<TransportOutcome> SendAsync(QueuedReport report, CancellationToken cancellationToken = default);
}

/// <summary>نتیجهٔ transport — جدا از خطای هندسه/فایل (سند ۵-۶).</summary>
public sealed record TransportOutcome(
    bool Success,
    string? PersianError,
    string DiagnosticLog)
{
    /// <summary>نتیجهٔ موفق.</summary>
    public static TransportOutcome Ok(string log) => new(true, null, log);
}

/// <summary>
/// Transport SMTP — رمز را از راه‌کار بیرونی (SecretStore لایهٔ App) می‌گیرد.
/// </summary>
/// <remarks>
/// رابط ISecretProvider عمداً در این لایه تعریف می‌شود تا Reporting مستقل از Windows بماند؛
/// پیاده‌سازی DPAPI فقط در App قرار دارد و در محیط‌های دیگر پیاده‌سازی آزمایشی/خطادار امن.
/// </remarks>
public interface ISecretProvider
{
    /// <summary>رمز SMTP را از مخزن امن می‌خواند؛ null یعنی «پیکربندی نشده».</summary>
    string? GetSmtpPassword();

    /// <summary>رمز را در مخزن امن ذخیره می‌کند (فقط در App با DPAPI).</summary>
    void StoreSmtpPassword(string password);
}

/// <summary>Transport SMTP با تنظیمات و مخزن رمز تزریق‌شده.</summary>
public sealed class SmtpTransport : IReportTransport
{
    private readonly SmtpSettings _settings;
    private readonly ISecretProvider _secrets;
    private readonly RawSmtpClient _client = new();

    /// <summary>مقصد ارسال — پیش‌فرض نویسنده (سند ۵-۷)؛ در UI قابل مشاهده.</summary>
    public string RecipientAddress { get; set; }

    public SmtpTransport(SmtpSettings settings, ISecretProvider secrets)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        RecipientAddress = settings.DefaultToAddress;
    }

    /// <inheritdoc/>
    public string Name => "SMTP با TLS";

    /// <inheritdoc/>
    public bool IsConfigured =>
        _settings.IsConfigured && !string.IsNullOrEmpty(_secrets.GetSmtpPassword());

    /// <inheritdoc/>
    public async Task<TransportOutcome> SendAsync(QueuedReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        var password = _secrets.GetSmtpPassword();
        if (string.IsNullOrEmpty(password))
            return new TransportOutcome(false,
                "ارسال نشده — پیکربندی لازم است: رمز سرویس ایمیل در مخزن امن ثبت نشده است.",
                "missing-secret");

        if (!_settings.IsConfigured)
            return new TransportOutcome(false,
                "ارسال نشده — پیکربندی لازم است: تنظیمات SMTP کامل نیست.",
                "not-configured");

        // موضوع و متن ایمیل — پاک‌سازی دومرحله‌ای؛ دادهٔ حساس هرگز به سرور نمی‌رود.
        var subjectPrefix = report.Kind == ReportKind.Error
            ? "[خطای برنامه]"
            : "[بازخورد]";
        var subject = $"{subjectPrefix} {report.Subject ?? report.Id.ToString()[..8]}";

        var bodyLines = new List<string>
        {
            $"نوع: {report.Kind}",
            $"شناسهٔ گزارش: {report.Id}",
            $"نسخهٔ برنامه: {report.AppVersion}",
            $"زمان ایجاد: {report.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}",
            $"ماژول: {report.Module ?? "—"}",
            "",
            "متن گزارش:",
            report.Body,
        };

        if (report.ContactEmail is { } contact)
            bodyLines.AddRange(["", $"ایمیل تماس (داوطلبانه): {contact}"]);

        if (report.RedactionNotes.Count > 0)
            bodyLines.AddRange(["", "پاک‌سازی‌های انجام‌شده:", .. report.RedactionNotes.Select(n => $"- {n}")]);

        var body = string.Join(Environment.NewLine, bodyLines);

        // ضمیمه فقط اگر کاربر جداگانه تأیید کرده باشد (سند ۵-۴).
        var attachment = report.UserConsented ? report.OptionalAttachmentPath : null;

        var result = await _client.SendAsync(
            _settings, password, RecipientAddress, subject, body, attachment, cancellationToken);

        return result.Success
            ? TransportOutcome.Ok(result.ProtocolLog)
            : new TransportOutcome(false, result.PersianError, result.ProtocolLog);
    }
}

using System.Text.Json.Serialization;

namespace Sahand.Reporting;

/// <summary>نوع گزارش کاربر.</summary>
public enum ReportKind
{
    /// <summary>خطای غیرمنتظرهٔ برنامه.</summary>
    Error,

    /// <summary>بازخورد/پیشنهاد کاربر.</summary>
    Feedback,
}

/// <summary>وضعیت چرخهٔ حیات گزارش — دقیق و بدون موفقیت کاذب (سند ۵-۳).</summary>
public enum ReportStatus
{
    /// <summary>در صف محلی؛ در انتظار اتصال/پیکربندی.</summary>
    Queued,

    /// <summary>ارسال در جریان است.</summary>
    Sending,

    /// <summary>ارسال با تأیید واقعی transport موفق شد.</summary>
    Sent,

    /// <summary>ارسال ناموفق — تعداد تلاش ثبت شده.</summary>
    Failed,

    /// <summary>کاربر ارسال را لغو کرد یا گزارش را حذف کرد.</summary>
    Cancelled,
}

/// <summary>
/// یک گزارش در صف محلی — کمینهٔ داده (سند ۵-۴) و بدون هیچ دادهٔ حساس پیش‌فرض.
/// </summary>
/// <remarks>
/// محتوای مجاز: نسخهٔ برنامه، شناسهٔ تصادفی گزارش، زمان محلی با منطقهٔ زمانی، ماژول،
/// متن پاک‌سازی‌شده. شناسهٔ دستگاه/کاربر، IP، credential، مسیرهای شخصی و محتوای پروژه
/// به‌طور پیش‌فرض وجود ندارند؛ یافته‌های redaction در <see cref="RedactionNotes"/> ثبت می‌شوند.
/// </remarks>
public sealed record QueuedReport
{
    /// <summary>شناسهٔ یکتا برای جلوگیری از ارسال تکراری (سند ۵-۳).</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>نوع گزارش.</summary>
    public ReportKind Kind { get; init; }

    /// <summary>عنوان کوتاه (اختیاری؛ برای بازخورد).</summary>
    public string? Subject { get; init; }

    /// <summary>متن اصلی گزارش — پس از پاک‌سازی.</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>ایمیل تماس اختیاری کاربر (دادهٔ شخصی داوطلبانه).</summary>
    public string? ContactEmail { get; init; }

    /// <summary>ماژول/فرایند درگیر (مثل «هندسه» یا «خروجی STL»).</summary>
    public string? Module { get; init; }

    /// <summary>نسخهٔ برنامه.</summary>
    public string AppVersion { get; init; } = "0.1.0";

    /// <summary>زمان ایجاد به وقت محلی با آفست منطقهٔ زمانی.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    /// <summary>وضعیت فعلی.</summary>
    public ReportStatus Status { get; set; } = ReportStatus.Queued;

    /// <summary>تعداد تلاش‌های ارسال.</summary>
    public int AttemptCount { get; set; }

    /// <summary>زمان آخرین تلاش.</summary>
    public DateTimeOffset? LastAttemptAt { get; set; }

    /// <summary>خطای آخرین transport (پاک‌سازی‌شده).</summary>
    public string? LastError { get; set; }

    /// <summary>یادداشت‌های پاک‌سازی (چه چیزی حذف/محرمانه شد).</summary>
    public IReadOnlyList<string> RedactionNotes { get; init; } = [];

    /// <summary>پیوست اختیاری — فقط با تأیید جداگانهٔ کاربر (سند ۵-۴).</summary>
    public string? OptionalAttachmentPath { get; init; }

    /// <summary>کاربر ارسال خودکار این گزارش را تأیید کرده است.</summary>
    public bool UserConsented { get; init; }
}

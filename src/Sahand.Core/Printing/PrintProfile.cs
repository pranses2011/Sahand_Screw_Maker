namespace Sahand.Core.Printing;

/// <summary>فرایند چاپ سه‌بعدی — پارامترهای مختص هر فرایند فقط با پشتیبانی واقعی فعال‌اند.</summary>
public enum PrintProcess
{
    /// <summary>رشته‌مذاب FDM/FFF — پارامتر نازل و ارتفاع لایه.</summary>
    Fdm,

    /// <summary>رزینی SLA/MSLA — پارامتر دقت XY.</summary>
    Sla,

    /// <summary>پودری SLS.</summary>
    Sls,

    /// <summary>پروفایل سفارشی کاربر.</summary>
    Custom,
}

/// <summary>نوع اتصال/فیت هدف برای قطعهٔ چاپی.</summary>
public enum PrintFitMode
{
    /// <summary>لغزش‌دار — لقی مثبت برای چرخش راحت (مقدار پیشنهادی، نه تضمین).</summary>
    Loose,

    /// <summary>نزدیک — لقی کم برای اتصال نسبتاً محکم.</summary>
    Snug,

    /// <summary>فشاری — لقی منفی برای درج با فشار (مستعد جنس/دقت دستگاه).</summary>
    Press,

    /// <summary>لقی عددی صریح کاربر.</summary>
    Custom,
}

/// <summary>
/// پروفایل چاپ — مجموعهٔ تنظیمات بهینه‌سازی برای ساخت «نسخهٔ چاپی» (سند ۴-۱).
/// </summary>
/// <remarks>
/// همهٔ جبران‌ها مستقل و قابل خاموش‌کردن‌اند؛ هیچ مقداری تضمین فیت یا استحکام نیست و در UI
/// با برچسب «پیشنهاد» ارائه می‌شود. علامت قرارداد: جبران مثبت قطرِ رزوهٔ خارجی را «کم» می‌کند
/// و حفره/رزوهٔ داخلی را «زیاد» — جهت هر فیلد در جدول diff دوباره نمایش داده می‌شود.
/// </remarks>
public sealed record PrintProfile
{
    /// <summary>نام پروفایل برای ذخیره/بازیابی.</summary>
    public string Name { get; init; } = "پروفایل چاپ پیش‌فرض";

    /// <summary>فرایند چاپ.</summary>
    public PrintProcess Process { get; init; } = PrintProcess.Fdm;

    /// <summary>قطر نازل برحسب میلی‌متر (FDM) — صفر یعنی «تعیین‌نشده».</summary>
    public double NozzleDiameterMm { get; init; }

    /// <summary>ارتفاع لایه برحسب میلی‌متر — صفر یعنی «تعیین‌نشده».</summary>
    public double LayerHeightMm { get; init; }

    /// <summary>دقت XY برحسب میلی‌متر (SLA) — صفر یعنی «تعیین‌نشده».</summary>
    public double XyResolutionMm { get; init; }

    /// <summary>درصد انقباض ماده (مثلاً ۰٫۵ برای ABS) — تقریب؛ از روی فایل حدس زده نمی‌شود.</summary>
    public double ShrinkagePercent { get; init; }

    /// <summary>حالت فیت هدف.</summary>
    public PrintFitMode FitMode { get; init; } = PrintFitMode.Snug;

    /// <summary>لقی عددی کاربر برحسب میلی‌متر — فقط برای <see cref="PrintFitMode.Custom"/>.</summary>
    public double CustomClearanceMm { get; init; }

    /// <summary>جبران شعاعی قطر رزوهٔ خارجی برحسب میلی‌متر — مثبت یعنی ریزتر شدن.</summary>
    public double ExternalThreadRadialCompensationMm { get; init; }

    /// <summary>جبران شعاعی قطر رزوهٔ داخلی برحسب میلی‌متر — مثبت یعنی درشت‌تر شدن حفره.</summary>
    public double InternalThreadRadialCompensationMm { get; init; }

    /// <summary>جبران قطر حفره‌های غیررزوه برحسب میلی‌متر — مثبت یعنی بزرگ‌تر شدن حفره.</summary>
    public double HoleDiameterCompensationMm { get; init; }

    /// <summary>پخ شروع ورود رزوه برحسب میلی‌متر — کمک به شروع درگیرشدن در چاپ.</summary>
    public double ThreadLeadInChamferMm { get; init; }

    /// <summary>حداقل ضخامت دیوارهٔ مجاز برحسب میلی‌متر؛ زیر آن هشدار دیوارهٔ نازک صادر می‌شود.</summary>
    public double MinWallThicknessMm { get; init; } = 1.0;

    /// <summary>فعال‌بودن جبران انقباض ماده.</summary>
    public bool ApplyShrinkage { get; init; }

    /// <summary>
    /// لقی مؤثر برحسب میلی‌متر بر اساس حالت فیت — صرفاً «پیشنهاد»، بدون تضمین.
    /// </summary>
    public double EffectiveClearanceMm => FitMode switch
    {
        PrintFitMode.Loose => 0.35,
        PrintFitMode.Snug => 0.15,
        PrintFitMode.Press => -0.10,
        PrintFitMode.Custom => CustomClearanceMm,
        _ => 0.0,
    };
}

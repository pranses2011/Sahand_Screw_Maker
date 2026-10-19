namespace Sahand.Core.Threads;

/// <summary>
/// تعریف کامل یک رزوه — واحد اطلاعات مرکزی مشترک میان پیچ، مهره، میله و خروجی‌گیرها.
/// </summary>
/// <remarks>
/// واحد تمام طول‌ها «میلی‌متر» است؛ برای خانوادهٔ اینچی، TPI ورودی کاربر به گام میلی‌متری
/// تبدیل و مقدار اصلی هم برای نمایش نگه داشته می‌شود تا رفت‌وبرگشت بدون خطای تجمعی ممکن باشد (GEO-08).
/// «گام» (pitch) فاصلهٔ محوری دندانه‌های مجاور و «پیشروی» (lead) جابه‌جایی هر دور کامل است:
/// <c>lead = pitch × starts</c>. این دو هرگز یکی فرض نمی‌شوند (سند ۷-۵).
/// </remarks>
public sealed record ThreadSpec
{
    /// <summary>خانوادهٔ رزوه — پروفیل و نظام داده را تعیین می‌کند.</summary>
    public ThreadFamily Family { get; init; } = ThreadFamily.IsoMetric;

    /// <summary>نوع قرارگیری: خارجی (پیچ) یا داخلی (مهره).</summary>
    public ThreadKind Kind { get; init; } = ThreadKind.External;

    /// <summary>قطر اسمی/مرجع برحسب میلی‌متر.</summary>
    /// <remarks>
    /// برای NPT/BSP این «قطر مرجع نامی» است نه قطر خارجی واقعی؛ قرارداد مرجع در سند ۱-۵ الزامی است.
    /// </remarks>
    public double NominalDiameterMm { get; init; }

    /// <summary>گام (فاصلهٔ محوری دو دندانهٔ مجاور) برحسب میلی‌متر — همیشه مثبت.</summary>
    public double PitchMm { get; init; }

    /// <summary>تعداد شروع‌های مستقل رزوه؛ حداقل ۱. پیشروی = گام × شروع‌ها.</summary>
    public int Starts { get; init; } = 1;

    /// <summary>جهت پیچش: راست‌گرد (رایج) یا چپ‌گرد.</summary>
    public ThreadHandedness Handedness { get; init; } = ThreadHandedness.Right;

    /// <summary>طول ناحیهٔ رزوه‌شده برحسب میلی‌متر.</summary>
    public double ThreadLengthMm { get; init; }

    /// <summary>حالت مدل‌سازی: رزوهٔ واقعی مارپیچ یا ساده/نمایشی.</summary>
    public ThreadModelingMode ModelingMode { get; init; } = ThreadModelingMode.RealHelical;

    /// <summary>زاویهٔ نیم‌پروفیل برحسب درجه — فقط برای خانواده‌های زاویه‌دار معنا دارد.</summary>
    /// <remarks>
    /// متریک/یونیفاید ۳۰، ویتوورث ۲۷٫۵، ذوزنقه‌ای ۳۰ و ACME ۱۴٫۵ درجه.
    /// برای Custom مقدار کاربر ملاک است. صفر = بی‌تفاوت/نامعتبر برای خانواده.
    /// </remarks>
    public double HalfAngleDegrees { get; init; }

    /// <summary>کلاس/درجهٔ سازگاری (مثل «6g/6H» یا «2A/2B») — نمایشی و مقایسه‌ای؛ صرفاً برچسب.</summary>
    /// <remarks>
    /// مدل تلرانس واقعی پیاده نشده؛ این فیلد فقط در هشدار سازگاری جفت (سند ۳-۶) مقایسه متنی می‌شود.
    /// </remarks>
    public string? FitClass { get; init; }

    /// <summary>شناسهٔ استاندارد/قالب منبع (مثل «ISO-261-M6») یا null برای کاملاً سفارشی.</summary>
    public string? StandardId { get; init; }

    /// <summary>یادداشت منبع و ویرایش داده — الزام ردیابی سند ۳-۶.</summary>
    public string? DataSourceNote { get; init; }

    /// <summary>پروفیل سفارشی کاربر — فقط وقتی <see cref="Family"/> برابر Custom معنا دارد.</summary>
    /// <remarks>نقاط در صفحهٔ (شعاع، محور) با واحد میلی‌متر؛ حلقهٔ بستهٔ معتبر لازم است.</remarks>
    public IReadOnlyList<ThreadProfilePoint>? CustomProfile { get; init; }

    /// <summary>آنگولارِ مخروط (taper) برحسب درجه نسبت به محور؛ صفر = موازی.</summary>
    /// <remarks>برای NPT حدود ۱٫۷۸۹۹ درجه (۱:۱۶) است؛ مخروط در جهت قطرِ در حال افزایش اعمال می‌شود.</remarks>
    public double TaperDegrees { get; init; }

    /// <summary>پیشروی محوری در هر دور کامل برحسب میلی‌متر (گام × تعداد شروع‌ها).</summary>
    public double LeadMm => PitchMm * Starts;

    /// <summary>نمایش خوانای مشخصات برای خلاصه‌های UI و گزارش (بدون ادعای انطباق رسمی).</summary>
    public override string ToString()
    {
        var hand = Handedness == ThreadHandedness.Left ? "LH" : "RH";
        var pitch = Family is ThreadFamily.Unified or ThreadFamily.Whitworth
            ? $"TPI={25.4 / PitchMm:0.##}"
            : $"P={PitchMm:0.###}mm";
        var custom = Family == ThreadFamily.Custom ? " (سفارشی)" : string.Empty;
        return $"{Family}{custom} {NominalDiameterMm:0.###}mm {pitch} {hand}×{Starts}";
    }
}

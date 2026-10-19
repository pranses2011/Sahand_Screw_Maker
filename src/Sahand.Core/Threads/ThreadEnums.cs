namespace Sahand.Core.Threads;

/// <summary>
/// خانوادهٔ رزوه؛ تعیین‌کنندهٔ پروفیل مقطع و نظام دادهٔ کاتالوگ.
/// </summary>
/// <remarks>
/// هر خانواده دادهٔ مجاز و محدودهٔ خود را دارد؛ پشتیبانی پروفیل هندسی به معنی
/// پشتیبانی «کاتالوگ استاندارد» نیست — وضعیت دادهٔ هر خانواده در ماتریس کاتالوگ ثبت می‌شود.
/// </remarks>
public enum ThreadFamily
{
    /// <summary>متریک ISO با زاویهٔ اسمی ۶۰ درجه (پروفیل مثلثی).</summary>
    IsoMetric,

    /// <summary>یونیفاید UNC/UNF/UNEF با زاویهٔ اسمی ۶۰ درجه (اینچی، TPI).</summary>
    Unified,

    /// <summary>ویتوورث BSW/BSF با زاویهٔ اسمی ۵۵ درجه و گردی تاج/ریشه.</summary>
    Whitworth,

    /// <summary>لوله‌ای مخروطی NPT/NPTF — مخروطی‌بودن الزامی است.</summary>
    Npt,

    /// <summary>لوله‌ای BSP موازی (BSPP) یا مخروطی (BSPT) با پروفیل ۵۵ درجه.</summary>
    Bsp,

    /// <summary>ذوزنقه‌ای ISO با زاویهٔ نیم‌پروفیل ۳۰ درجه (خانوادهٔ ترابزوئیدال).</summary>
    IsoTrapezoidal,

    /// <summary>ACME با زاویهٔ نیم‌پروفیل ۱۴٫۵ درجه (۲۹ درجهٔ کل).</summary>
    Acme,

    /// <summary>مربعی — پروفیل مربع شعله‌دار.</summary>
    Square,

    /// <summary>پروفیل کاملاً سفارشی کاربر با نقاط صریح.</summary>
    Custom,
}

/// <summary>
/// جهت پیچش رزوه (راست‌گرد یا چپ‌گرد).
/// </summary>
/// <remarks>
/// جهت در سازگاری جفت (سند ۳-۶) مقایسه می‌شود؛ پیچ چپ‌گرد فقط با مهرهٔ چپ‌گرد جفت می‌شود.
/// </remarks>
public enum ThreadHandedness
{
    /// <summary>راست‌گرد (رایج) — با چرخش ساعت‌گرد در پیش می‌رود.</summary>
    Right,

    /// <summary>چپ‌گرد — جهت جاروب معکوس است.</summary>
    Left,
}

/// <summary>
/// نوع رزوه از نظر قرارگیری روی قطعه.
/// </summary>
public enum ThreadKind
{
    /// <summary>رزوهٔ خارجی (روی پیچ/میل — دندانه به بیرون).</summary>
    External,

    /// <summary>رزوهٔ داخلی (درون مهره — دندانه به داخل حفره).</summary>
    Internal,
}

/// <summary>
/// حالت مدل‌سازی رزوه در هندسه (سند ۳-۵).
/// </summary>
/// <remarks>
/// تمایز صادقانه میان «هندسهٔ واقعی» و «نمایشی» الزامی است؛ خروجی حالت نمایشی
/// باید هشدار روشن داشته باشد که رزوهٔ واقعی در فایل نیست.
/// </remarks>
public enum ThreadModelingMode
{
    /// <summary>رزوهٔ مارپیچِ واقعاً مدل‌شده در مش — سنگین‌تر ولی صادقانه.</summary>
    RealHelical,

    /// <summary>بدون هندسهٔ رزوه — بدنهٔ صاف با برچسب صریح در UI/خروجی.</summary>
    Simplified,
}

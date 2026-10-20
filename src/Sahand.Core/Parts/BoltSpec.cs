using Sahand.Core.Threads;

namespace Sahand.Core.Parts;

/// <summary>
/// مشخصات پارامتریک پیچ/بولت — تمام ابعاد برحسب میلی‌متر (واحد پایه).
/// </summary>
/// <remarks>
/// قرارداد محور و مبدأ (سند ۷-۵): محور قطعه Z است؛ z=۰ روی سطح اتکای سر
/// و Z+ به سمت نوک ساق. «زیر سر» یعنی فاصلهٔ مؤثر درگیر، «طول کلی» شامل ارتفاع سر است.
/// مقادیر اختیاری null معنای «تعریف‌نشده» دارند؛ صفرِ بی‌معنا جایگزین نمی‌شود (سند ۳-۲).
/// </remarks>
public sealed record BoltSpec
{
    /// <summary>نام قطعه برای نمایش/فایل پروژه.</summary>
    public string Name { get; init; } = "پیچ";

    /// <summary>توضیح اختیاری کاربر.</summary>
    public string? Description { get; init; }

    /// <summary>سبک سر.</summary>
    public HeadStyle Head { get; init; } = HeadStyle.Hex;

    /// <summary>نوع محرک روی سر.</summary>
    public DriveStyle Drive { get; init; } = DriveStyle.None;

    /// <summary>شکل نوک.</summary>
    public PointStyle Point { get; init; } = PointStyle.Flat;

    /// <summary>مشخصات رزوهٔ ساق (خارجی).</summary>
    public ThreadSpec Thread { get; init; } = new();

    /// <summary>طول ساق زیر سر برحسب میلی‌متر — پارامتر اصلی طول.</summary>
    public double LengthUnderHeadMm { get; init; }

    /// <summary>ارتفاع سر برحسب میلی‌متر (برای خزینه: عمق مؤثر مخروط).</summary>
    public double HeadHeightMm { get; init; }

    /// <summary>عرض آچارخور (فاصلهٔ بین دو تخت مقابل) برای سر شش‌گوش برحسب میلی‌متر.</summary>
    /// <remarks>فقط برای <see cref="HeadStyle.Hex"/> معنا دارد؛ مقدار null یعنی نوع سر آن را نمی‌خواهد.</remarks>
    public double? HeadAcrossFlatsMm { get; init; }

    /// <summary>قطر سر استوانه‌ای/نیم‌گرد برحسب میلی‌متر — برای آلن و پان.</summary>
    public double? HeadDiameterMm { get; init; }

    /// <summary>زاویهٔ سر مخروطی خزینه برحسب درجه (معمولاً ۹۰).</summary>
    public double CountersunkAngleDegrees { get; init; } = 90.0;

    /// <summary>قطر حفرهٔ آلن برحسب میلی‌متر — فقط برای <see cref="DriveStyle.HexSocket"/>.</summary>
    public double? HexSocketAcrossFlatsMm { get; init; }

    /// <summary>عمق حفرهٔ آلن برحسب میلی‌متر.</summary>
    public double? HexSocketDepthMm { get; init; }

    /// <summary>قطر اسمی حفرهٔ تورکس (روی پَرها، E) برحسب میلی‌متر — برای <see cref="DriveStyle.Torx"/>/<see cref="DriveStyle.SecurityTorx"/> و دیگر محرک‌های گلبرگی (پنتالوب/تری‌وینگ/تورک‌ست — گسترش ۰٫۵٫۰).</summary>
    public double? TorxAcrossLobesMm { get; init; }

    /// <summary>عمق حفرهٔ محرک گلبرگی (تورکس/پنتالوب/تری‌وینگ/تورک‌ست) برحسب میلی‌متر.</summary>
    public double? TorxDepthMm { get; init; }

    /// <summary>قطر پین مرکزی محرک امنیتی برحسب میلی‌متر — فقط برای <see cref="DriveStyle.PinHex"/> و <see cref="DriveStyle.SecurityTorx"/> (گسترش ۰٫۴٫۰).</summary>
    /// <remarks>پین از کف حفره بیرون می‌زند و آچار معمولی را قفل می‌کند؛ ابزار مخصوصِ گوددار لازم است.</remarks>
    public double? SecurityPinDiameterMm { get; init; }

    /// <summary>قطر هر سوراخ محرک اسپنر/«چشم مار» برحسب میلی‌متر — فقط برای <see cref="DriveStyle.Spanner"/> (گسترش ۰٫۵٫۰).</summary>
    /// <remarks>دو سوراخ گرد جدار‌دار؛ فاصلهٔ مراکز به‌طور خودکار ~۱٫۹ برابر قطر سوراخ انتخاب می‌شود.</remarks>
    public double? SpannerHoleDiameterMm { get; init; }

    /// <summary>عمق سوراخ‌های محرک اسپنر برحسب میلی‌متر (گسترش ۰٫۵٫۰).</summary>
    public double? SpannerHoleDepthMm { get; init; }

    /// <summary>پهنای شیار تخت/صلیبی و پهنای پرهٔ محرک یک‌طرفه برحسب میلی‌متر.</summary>
    public double? SlotWidthMm { get; init; }

    /// <summary>عمق شیار/پرهٔ محرک یک‌طرفه برحسب میلی‌متر.</summary>
    public double? SlotDepthMm { get; init; }

    /// <summary>طول ناحیهٔ رزوه از نوک به سمت سر؛ null یعنی تمام‌رزوه.</summary>
    /// <remarks>اگر مقدار داشته باشد نباید از طول ساق بیشتر شود (اعتبارسنجی GEO-07).</remarks>
    public double? ThreadRunoutFromTipMm { get; init; }

    /// <summary>پخ انتهای ساق برحسب میلی‌متر (کاهش قطر در نوک).</summary>
    public double TipChamferMm { get; init; }

    /// <summary>پخ لبهٔ بالای سر شش‌گوش برحسب میلی‌متر (تقریب مخروطی فرز فوقانی).</summary>
    public double HeadTopChamferMm { get; init; }

    /// <summary>قطر دیسک فلنج برحسب میلی‌متر — فقط برای <see cref="HeadStyle.Flange"/> (گسترش ۰٫۴٫۰).</summary>
    /// <remarks>باید از عرض آچارخور سر بزرگ‌تر باشد؛ سطح اتکای وسیع‌تر برای مواد نرم.</remarks>
    public double? FlangeDiameterMm { get; init; }

    /// <summary>ضخامت دیسک فلنج برحسب میلی‌متر (پایهٔ مخروطی به شش‌گوش).</summary>
    public double? FlangeThicknessMm { get; init; }

    /// <summary>طول کلی قطعه شامل ارتفاع سر.</summary>
    public double TotalLengthMm => LengthUnderHeadMm + HeadHeightMm;
}

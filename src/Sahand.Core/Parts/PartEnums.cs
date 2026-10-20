namespace Sahand.Core.Parts;

/// <summary>نوع کلی قطعه — تعیین‌کنندهٔ فرم پارامترها و سازندهٔ هندسه.</summary>
public enum PartKind
{
    /// <summary>پیچ/بولت با سر و ساق رزوه‌دار.</summary>
    Bolt,

    /// <summary>مهره با رزوهٔ داخلی.</summary>
    Nut,

    /// <summary>واشر (بدون رزوه).</summary>
    Washer,

    /// <summary>استادبولت/میلِ تمام‌رزوه یا بخش‌رزوه.</summary>
    ThreadedRod,

    /// <summary>مجموعهٔ جفت (مثلاً پیچ + مهره + واشر) به‌صورت اجزای جدا.</summary>
    Assembly,
}

/// <summary>
/// سبک سر پیچ — سبک‌های پیاده‌سازی‌شده در این نسخه وضعیت «تکمیل» دارند؛
/// بقیه در ماتریس قابلیت با وضعیت «برنامهٔ توسعه» جدا می‌شوند.
/// </summary>
public enum HeadStyle
{
    /// <summary>سر شش‌گوش (Hex) — رایج‌ترین.</summary>
    Hex,

    /// <summary>سر استوانه‌ای با حفرهٔ آلن (Socket Head Cap).</summary>
    SocketCap,

    /// <summary>سر مخروطی خزینه (Flat Countersunk).</summary>
    FlatCountersunk,

    /// <summary>سر نیم‌گرد (Pan).</summary>
    Pan,

    /// <summary>سر دکمه‌ای (Button) — گنبد کم‌انحنا با لبهٔ گرد؛ با حفرهٔ آلن/تورکس روی پلتفرم قله (گسترش ۰٫۴٫۰).</summary>
    Button,

    /// <summary>سر فلنجی (Flange) — شش‌گوش با دیسک فلنج در پایه؛ توزیع فشار روی سطح نرم (گسترش ۰٫۴٫۰).</summary>
    Flange,

    /// <summary>سر پنیری (Cheese) — استوانهٔ کم‌انحنا با رویهٔ تخت و لبهٔ پخ‌دار؛ همهٔ محرک‌ها (گسترش ۰٫۵٫۰).</summary>
    Cheese,

    /// <summary>سر پهن/تخت (Truss) — گنبد پهن و کم‌ارتفاع برای سطح اتکای وسیع؛ محرک حفره‌ای روی پلتفرم قله (گسترش ۰٫۵٫۰).</summary>
    Truss,

    /// <summary>سر فلیستر (Fillister) — استوانه با گنبد بلند؛ محرک حفره‌ای روی پلتفرم قله (گسترش ۰٫۵٫۰).</summary>
    Fillister,

    /// <summary>سر وافری (Wafer) — مخروط خیلی کم‌عمق با رویهٔ تخت کوچک؛ شیار/صلیبی (گسترش ۰٫۵٫۰).</summary>
    Wafer,

    /// <summary>سر خزینهٔ گنبدی (Raised Countersunk / Oval) — مخروط خزینه با کلاهک گنبدی؛ بدون محرک در این نسخه (گسترش ۰٫۵٫۰).</summary>
    RaisedCountersunk,

    /// <summary>سر دوازده‌گوش (Twelve Point) — منشور ۱۲‌وجه با فلنج؛ آچار هوافضا، گشتاور بالا (گسترش ۰٫۵٫۰).</summary>
    TwelvePoint,
}

/// <summary>نوع محرک/جای ابزار روی سر.</summary>
public enum DriveStyle
{
    /// <summary>بدون محرک (سر صاف).</summary>
    None,

    /// <summary>حفرهٔ شش‌گوش داخلی (آلن).</summary>
    HexSocket,

    /// <summary>حفرهٔ شش‌پر تورکس (ISO 10664) — گسترش ۰٫۳٫۰.</summary>
    Torx,

    /// <summary>آلن امنیتی با پین مرکزی (Tamper-Resistant Hex) — آچار معمولی قفل می‌شود (گسترش ۰٫۴٫۰).</summary>
    PinHex,

    /// <summary>تورکس امنیتی با پین مرکزی (Security Torx / Tamper-Resistant Torx) — گسترش ۰٫۴٫۰.</summary>
    SecurityTorx,

    /// <summary>شیار تخت (Slot).</summary>
    Slot,

    /// <summary>شیار صلیبی (Phillips) — نام تجاری؛ نمایش با برچسب «بر پایهٔ فرم صلیبی».</summary>
    Phillips,

    /// <summary>اسپنر امنیتی / «چشم مار» (Spanner / Snake-Eyes) — دو سوراخ گرد جدار‌دار؛ آچار معمولی جا نمی‌شود (گسترش ۰٫۵٫۰).</summary>
    Spanner,

    /// <summary>پنتالوب امنیتی (Pentalobe / P-lobe) — پنج گلبرگ؛ پرکاربرد لوازم الکترونیکی (گسترش ۰٫۵٫۰).</summary>
    Pentalobe,

    /// <summary>تری‌وینگ امنیتی (Tri-Wing) — سه بال ۱۲۰°؛ پرکاربرد صنایع لوازم خانگی/تبلت (گسترش ۰٫۵٫۰).</summary>
    TriWing,

    /// <summary>تورک‌ست امنیتی (Torq-Set) — صلیب چهارپرِ آفست؛ هوافضا/نظامی (گسترش ۰٫۵٫۰).</summary>
    TorqSet,

    /// <summary>یک‌طرفه (One-Way) — پره‌های مورب فقط برای بستن؛ باز کردن با آچار معمولی ممکن نیست (گسترش ۰٫۵٫۰).</summary>
    OneWay,
}

/// <summary>شکل نوک ساق پیچ.</summary>
public enum PointStyle
{
    /// <summary>نوک تخت با پخ.</summary>
    Flat,

    /// <summary>نوک مخروطی (پیچ ماشینی/خودکار).</summary>
    Cone,

    /// <summary>نوک گِرد (پان/ماشینی).</summary>
    Round,
}

/// <summary>سبک مهره.</summary>
public enum NutStyle
{
    /// <summary>مهرهٔ شش‌گوش استاندارد.</summary>
    Hex,

    /// <summary>مهرهٔ کم‌ارتفاع (Jam).</summary>
    HexJam,
}

/// <summary>سبک واشر.</summary>
public enum WasherStyle
{
    /// <summary>واشر تخت ساده — تنها سبک پیاده‌سازی‌شده در این نسخه.</summary>
    FlatPlain,
}

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
}

/// <summary>نوع محرک/جای ابزار روی سر.</summary>
public enum DriveStyle
{
    /// <summary>بدون محرک (سر صاف).</summary>
    None,

    /// <summary>حفرهٔ شش‌گوش داخلی (آلن).</summary>
    HexSocket,

    /// <summary>شیار تخت (Slot).</summary>
    Slot,

    /// <summary>شیار صلیبی (Phillips) — نام تجاری؛ نمایش با برچسب «بر پایهٔ فرم صلیبی».</summary>
    Phillips,
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

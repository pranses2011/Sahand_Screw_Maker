using Sahand.Core.Threads;

namespace Sahand.Core.Parts;

/// <summary>
/// مجموعهٔ جفت (پیچ + مهره + واشر اختیاری) — اجزا جدا و قابل انتخاب ذخیره می‌شوند (سند ۳-۹).
/// </summary>
/// <remarks>
/// Boolean union اجباری وجود ندارد؛ هر جزء مش و پارامتر مستقل خود را دارد.
/// سازگاری رزوهٔ جفت در ساخت مجموعه بررسی و نتیجهٔ آن در هشدارهای پروژه ثبت می‌شود.
/// </remarks>
public sealed record AssemblySpec
{
    /// <summary>نام مجموعه.</summary>
    public string Name { get; init; } = "مجموعهٔ پیچ و مهره";

    /// <summary>توضیح اختیاری کاربر.</summary>
    public string? Description { get; init; }

    /// <summary>پیچ مجموعه.</summary>
    public BoltSpec Bolt { get; init; } = new();

    /// <summary>مهرهٔ جفت؛ null یعنی مجموعهٔ بدون مهره.</summary>
    public NutSpec? Nut { get; init; }

    /// <summary>واشر اختیاری زیر مهره.</summary>
    public WasherSpec? Washer { get; init; }

    /// <summary>
    /// فاصلهٔ نمایشی میان صفحهٔ اتکای مهره و انتهای رزوهٔ پیچ برحسب میلی‌متر — فقط برای پیش‌نمایش مجموعه.
    /// </summary>
    /// <remarks>این پارامتر «لقی» نیست؛ تحلیل درگیری/تماس مهندسی ارائه نمی‌شود (سند ۳-۹).</remarks>
    public double DisplayGapMm { get; init; }

    /// <summary>نتیجهٔ سازگاری رزوهٔ پیچ و مهره در همین مجموعه.</summary>
    public ThreadCompatibilityResult CheckThreadCompatibility()
    {
        if (Nut is null) return ThreadCompatibilityResult.Ok();
        return ThreadCompatibility.Check(Bolt.Thread, Nut.Thread);
    }
}

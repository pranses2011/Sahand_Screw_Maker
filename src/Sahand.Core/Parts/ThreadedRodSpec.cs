using Sahand.Core.Threads;

namespace Sahand.Core.Parts;

/// <summary>
/// مشخصات استادبولت/میل رزوه‌دار — تمام ابعاد برحسب میلی‌متر.
/// </summary>
/// <remarks>
/// قرارداد محور: z=۰ در یک انتها؛ میله روی محور Z. «تمام‌رزوه» یعنی ThreadRunoutMm صریحاً null است؛
/// مقدار مثبت یعنی بخش صاف میانی به همان اندازه از هر دو سر داخل می‌ماند.
/// </remarks>
public sealed record ThreadedRodSpec
{
    /// <summary>نام قطعه برای نمایش/فایل پروژه.</summary>
    public string Name { get; init; } = "میل رزوه‌دار";

    /// <summary>توضیح اختیاری کاربر.</summary>
    public string? Description { get; init; }

    /// <summary>مشخصات رزوهٔ میله (خارجی).</summary>
    public ThreadSpec Thread { get; init; } = new();

    /// <summary>طول کل میله برحسب میلی‌متر.</summary>
    public double TotalLengthMm { get; init; }

    /// <summary>
    /// طول ناحیهٔ صاف میانی برحسب میلی‌متر؛ null یا صفر یعنی تمام‌رزوه.
    /// </summary>
    /// <remarks>رزوهٔ دو سر هم‌اندازه است؛ میلهٔ با رزوهٔ نامتقارن در نسخهٔ پایه پشتیبانی نمی‌شود.</remarks>
    public double? SmoothCenterLengthMm { get; init; }

    /// <summary>پخ دو انتها برحسب میلی‌متر.</summary>
    public double EndChamferMm { get; init; }
}

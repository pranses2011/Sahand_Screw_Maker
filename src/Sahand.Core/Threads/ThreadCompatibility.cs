namespace Sahand.Core.Threads;

/// <summary>
/// نتیجهٔ بررسی سازگاری دو رزوهٔ جفت (معمولاً خارجی + داخلی).
/// </summary>
/// <param name="IsCompatible">اگر false باشد، فهرست <paramref name="Issues"/> دلیل‌ها را دارد.</param>
/// <param name="Issues">فهرست مسائل به فارسی؛ هر مورد قابل اقدام و قابل نمایش در UI است.</param>
public sealed record ThreadCompatibilityResult(bool IsCompatible, IReadOnlyList<string> Issues)
{
    /// <summary>نتیجهٔ موفق بدون مسئله.</summary>
    public static ThreadCompatibilityResult Ok() => new(true, Array.Empty<string>());
}

/// <summary>
/// سازگارسنج رزوهٔ جفت — مقایسهٔ قطر، گام/TPI، جهت، نوع پروفیل، مخروط و کلاس (سند ۳-۶).
/// </summary>
/// <remarks>
/// هماهنگ‌بودن فقط قطر اسمی کافی نیست (الزام صریح سند). مقایسه‌های عددی با تلرانس
/// ۱e-۶ میلی‌متر انجام می‌شود تا خطای ممیز شناور مهندسی اثرگذار نباشد.
/// کلاس سازگاری فقط به‌صورت هشدار غیرمسدودکننده گزارش می‌شود چون مدل تلرانس واقعی پیاده نشده.
/// </remarks>
public static class ThreadCompatibility
{
    private const double EpsilonMm = 1e-6;

    /// <summary>
    /// سازگاری رزوهٔ خارجی و داخلی را بررسی می‌کند.
    /// </summary>
    /// <param name="external">رزوهٔ خارجی (پیچ/میل).</param>
    /// <param name="internalThread">رزوهٔ داخلی (مهره).</param>
    /// <returns>نتیجه با فهرست مسائل فارسی.</returns>
    public static ThreadCompatibilityResult Check(ThreadSpec external, ThreadSpec internalThread)
    {
        ArgumentNullException.ThrowIfNull(external);
        ArgumentNullException.ThrowIfNull(internalThread);

        if (external.Kind != ThreadKind.External)
            return new ThreadCompatibilityResult(false,
                ["قطعهٔ اول باید رزوهٔ خارجی باشد تا مقایسهٔ جفت معنا داشته باشد."]);

        if (internalThread.Kind != ThreadKind.Internal)
            return new ThreadCompatibilityResult(false,
                ["قطعهٔ دوم باید رزوهٔ داخلی باشد تا مقایسهٔ جفت معنا داشته باشد."]);

        var issues = new List<string>();

        if (external.Family != internalThread.Family)
            issues.Add(
                $"خانوادهٔ رزوه متفاوت است: {external.Family} در برابر {internalThread.Family}. خانواده‌ها باید یکسان باشند.");

        if (Math.Abs(external.NominalDiameterMm - internalThread.NominalDiameterMm) > EpsilonMm)
            issues.Add(
                $"قطر اسمی متفاوت است: {external.NominalDiameterMm:0.###}mm در برابر {internalThread.NominalDiameterMm:0.###}mm.");

        if (Math.Abs(external.PitchMm - internalThread.PitchMm) > EpsilonMm)
            issues.Add(
                $"گام رزوه متفاوت است: {external.PitchMm:0.###}mm در برابر {internalThread.PitchMm:0.###}mm. رزوه‌های ناهم‌گام هرگز درگیر نمی‌شوند.");

        if (external.Starts != internalThread.Starts)
            issues.Add(
                $"تعداد شروع‌ها متفاوت است: {external.Starts} در برابر {internalThread.Starts}. تعداد شروع‌های جفت باید برابر باشد.");

        if (external.Handedness != internalThread.Handedness)
            issues.Add(
                $"جهت رزوه متفاوت است: {external.Handedness == ThreadHandedness.Right ? "راست‌گرد" : "چپ‌گرد"} در برابر " +
                $"{internalThread.Handedness == ThreadHandedness.Right ? "راست‌گرد" : "چپ‌گرد"}. پیچ چپ‌گرد فقط با مهرهٔ چپ‌گرد جفت می‌شود.");

        if (Math.Abs(external.TaperDegrees - internalThread.TaperDegrees) > 1e-4)
            issues.Add(
                $"مخروط‌بودن دو رزوه متفاوت است ({external.TaperDegrees:0.####}° در برابر {internalThread.TaperDegrees:0.####}°). رزوهٔ مخروطی و موازی جفت معتبر ندارند.");

        // هشدار غیرمسدودکنندهٔ کلاس سازگاری — مدل تلرانس واقعی پیاده نشده و این فقط مقایسهٔ برچسب است.
        if (!string.IsNullOrWhiteSpace(external.FitClass) && !string.IsNullOrWhiteSpace(internalThread.FitClass) &&
            !string.Equals(external.FitClass, internalThread.FitClass, StringComparison.OrdinalIgnoreCase))
            issues.Add(
                $"کلاس سازگاری متفاوت است ({external.FitClass} در برابر {internalThread.FitClass})؛ این یک هشدار است چون تلرانس هندسی واقعی مدل نمی‌شود.");

        return issues.Count == 0
            ? ThreadCompatibilityResult.Ok()
            : new ThreadCompatibilityResult(false, issues);
    }
}

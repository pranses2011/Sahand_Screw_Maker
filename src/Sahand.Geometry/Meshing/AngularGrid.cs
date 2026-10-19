namespace Sahand.Geometry.Meshing;

/// <summary>
/// شبکهٔ زاویه‌ای یکنواخت حول محور Z با پشتیبانی «زوایای ویژه» اجباری.
/// </summary>
/// <remarks>
/// چرا زوایای ویژه لازم است: ساختارهای مختلف قطعه (خروج شیار از سطح استوانه‌ای، گوشه‌های
/// شش‌گوش، مرز پروفیل رزوه) باید دقیقاً روی رأس‌های مشترک بیفتند وگرنه «رأس T» و شکست
/// مانیفولد رخ می‌دهد. این کلاس همهٔ ساختارها را روی یک شبکهٔ مشترک هم‌راستا می‌کند.
/// شبکهٔ خروجی همیشه شامل ۰ و ۲π (برای پیوند دورانی) و زوایای ویژه است و بین آن‌ها
/// نقاط یکنواخت می‌گذارد؛ کل بازه [۰، ۲π] پوشش داده می‌شود.
/// </remarks>
public sealed class AngularGrid
{
    private readonly double[] _angles;

    /// <summary>زوایای رأس‌ها برحسب رادیان — صعودی از ۰ تا ۲π (شامل هر دو سر).</summary>
    public IReadOnlyList<double> Angles => _angles;

    /// <summary>تعداد بازه‌های شبکه (یعنی رأس منهای یک).</summary>
    public int SegmentCount => _angles.Length - 1;

    private AngularGrid(double[] angles) => _angles = angles;

    /// <summary>
    /// شبکهٔ زاویه‌ای می‌سازد.
    /// </summary>
    /// <param name="targetCount">تعداد هدف تقسیم دور کامل (حداقل ۱۲).</param>
    /// <param name="specialAngles">زوایای اجباری برحسب رادیان (مثل گوشه‌های شش‌گوش)؛ هر مقدار مجاز است.</param>
    public static AngularGrid Create(int targetCount, IEnumerable<double>? specialAngles = null)
    {
        if (targetCount < 12) targetCount = 12; // زیر ۱۲ وجه، استوانهٔ معناداری ساخته نمی‌شود.
        if (targetCount > 720) targetCount = 720; // سقف کیفیت برای عملکرد تعاملی.

        var specials = new SortedSet<double>();
        if (specialAngles is not null)
        {
            foreach (var angle in specialAngles)
            {
                // نرمال‌سازی به [0, 2π)؛ زاویهٔ دقیقاً ۰ به ۲π مپ می‌شود تا سرِ شبکه باشد.
                var normalized = angle % (2.0 * Math.PI);
                if (normalized < 0) normalized += 2.0 * Math.PI;
                if (normalized < 1e-12) normalized = 2.0 * Math.PI;
                specials.Add(normalized);
            }
        }

        // نقاط یکنواخت پایه؛ سپس هر نقطهٔ ویژه به نزدیک‌ترین نقطهٔ پایه اسنپ می‌شود مگر آن‌که
        // فاصله معنادار داشته باشد (آن‌وقت خودش رأس مستقل می‌شود تا دقت ساختار حفظ شود).
        var baseAngles = new List<double>(targetCount + specials.Count);
        for (var i = 0; i < targetCount; i++)
            baseAngles.Add(2.0 * Math.PI * i / targetCount);

        var merged = new List<double>(baseAngles);
        foreach (var special in specials)
        {
            var nearestIndex = -1;
            var nearestDist = double.MaxValue;
            for (var i = 0; i < merged.Count; i++)
            {
                var dist = Math.Abs(merged[i] - special);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestIndex = i;
                }
            }

            // آستانهٔ اسنپ: اگر ویژه در فاصلهٔ یک‌پنجم گام پایه باشد، همان نقطهٔ پایه جابه‌جا می‌شود.
            var snapThreshold = Math.PI * 2.0 / targetCount / 5.0;
            if (nearestDist <= snapThreshold && nearestIndex >= 0)
                merged[nearestIndex] = special;
            else
                merged.Add(special);
        }

        merged.Sort();

        // حذف نقاط تکراری/به‌هم‌چسبیده (فاصلهٔ کمتر از 1e-9 رادیان).
        var final = new List<double>(merged.Count) { merged[0] };
        for (var i = 1; i < merged.Count; i++)
        {
            if (merged[i] - final[^1] > 1e-9)
                final.Add(merged[i]);
        }

        // تضمین سرِ دورانی: مقدار اول ۰ و آخری ۲π؛ اگر ۰ جابه‌جا شده، بازنویسی می‌کنیم.
        if (final[^1] < 2.0 * Math.PI - 1e-9)
            final.Add(2.0 * Math.PI);
        else
            final[^1] = 2.0 * Math.PI;

        if (final[0] > 1e-9)
            final.Insert(0, 0.0);
        else
            final[0] = 0.0;

        return new AngularGrid([.. final]);
    }

    /// <summary>رأس شبکه در اندیس داده‌شده — اندیس آخر برابر ۲π است.</summary>
    public double this[int index] => _angles[index];
}

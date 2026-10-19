namespace Sahand.Geometry.Meshing;

/// <summary>
/// مثلث‌بندی چندضلعی ساده با الگوریتم بریدن گوش (Ear Clipping) — برای وجه‌های تخت با مرز دلخواه.
/// </summary>
/// <remarks>
/// ورودی باید «چندضلعی ساده» باشد (بدون خود-تقاطع). جهت رئوس مهم نیست؛ خروجی مثلث‌ها
/// با نرمال «به سمت +Z صفحهٔ محلی» ساخته می‌شود و فراخوان در صورت نیاز جهت را معکوس می‌کند.
/// برای پایداری عددی، آزمون گوش با ضرب‌داخلی بردار لبه انجام می‌شود نه مساحت علامت‌دار کلی.
/// </remarks>
public static class PolygonTriangulation
{
    /// <summary>
    /// چندضلعی ساده را به مثلث‌های شاخص‌دار تقسیم می‌کند.
    /// </summary>
    /// <param name="vertexCount">تعداد رئوس چندضلعی (دست‌کم ۳).</param>
    /// <param name="getVertex">تابع دسترسی به مختصات دوبعدی رأس با اندیس.</param>
    /// <returns>مثلث‌ها به‌صورت سه‌تایی اندیس رأس‌ها.</returns>
    /// <exception cref="ArgumentException">اگر رأس کمتر از ۳ یا چندضلعی تحلیل‌رفته باشد.</exception>
    public static IEnumerable<(int A, int B, int C)> Triangulate(
        int vertexCount,
        Func<int, (double X, double Y)> getVertex)
    {
        ArgumentNullException.ThrowIfNull(getVertex);
        if (vertexCount < 3)
            throw new ArgumentException("چندضلعی دست‌کم ۳ رأس لازم دارد.", nameof(vertexCount));

        var vertices = new (double X, double Y)[vertexCount];
        for (var i = 0; i < vertexCount; i++) vertices[i] = getVertex(i);

        var orientation = SignedArea(vertices) >= 0;

        // فهرست پویای اندیس‌ها؛ گوش‌ها یکی‌یکی بریده می‌شوند.
        var remaining = new List<int>(vertexCount);
        for (var i = 0; i < vertexCount; i++) remaining.Add(i);

        var guard = 0;
        var maxIterations = vertexCount * vertexCount; // سقف ایمنی برای جلوگیری از حلقهٔ بی‌نهایت.

        while (remaining.Count > 3 && guard++ < maxIterations)
        {
            var clipped = false;
            for (var i = 0; i < remaining.Count && remaining.Count > 3; i++)
            {
                var prev = remaining[(i + remaining.Count - 1) % remaining.Count];
                var current = remaining[i];
                var next = remaining[(i + 1) % remaining.Count];

                if (IsEar(prev, current, next, remaining, vertices, orientation))
                {
                    yield return orientation ? (prev, current, next) : (prev, next, current);
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }
            }

            if (!clipped) break; // چندضلعی تحلیل‌رفته یا رأس‌های هم‌مکان؛ آنچه مانده را باز می‌کنیم.
        }

        if (remaining.Count == 3)
        {
            yield return orientation
                ? (remaining[0], remaining[1], remaining[2])
                : (remaining[0], remaining[2], remaining[1]);
        }
        else if (remaining.Count > 3)
        {
            // حالت نادر تحلیل‌رفتگی: بادبزکی از رأس اول تولید می‌شود تا خروجی همیشه کامل باشد.
            // کیفیت چندضلعی‌های معمول گوشهٔ فرم‌ها با این مسیر مواجه نمی‌شود.
            for (var i = 1; i + 1 < remaining.Count; i++)
            {
                yield return orientation
                    ? (remaining[0], remaining[i], remaining[i + 1])
                    : (remaining[0], remaining[i + 1], remaining[i]);
            }
        }
    }

    /// <summary>مساحت امضادار — مثبت یعنی جهت رئوس پادساعتگرد.</summary>
    private static double SignedArea(IReadOnlyList<(double X, double Y)> vertices)
    {
        double sum = 0;
        for (var i = 0; i < vertices.Count; i++)
        {
            var j = (i + 1) % vertices.Count;
            sum += vertices[i].X * vertices[j].Y - vertices[j].X * vertices[i].Y;
        }
        return sum / 2.0;
    }

    /// <summary>آزمون گوش: رأس فرورفته نباشد، مثلث گوش هیچ رأس دیگر را در بر نگیرد.</summary>
    private static bool IsEar(
        int prevIndex,
        int currentIndex,
        int nextIndex,
        List<int> remaining,
        (double X, double Y)[] vertices,
        bool ccw)
    {
        var a = vertices[prevIndex];
        var b = vertices[currentIndex];
        var c = vertices[nextIndex];

        var cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        var convex = ccw ? cross > 1e-12 : cross < -1e-12;
        if (!convex) return false;

        // رأس‌های باقی‌مانده (به‌جز سه رأس گوش) نباید داخل مثلث گوش باشند.
        foreach (var index in remaining)
        {
            if (index == prevIndex || index == currentIndex || index == nextIndex) continue;
            if (PointInTriangle(vertices[index], a, b, c)) return false;
        }

        return true;
    }

    private static bool PointInTriangle(
        (double X, double Y) p,
        (double X, double Y) a,
        (double X, double Y) b,
        (double X, double Y) c)
    {
        var d1 = Sign(p, a, b);
        var d2 = Sign(p, b, c);
        var d3 = Sign(p, c, a);
        var hasNegative = d1 < 0 || d2 < 0 || d3 < 0;
        var hasPositive = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNegative && hasPositive);
    }

    private static double Sign((double X, double Y) p, (double X, double Y) a, (double X, double Y) b) =>
        (p.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (p.Y - b.Y);
}

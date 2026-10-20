namespace Sahand.Geometry.Meshing;

/// <summary>
/// پیمایش چندضلعی حفره روی رویهٔ تخت سر — دایره (اسپنر) یا کپسول مورب (یک‌طرفه).
/// </summary>
/// <remarks>
/// نقاط پیمایش «ساعتگرد» دیده از +Z هستند (قرارداد مشترک با SlotCutter).
/// حفره‌ها از مرز بیرونی جدا هستند (سوراخ داخلی، نه برش لبه) — الگوی اسپنر/چشم‌مار
/// و پره‌های مورب یک‌طرفه هر دو همین‌اند.
/// </remarks>
public sealed class PocketOutline
{
    /// <summary>مختصات x نقاط پیمایش (ساعتگرد).</summary>
    public double[] X { get; }

    /// <summary>مختصات y نقاط پیمایش (ساعتگرد).</summary>
    public double[] Y { get; }

    private PocketOutline(double[] x, double[] y)
    {
        X = x;
        Y = y;
    }

    /// <summary>حفرهٔ دایره‌ای — اسپنر/چشم‌مار.</summary>
    /// <param name="centerX">مرکز x.</param>
    /// <param name="centerY">مرکز y.</param>
    /// <param name="radiusMm">شعاع سوراخ.</param>
    /// <param name="segments">تعداد نقاط نمونه.</param>
    public static PocketOutline Circle(double centerX, double centerY, double radiusMm, int segments = 20)
    {
        if (radiusMm <= 0) throw new ArgumentException("شعاع حفره باید مثبت باشد.");
        if (segments < 8) segments = 8;

        var xs = new double[segments];
        var ys = new double[segments];
        for (var i = 0; i < segments; i++)
        {
            // ساعتگرد: زاویه نزولی.
            var a = -i * 2.0 * Math.PI / segments;
            xs[i] = centerX + radiusMm * Math.Cos(a);
            ys[i] = centerY + radiusMm * Math.Sin(a);
        }
        return new PocketOutline(xs, ys);
    }

    /// <summary>حفرهٔ کپسولی (استادیوم) — پرهٔ مورب محرک یک‌طرفه.</summary>
    /// <param name="ax">سر A x.</param>
    /// <param name="ay">سر A y.</param>
    /// <param name="bx">سر B x.</param>
    /// <param name="by">سر B y.</param>
    /// <param name="radiusMm">نیم‌پهنای پره.</param>
    /// <param name="segmentsPerArc">نقاط هر نیم‌دایره.</param>
    public static PocketOutline Capsule(
        double ax, double ay, double bx, double by, double radiusMm, int segmentsPerArc = 10)
    {
        if (radiusMm <= 0) throw new ArgumentException("نیم‌پهنای پره باید مثبت باشد.");
        if (segmentsPerArc < 4) segmentsPerArc = 4;

        var dx = bx - ax;
        var dy = by - ay;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-9) return Circle(ax, ay, radiusMm, segmentsPerArc * 2);

        var ux = dx / length;
        var uy = dy / length;
        var dir = Math.Atan2(uy, ux);

        // پیمایش پادساعتگرد: نیم‌دایرهٔ دور A (از dir+90° تا dir+270°)، لبه تا B،
        // نیم‌دایرهٔ دور B (از dir−90° تا dir+90°)، بستن.
        var ccw = new List<(double X, double Y)>();
        for (var i = 0; i <= segmentsPerArc; i++)
        {
            var a = dir + Math.PI / 2.0 + i * Math.PI / segmentsPerArc;
            ccw.Add((ax + radiusMm * Math.Cos(a), ay + radiusMm * Math.Sin(a)));
        }
        for (var i = 0; i <= segmentsPerArc; i++)
        {
            var a = dir - Math.PI / 2.0 + i * Math.PI / segmentsPerArc;
            ccw.Add((bx + radiusMm * Math.Cos(a), by + radiusMm * Math.Sin(a)));
        }

        // معکوس برای ساعتگرد.
        ccw.Reverse();
        return new PocketOutline(ccw.Select(p => p.X).ToArray(), ccw.Select(p => p.Y).ToArray());
    }

    /// <summary>بیشینهٔ فاصلهٔ نقاط حفره از محور — برای گارد جا شدن در سر.</summary>
    public double MaxRadiusMm
    {
        get
        {
            var max = 0.0;
            for (var i = 0; i < X.Length; i++)
                max = Math.Max(max, Math.Sqrt(X[i] * X[i] + Y[i] * Y[i]));
            return max;
        }
    }
}

/// <summary>
/// ماشین‌کاری رویهٔ تختِ سر با حفره‌های داخلی — هندسهٔ واقعی بدون Boolean (گسترش ۰٫۵٫۰).
/// </summary>
/// <remarks>
/// کاربرد: محرک اسپنر (دو سوراخ گرد «چشم مار») و محرک یک‌طرفه (پره‌های مورب کپسولی).
/// اصل watertight: رویهٔ بالای سر، دیواره‌ها و کف حفره‌ها «همه اندیس رأس مشترک» دارند.
/// رویهٔ تخت با حفره‌ها با روش «پل‌زدن» (اتصال هر حفره به مرز بیرونی با راهروی صفر-عرض)
/// به یک چندضلعی سادهٔ弱 تبدیل و با بریدن گوش مثلث‌بندی می‌شود — همان رویکرد استاندارد
/// کتابخانه‌های مثلث‌بندی با حفره.
/// قرارداد جهت (سند ۷-۵): رویهٔ تخت سر رو به −Z است (ماده در z بزرگ‌تر)؛ رویه و کف
/// حفره‌ها نرمال −Z و دیواره‌ها نرمال رو به داخل حفره دارند.
/// </remarks>
public static class PocketCutter
{
    /// <summary>
    /// رویهٔ تختِ سوراخ‌دار + دیواره‌ها + کف‌های حفره‌ها را می‌سازد.
    /// </summary>
    /// <param name="mesh">مش مقصد.</param>
    /// <param name="grid">شبکهٔ زاویه‌ای (برای بازسازی مرز بیرونی).</param>
    /// <param name="topRing">اندیس رأس‌های حلقهٔ مرز رویهٔ بالا (به‌ترتیب زوایای شبکه).</param>
    /// <param name="topRadiusAtTheta">شعاع مرز رویه در زاویهٔ θ (برای سرهای چندگوش متغیر).</param>
    /// <param name="topZ">تراز رویهٔ بالا (ماده در z بزرگ‌تر).</param>
    /// <param name="depthMm">عمق مشترک حفره‌ها.</param>
    /// <param name="pockets">حفره‌ها (پیمایش ساعتگرد).</param>
    /// <exception cref="ArgumentException">حفره‌های بیرون مرز، هم‌پوشان یا پل‌های متقاطع.</exception>
    public static void Build(
        MeshBuilder mesh,
        AngularGrid grid,
        int[] topRing,
        Func<double, double> topRadiusAtTheta,
        double topZ,
        double depthMm,
        IReadOnlyList<PocketOutline> pockets)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(topRing);
        ArgumentNullException.ThrowIfNull(topRadiusAtTheta);
        ArgumentNullException.ThrowIfNull(pockets);
        if (pockets.Count == 0)
            throw new ArgumentException("دست‌کم یک حفره لازم است.", nameof(pockets));
        if (depthMm <= 0)
            throw new ArgumentException("عمق حفره باید مثبت باشد.");

        var count = grid.SegmentCount;
        if (topRing.Length != count)
            throw new ArgumentException("حلقهٔ مرز باید هم‌اندازهٔ شبکهٔ زاویه‌ای باشد.");

        // مرز بیرونی رویه (پادساعتگرد — زوایای شبکه صعودی).
        var outerX = new double[count];
        var outerY = new double[count];
        var minOuterR = double.MaxValue;
        for (var i = 0; i < count; i++)
        {
            var theta = grid[i];
            var r = topRadiusAtTheta(theta);
            outerX[i] = r * Math.Cos(theta);
            outerY[i] = r * Math.Sin(theta);
            minOuterR = Math.Min(minOuterR, r);
        }

        // --- گاردها: حفره‌ها باید کامل داخل مرز و جدا از هم باشند ---
        foreach (var pocket in pockets)
        {
            if (pocket.MaxRadiusMm + 1e-6 >= minOuterR)
                throw new ArgumentException(
                    $"حفرهٔ محرک (تا شعاع {pocket.MaxRadiusMm:0.###}mm) از مرز رویهٔ سر ({minOuterR:0.###}mm) بیرون می‌زند؛ قطر محرک را کم یا قطر سر را زیاد کنید.");
        }
        for (var a = 0; a < pockets.Count; a++)
        {
            for (var b = a + 1; b < pockets.Count; b++)
            {
                if (OutlinesOverlap(pockets[a], pockets[b]))
                    throw new ArgumentException(
                        "دو حفرهٔ محرک روی هم افتاده‌اند؛ چیدمان محرک معتبر نیست.");
            }
        }

        // --- رأس‌های هر حفره در دو تراز (رویه و کف) ---
        var floorZ = topZ + depthMm;
        var tops = new int[pockets.Count][];
        var floors = new int[pockets.Count][];
        for (var p = 0; p < pockets.Count; p++)
        {
            var pocket = pockets[p];
            var n = pocket.X.Length;
            var top = new int[n];
            var floor = new int[n];
            for (var i = 0; i < n; i++)
            {
                top[i] = mesh.AddVertex(pocket.X[i], pocket.Y[i], topZ);
                floor[i] = mesh.AddVertex(pocket.X[i], pocket.Y[i], floorZ);
            }
            tops[p] = top;
            floors[p] = floor;
        }

        // --- رویهٔ بالای سر با حفره‌ها: چندضلعی پل‌زده + مثلث‌بندی ---
        BuildPiercedTop(mesh, grid, topRing, outerX, outerY, topZ, pockets, tops);

        // --- دیواره‌ها و کف حفره‌ها ---
        for (var p = 0; p < pockets.Count; p++)
        {
            var n = pockets[p].X.Length;
            var top = tops[p];
            var floor = floors[p];

            // دیواره: نرمال رو به داخل حفره — یال بالایی با ترتیب اندیس (ساعتگرد) پیمایش
            // می‌شود تا با مرز حفره در رویه (پادساعتگرد) جهت مخالف بماند (مانیفولد).
            for (var i = 0; i < n; i++)
            {
                var next = (i + 1) % n;
                mesh.AddQuad(top[i], top[next], floor[next], floor[i]);
            }

            // کف: چندضلعی ساعتگرد → نرمال −Z (ماده در z بزرگ‌تر)؛ یال کف با ترتیب اندیس
            // پیمایش می‌شود و دیواره یال کف را معکوس می‌پیماید (مانیفولد).
            var polygon = new List<(double X, double Y, int V)>();
            for (var i = 0; i < n; i++)
                polygon.Add((pockets[p].X[i], pockets[p].Y[i], floor[i]));
            EmitPlanar(mesh, polygon, normalMinusZ: true);
        }
    }

    /// <summary>
    /// دو سوراخ گرد محرک اسپنر (چشم‌مار) — متقارن روی محور X.
    /// </summary>
    /// <param name="holeDiameterMm">قطر هر سوراخ.</param>
    /// <param name="centerSpacingMm">فاصلهٔ مراکز از هم.</param>
    public static IReadOnlyList<PocketOutline> SpannerHoles(double holeDiameterMm, double centerSpacingMm)
    {
        if (holeDiameterMm <= 0)
            throw new ArgumentException("قطر سوراخ اسپنر باید مثبت باشد.");
        if (centerSpacingMm <= holeDiameterMm)
            throw new ArgumentException(
                "فاصلهٔ مراکز سوراخ‌های اسپنر باید از قطر سوراخ بزرگ‌تر باشد؛ سوراخ‌ها به هم می‌چسبند.");

        var half = centerSpacingMm / 2.0;
        var segments = Math.Clamp((int)Math.Ceiling(Math.PI * holeDiameterMm / 0.25), 12, 36);
        return
        [
            PocketOutline.Circle(+half, 0, holeDiameterMm / 2.0, segments),
            PocketOutline.Circle(-half, 0, holeDiameterMm / 2.0, segments),
        ];
    }

    /// <summary>
    /// پره‌های مورب محرک یک‌طرفه — کپسول‌های مماسی (توربین‌ مانند) در زاویه‌های ۴۵°+k·۹۰°.
    /// </summary>
    /// <param name="headTopRadiusMm">شعاع مرز رویهٔ سر.</param>
    /// <param name="bladeWidthMm">پهنای پره (قطر کپسول).</param>
    /// <param name="bladeCount">تعداد پره (۴ پیش‌فرض).</param>
    public static IReadOnlyList<PocketOutline> OneWayBlades(
        double headTopRadiusMm, double bladeWidthMm, int bladeCount = 4)
    {
        if (headTopRadiusMm <= 0)
            throw new ArgumentException("شعاع رویهٔ سر باید مثبت باشد.");
        if (bladeWidthMm <= 0)
            throw new ArgumentException("پهنای پره باید مثبت باشد.");
        if (bladeCount is < 3 or > 8)
            throw new ArgumentException("تعداد پره‌های محرک یک‌طرفه باید بین ۳ و ۸ باشد.");

        // پره از شعاع ۰٫۳ تا ۰٫۸۵ رویه — کپسول مماسی (عمود بر شعع) در مرکز ۰٫۵۷۵.
        var r0 = headTopRadiusMm * 0.30;
        var r1 = headTopRadiusMm * 0.85;
        var rc = (r0 + r1) / 2.0;
        var halfLength = (r1 - r0) / 2.0;
        var halfWidth = bladeWidthMm / 2.0;

        var blades = new List<PocketOutline>();
        for (var k = 0; k < bladeCount; k++)
        {
            var phi = Math.PI / 4.0 + k * 2.0 * Math.PI / bladeCount;
            var cx = rc * Math.Cos(phi);
            var cy = rc * Math.Sin(phi);
            // جهت مماسی.
            var tx = -Math.Sin(phi);
            var ty = Math.Cos(phi);

            blades.Add(PocketOutline.Capsule(
                cx - halfLength * tx, cy - halfLength * ty,
                cx + halfLength * tx, cy + halfLength * ty,
                halfWidth));
        }
        return blades;
    }

    // -------------------------------------------------------------------------

    /// <summary>رویهٔ تختِ سوراخ‌دار — پل‌زدن حفره‌ها به مرز و مثلث‌بندی با بریدن گوش.</summary>
    /// <remarks>
    /// جهت‌ها (قرارداد مانیفولد): دیوارهٔ جانبیِ سر (سازندهٔ بیرونی) یال مرز رویه را با زاویهٔ
    /// «صعودی» می‌پیماید؛ پس رویهٔ سوراخ‌دار باید مرز بیرونی را «نزولی» پیمایش کند (معکوس).
    /// دیواره‌های حفره نیز یال بالایی حفره را با ترتیب اندیس آرایهٔ (ساعتگرد) می‌پیمایند؛ پس
    /// مرز حفره در رویه باید «معکوسِ اندیس» (پادساعتگرد) پیمایش شود. نتیجه: چندضلعی ادغامی
    /// «کلا ساعتگرد» با حفره‌های پادساعتگرد — هر یال مشترک دقیقاً دو بار با جهت مخالف.
    /// </remarks>
    private static void BuildPiercedTop(
        MeshBuilder mesh,
        AngularGrid grid,
        int[] topRing,
        double[] outerX,
        double[] outerY,
        double topZ,
        IReadOnlyList<PocketOutline> pockets,
        int[][] pocketTops)
    {
        var count = topRing.Length;

        // حفره‌ها به‌ترتیب زاویهٔ پل (رادیال بیرونی‌ترین نقطه) مرتب می‌شوند.
        var bridges = new List<(int PocketIndex, int HoleVertex, double Angle, int OuterIndex)>();
        for (var p = 0; p < pockets.Count; p++)
        {
            var pocket = pockets[p];
            var holeVertex = 0;
            var bestR = -1.0;
            for (var i = 0; i < pocket.X.Length; i++)
            {
                var r = Math.Sqrt(pocket.X[i] * pocket.X[i] + pocket.Y[i] * pocket.Y[i]);
                if (r > bestR)
                {
                    bestR = r;
                    holeVertex = i;
                }
            }
            var angle = Math.Atan2(pocket.Y[holeVertex], pocket.X[holeVertex]);

            // نزدیک‌ترین رأس مرز بیرونی به این زاویه.
            var outerIndex = 0;
            var bestDelta = double.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var theta = grid[i];
                var delta = Math.Abs(Math.Abs(theta - angle + Math.PI) % (2.0 * Math.PI) - Math.PI);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    outerIndex = i;
                }
            }
            bridges.Add((p, holeVertex, angle, outerIndex));
        }

        // گارد: پل‌ها نباید به یک رأس مرز بیفتند (زاویه‌ها جدایش کافی دارند).
        var duplicatedOuter = bridges.GroupBy(b => b.OuterIndex).FirstOrDefault(g => g.Count() > 1);
        if (duplicatedOuter is not null)
            throw new ArgumentException(
                "چیدمان حفره‌های محرک معتبر نیست: دو حفره پلِ یکسان به مرز سر می‌خورند؛ فاصلهٔ زاویه‌ای حفره‌ها را بیشتر کنید.");

        // چندضلعی ادغام‌شده: مرز نزولی (ساعتگرد کل)؛ در هر رأس پل، حفره معکوسِ اندیس پیمایش می‌شود.
        var merged = new List<(double X, double Y, int V)>();
        var bridgeByOuter = bridges.ToDictionary(b => b.OuterIndex);

        for (var i = count - 1; i >= 0; i--)
        {
            merged.Add((outerX[i], outerY[i], topRing[i]));

            if (!bridgeByOuter.TryGetValue(i, out var bridge))
                continue;

            // ورود به حفره از بیرونی‌ترین رأس آن و پیمایش کامل معکوسِ اندیس (پادساعتگرد) تا بازگشت.
            var pocket = pockets[bridge.PocketIndex];
            var top = pocketTops[bridge.PocketIndex];
            var n = pocket.X.Length;
            for (var j = 0; j < n; j++)
            {
                var idx = ((bridge.HoleVertex - j) % n + n) % n;
                merged.Add((pocket.X[idx], pocket.Y[idx], top[idx]));
            }
            // بازگشت به رأس حفره (تکرار) و سپس رأس مرز (تکرار) — راهروی صفر-عرض پل.
            merged.Add((pocket.X[bridge.HoleVertex], pocket.Y[bridge.HoleVertex], top[bridge.HoleVertex]));
            merged.Add((outerX[i], outerY[i], topRing[i]));
        }

        EmitPlanar(mesh, merged, normalMinusZ: true);
    }

    /// <summary>چندضلعی مسطح افقی را مثلث‌بندی و با جهت دلخواه می‌سازد.</summary>
    private static void EmitPlanar(MeshBuilder mesh, List<(double X, double Y, int V)> polygon, bool normalMinusZ)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(polygon.Count, 3);

        var indices = polygon.Select(p => p.V).ToArray();
        var coords = polygon.Select(p => (p.X, p.Y)).ToArray();

        foreach (var (a, b, c) in TriangulateWithHoles(polygon.Count, i => coords[i]))
        {
            // مثلث‌های مثلث‌بند همیشه پادساعتگرد (+Z) هستند؛ برای نرمال −Z معکوس می‌شوند.
            if (normalMinusZ)
                mesh.AddTriangle(indices[a], indices[c], indices[b]);
            else
                mesh.AddTriangle(indices[a], indices[b], indices[c]);
        }
    }

    /// <summary>
    /// مثلث‌بندی چندضلعی (با حفره‌های پل‌زده) با بریدن گوش — آزمون نقطه-در-مثلث «سخت‌گیر»
    /// (نقاط روی مرز داخل شمرده نمی‌شوند تا رأس‌های تکراریِ پل گوش‌های معتبر را قفل نکنند).
    /// </summary>
    private static IEnumerable<(int A, int B, int C)> TriangulateWithHoles(
        int vertexCount,
        Func<int, (double X, double Y)> getVertex)
    {
        var vertices = new (double X, double Y)[vertexCount];
        for (var i = 0; i < vertexCount; i++) vertices[i] = getVertex(i);

        var orientation = SignedArea(vertices) >= 0;
        var remaining = new List<int>(vertexCount);
        for (var i = 0; i < vertexCount; i++) remaining.Add(i);

        var guard = 0;
        var maxIterations = vertexCount * vertexCount;

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

            if (!clipped) break;
        }

        if (remaining.Count == 3)
        {
            yield return orientation
                ? (remaining[0], remaining[1], remaining[2])
                : (remaining[0], remaining[2], remaining[1]);
        }
        else if (remaining.Count > 3)
        {
            // حالت نادر: بادبزکی از رأس اول تا خروجی همیشه کامل بماند.
            for (var i = 1; i + 1 < remaining.Count; i++)
            {
                yield return orientation
                    ? (remaining[0], remaining[i], remaining[i + 1])
                    : (remaining[0], remaining[i + 1], remaining[i]);
            }
        }
    }

    /// <summary>مساحت امضادار — مثبت یعنی پادساعتگرد.</summary>
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

    /// <summary>آزمون گوش با نقطه-در-مثلث سخت‌گیر (مرز = بیرون).</summary>
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
        var convex = ccw ? cross > 1e-10 : cross < -1e-10;
        if (!convex) return false;

        foreach (var index in remaining)
        {
            if (index == prevIndex || index == currentIndex || index == nextIndex) continue;
            if (PointStrictlyInsideTriangle(vertices[index], a, b, c)) return false;
        }

        return true;
    }

    /// <summary>نقطه «اکیداً داخل» مثلث؟ نقاط مرز (و رأس‌های تکراری پل) داخل نیستند.</summary>
    /// <remarks>
    /// قرارداد درست: نقطهٔ داخل مثلث نسبت به هر سه ضلع «هم‌علامت» است (مثلث پادساعتگرد:
    /// همهٔ مثبت؛ ساعتگرد: همهٔ منفی). آمیخته‌بودن علامت‌ها یعنی بیرون. آستانهٔ eps نقاط
    /// روی مرز را (که برای رأس‌های تکراری پل حیاتی است) «بیرون» می‌شمارد.
    /// </remarks>
    private static bool PointStrictlyInsideTriangle(
        (double X, double Y) p,
        (double X, double Y) a,
        (double X, double Y) b,
        (double X, double Y) c)
    {
        const double eps = 1e-9;
        var d1 = Sign(p, a, b);
        var d2 = Sign(p, b, c);
        var d3 = Sign(p, c, a);

        var allNegative = d1 < -eps && d2 < -eps && d3 < -eps;
        var allPositive = d1 > eps && d2 > eps && d3 > eps;
        return allNegative || allPositive;
    }

    private static double Sign((double X, double Y) p, (double X, double Y) a, (double X, double Y) b) =>
        (p.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (p.Y - b.Y);

    /// <summary>آیا دو پیمایش هم‌پوشان‌اند؟ (دایرهٔ محیطی اجزا + فاصلهٔ نقاط — تقریب محافظه‌کار).</summary>
    private static bool OutlinesOverlap(PocketOutline a, PocketOutline b)
    {
        // دایرهٔ محیطی هر پیمایش (مرکز میانگین + بیشینهٔ فاصله) —پوشش محافظه‌کار.
        var (acx, acy, ar) = BoundingCircle(a);
        var (bcx, bcy, br) = BoundingCircle(b);
        var centerDistance = Math.Sqrt((acx - bcx) * (acx - bcx) + (acy - bcy) * (acy - bcy));
        if (centerDistance >= ar + br + 1e-9)
            return false; // قطعاً جدا.

        // پوشش دایره‌ای نامطمئن: نزدیکی نقطه‌به‌نقطه را هم آزمون می‌کنیم.
        for (var i = 0; i < a.X.Length; i++)
        {
            for (var j = 0; j < b.X.Length; j++)
            {
                var dx = a.X[i] - b.X[j];
                var dy = a.Y[i] - b.Y[j];
                if (dx * dx + dy * dy < 1e-6)
                    return true;
            }
        }
        return centerDistance < Math.Abs(ar - br) - 1e-9; // یکی داخل دیگری (تودرتو).
    }

    /// <summary>دایرهٔ محیطی تقریبی پیمایش — مرکز میانگین و شعاع بیشینه.</summary>
    private static (double Cx, double Cy, double R) BoundingCircle(PocketOutline outline)
    {
        double cx = 0, cy = 0;
        for (var i = 0; i < outline.X.Length; i++)
        {
            cx += outline.X[i];
            cy += outline.Y[i];
        }
        cx /= outline.X.Length;
        cy /= outline.X.Length;

        var r = 0.0;
        for (var i = 0; i < outline.X.Length; i++)
            r = Math.Max(r, Math.Sqrt(
                (outline.X[i] - cx) * (outline.X[i] - cx) +
                (outline.Y[i] - cy) * (outline.Y[i] - cy)));
        return (cx, cy, r);
    }
}

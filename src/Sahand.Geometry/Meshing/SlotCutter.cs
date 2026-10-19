namespace Sahand.Geometry.Meshing;

/// <summary>
/// برش شیار تخت/صلیبی (فیلیپس) روی سر با رویهٔ تختِ استوانه‌ای — هندسهٔ واقعی بدون Boolean.
/// </summary>
/// <remarks>
/// اصل watertight: همهٔ پچ‌ها «اندیس رأس» مشترک دارند؛ گوشه‌های روی دایرهٔ سر از رأس‌های
/// حلقهٔ دیواره و فقط گوشه‌های داخلی صلیب (±w/2, ±w/2) رأس جدید هستند.
/// قرارداد جهت (سند ۷-۵): رویهٔ تخت سر رو به −Z است (مادهٔ سر در z بزرگ‌تر)؛ رویه و کف
/// شیار «ساعتگرد» (نرمال −Z) ساخته می‌شوند و دیواره‌های شیار نرمال به سمت حفره دارند:
/// ماده بیرونِ شیار است، پس دیوارهٔ y=+w/2 نرمال −ŷ و y=−w/2 نرمال +ŷ دارد (و مشابه x).
/// جهت کمان‌ها در پیمایش چندضلعی‌ها دقیقاً برعکس جهت پیمایش ساعتگرد است (نزولی).
/// </remarks>
public static class SlotCutter
{
    /// <summary>
    /// زوایای خروج شیار از دیوارهٔ استوانه‌ای — برای درج در شبکهٔ زاویه‌ای فراخوان.
    /// </summary>
    public static IEnumerable<double> GetSpecialAngles(double headRadiusMm, double slotWidthMm, bool phillips)
    {
        var alpha = Math.Asin(Math.Clamp(slotWidthMm / (2.0 * headRadiusMm), 0.0, 0.999));

        // شیار امتداد X: خروج‌ها در ±α و π±α.
        yield return alpha;
        yield return Math.PI - alpha;
        yield return Math.PI + alpha;
        yield return 2.0 * Math.PI - alpha;

        if (phillips)
        {
            // شیار امتداد Y: خروج‌ها در π/2±α و 3π/2±α.
            yield return Math.PI / 2.0 - alpha;
            yield return Math.PI / 2.0 + alpha;
            yield return 3.0 * Math.PI / 2.0 - alpha;
            yield return 3.0 * Math.PI / 2.0 + alpha;
        }
    }

    /// <summary>
    /// دیوارهٔ جانبی + کف شیار + دیواره‌های شیار + رویهٔ تختِ بریده‌شده را می‌سازد.
    /// </summary>
    /// <returns>حلقهٔ رأس‌های دیواره در تراز sideFromZ (برای درپوش حلقه‌ای اتکای سر).</returns>
    public static int[] Build(
        MeshBuilder mesh,
        AngularGrid grid,
        double headRadiusMm,
        double sideFromZ,
        double topZ,
        double slotWidthMm,
        double slotDepthMm,
        bool phillips)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(grid);
        if (slotWidthMm <= 0)
            throw new ArgumentException("پهنای شیار باید مثبت باشد.");

        if (slotWidthMm >= 2.0 * headRadiusMm * (phillips ? 0.5 : 0.95))
            throw new ArgumentException(
                $"پهنای شیار ({slotWidthMm:0.###}mm) برای سر به شعاع {headRadiusMm:0.###}mm بیش از حد بزرگ است.");

        if (slotDepthMm <= 0 || topZ + slotDepthMm >= sideFromZ)
            throw new ArgumentException("عمق شیار نباید از ارتفاع سر بیشتر شود.");

        var floorZ = topZ + slotDepthMm;
        var radius = headRadiusMm;
        var halfWidth = slotWidthMm / 2.0;
        var alpha = Math.Asin(halfWidth / radius);

        var angles = Enumerable.Range(0, grid.SegmentCount).Select(i => grid[i]).ToArray();
        var count = angles.Length;

        // --- رأس‌های مشترک ---
        var ringFloor = new int[count];
        var ringSide = new int[count];
        var ringTop = new int[count];
        var hasTop = phillips ? BuildPhillipsTopMask(angles, alpha) : BuildSlotTopMask(angles, alpha);

        for (var i = 0; i < count; i++)
        {
            ringFloor[i] = mesh.AddCylindrical(radius, angles[i], floorZ);
            ringSide[i] = mesh.AddCylindrical(radius, angles[i], sideFromZ);
            // رأس رویه در همهٔ زوایا ساخته می‌شود: نوار کناری فقط در زوایای خارج از شیار به آن
            // می‌چسبد، اما گوشه‌های دیواره/رویه در زوایای خروج شیار به همین رأس‌ها نیاز دارند.
            ringTop[i] = mesh.AddCylindrical(radius, angles[i], topZ);
        }

        // گوشه‌های داخلی صلیب (±w/2, ±w/2) — تنها رأس‌های غیرحلقه‌ای؛ کش و بازاستفاده.
        var innerCorners = new Dictionary<(double X, double Y, double Z), int>();
        int Inner(double x, double y, double z)
        {
            var key = (x, y, z);
            if (innerCorners.TryGetValue(key, out var index)) return index;
            index = mesh.AddVertex(x, y, z);
            innerCorners[key] = index;
            return index;
        }

        // --- دیوارهٔ جانبی (نوارهای رو به بیرون؛ ترازهای z صعودی) ---
        for (var i = 0; i < count; i++)
        {
            var next = (i + 1) % count;
            // نوار پایینی: floorZ < sideFromZ.
            mesh.AddQuad(ringFloor[i], ringFloor[next], ringSide[next], ringSide[i]);

            // نوار بالایی فقط روی کمان‌های خارج از شیار: topZ < floorZ.
            if (hasTop[i] && hasTop[next])
                mesh.AddQuad(ringTop[i], ringTop[next], ringFloor[next], ringFloor[i]);
        }

        if (phillips)
        {
            BuildPhillipsFloor(mesh, angles, ringFloor, radius, halfWidth, floorZ, alpha, Inner);
            BuildPhillipsWalls(mesh, halfWidth, topZ, floorZ, alpha, Inner, angles, ringTop, ringFloor);
            BuildPhillipsTop(mesh, angles, ringTop, radius, halfWidth, topZ, alpha, Inner);
        }
        else
        {
            BuildSlotFloor(mesh, angles, ringFloor, radius, floorZ, alpha);
            BuildSlotWalls(mesh, halfWidth, topZ, floorZ, alpha, angles, ringTop, ringFloor);
            BuildSlotTop(mesh, angles, ringTop, radius, floorZ, topZ, alpha);
        }

        return ringSide;
    }

    /// <summary>نقاب زوایای خارج از پاهنای شیار تکی (امتداد X).</summary>
    private static bool[] BuildSlotTopMask(double[] angles, double alpha)
    {
        var mask = new bool[angles.Length];
        for (var i = 0; i < angles.Length; i++)
        {
            var a = angles[i];
            var inSlotBand = a <= alpha || a >= 2.0 * Math.PI - alpha ||
                             (a >= Math.PI - alpha && a <= Math.PI + alpha);
            mask[i] = !inSlotBand;
        }
        return mask;
    }

    /// <summary>نقاب زوایای خارج از پاهنای هر دو شیار (X و Y).</summary>
    private static bool[] BuildPhillipsTopMask(double[] angles, double alpha)
    {
        var mask = new bool[angles.Length];
        for (var i = 0; i < angles.Length; i++)
        {
            var a = angles[i];
            var inXBand = a <= alpha || a >= 2.0 * Math.PI - alpha ||
                          (a >= Math.PI - alpha && a <= Math.PI + alpha);
            var inYBand = (a >= Math.PI / 2.0 - alpha && a <= Math.PI / 2.0 + alpha) ||
                          (a >= 3.0 * Math.PI / 2.0 - alpha && a <= 3.0 * Math.PI / 2.0 + alpha);
            mask[i] = !inXBand && !inYBand;
        }
        return mask;
    }

    /// <summary>کف شیار تکی: کپسول — همهٔ رأس‌ها از حلقهٔ کف؛ پیمایش ساعتگرد (نرمال −Z).</summary>
    private static void BuildSlotFloor(
        MeshBuilder mesh,
        double[] angles,
        int[] ringFloor,
        double radius,
        double floorZ,
        double alpha)
    {
        var polygon = new List<(double X, double Y, int V)>();

        // ساعتگرد: کمان راست نزولی (α→−α)، وتر پایین، کمان چپ نزولی (π+α→π−α)، وتر بالا (بستن).
        AddArc(polygon, angles, ringFloor, radius, alpha, -alpha);
        AddArc(polygon, angles, ringFloor, radius, Math.PI + alpha, Math.PI - alpha);

        TriangulatePlanar(mesh, polygon, clockwise: true);
    }

    /// <summary>کف صلیبی: چلیپا — ساعتگرد؛ گوشه‌های داخلی رأس جدید و کمان‌ها نزولی.</summary>
    private static void BuildPhillipsFloor(
        MeshBuilder mesh,
        double[] angles,
        int[] ringFloor,
        double radius,
        double halfWidth,
        double floorZ,
        double alpha,
        Func<double, double, double, int> inner)
    {
        var polygon = new List<(double X, double Y, int V)>
        {
            (halfWidth, halfWidth, inner(halfWidth, halfWidth, floorZ)),
        };

        // بازوی راست → پایین → چپ → بالا؛ همهٔ کمان‌ها نزولی مطابق پیمایش ساعتگرد.
        AddArc(polygon, angles, ringFloor, radius, alpha, -alpha);
        polygon.Add((halfWidth, -halfWidth, inner(halfWidth, -halfWidth, floorZ)));
        AddArc(polygon, angles, ringFloor, radius, 3.0 * Math.PI / 2.0 + alpha, 3.0 * Math.PI / 2.0 - alpha);
        polygon.Add((-halfWidth, -halfWidth, inner(-halfWidth, -halfWidth, floorZ)));
        AddArc(polygon, angles, ringFloor, radius, Math.PI + alpha, Math.PI - alpha);
        polygon.Add((-halfWidth, halfWidth, inner(-halfWidth, halfWidth, floorZ)));
        AddArc(polygon, angles, ringFloor, radius, Math.PI / 2.0 + alpha, Math.PI / 2.0 - alpha);

        TriangulatePlanar(mesh, polygon, clockwise: true);
    }

    /// <summary>دیواره‌های شیار تکی — نرمال به سمت حفره (y=+w/2 → −ŷ؛ y=−w/2 → +ŷ).</summary>
    private static void BuildSlotWalls(
        MeshBuilder mesh,
        double halfWidth,
        double topZ,
        double floorZ,
        double alpha,
        double[] angles,
        int[] ringTop,
        int[] ringFloor)
    {
        int TopAt(double target) => RingIndexAt(angles, ringTop, target);
        int FloorAt(double target) => RingIndexAt(angles, ringFloor, target);

        // دیوارهٔ y=+w/2: گوشه‌ها = خروج‌های شیار (α و π−α) روی حلقه‌ها.
        // نرمال −ŷ: (بالا-راست، پایین-راست، پایین-چپ، بالا-چپ).
        mesh.AddQuad(TopAt(alpha), FloorAt(alpha), FloorAt(Math.PI - alpha), TopAt(Math.PI - alpha));

        // دیوارهٔ y=−w/2: نرمال +ŷ — گردش معکوس.
        mesh.AddQuad(
            TopAt(2.0 * Math.PI - alpha), TopAt(Math.PI + alpha),
            FloorAt(Math.PI + alpha), FloorAt(2.0 * Math.PI - alpha));
    }

    /// <summary>دیواره‌های صلیبی — ۸ زیرمستطیل با حذف ناحیهٔ تلاقی شیار دوم.</summary>
    private static void BuildPhillipsWalls(
        MeshBuilder mesh,
        double halfWidth,
        double topZ,
        double floorZ,
        double alpha,
        Func<double, double, double, int> inner,
        double[] angles,
        int[] ringTop,
        int[] ringFloor)
    {
        int TopAt(double t) => RingIndexAt(angles, ringTop, t);
        int FloorAt(double t) => RingIndexAt(angles, ringFloor, t);

        var q1 = Math.PI / 2.0;
        var q3 = 3.0 * Math.PI / 2.0;

        // --- دیواره‌های y=+w/2 (نرمال −ŷ): بخش‌های x∈[w/2, chord] و x∈[−chord, −w/2] ---
        mesh.AddQuad(TopAt(alpha), FloorAt(alpha), inner(halfWidth, halfWidth, floorZ), inner(halfWidth, halfWidth, topZ));
        mesh.AddQuad(
            inner(-halfWidth, halfWidth, topZ), inner(-halfWidth, halfWidth, floorZ),
            FloorAt(Math.PI - alpha), TopAt(Math.PI - alpha));

        // --- دیواره‌های y=−w/2 (نرمال +ŷ) ---
        mesh.AddQuad(
            TopAt(2.0 * Math.PI - alpha), inner(halfWidth, -halfWidth, topZ),
            inner(halfWidth, -halfWidth, floorZ), FloorAt(2.0 * Math.PI - alpha));
        mesh.AddQuad(
            inner(-halfWidth, -halfWidth, topZ), TopAt(Math.PI + alpha),
            FloorAt(Math.PI + alpha), inner(-halfWidth, -halfWidth, floorZ));

        // --- دیواره‌های x=+w/2 (نرمال −x̂): بخش‌های y∈[w/2, chord] و y∈[−chord, −w/2] ---
        mesh.AddQuad(
            TopAt(q1 - alpha), inner(halfWidth, halfWidth, topZ),
            inner(halfWidth, halfWidth, floorZ), FloorAt(q1 - alpha));
        mesh.AddQuad(
            TopAt(q3 + alpha), FloorAt(q3 + alpha),
            inner(halfWidth, -halfWidth, floorZ), inner(halfWidth, -halfWidth, topZ));

        // --- دیواره‌های x=−w/2 (نرمال +x̂) ---
        mesh.AddQuad(
            TopAt(q1 + alpha), FloorAt(q1 + alpha),
            inner(-halfWidth, halfWidth, floorZ), inner(-halfWidth, halfWidth, topZ));
        mesh.AddQuad(
            TopAt(q3 - alpha), inner(-halfWidth, -halfWidth, topZ),
            inner(-halfWidth, -halfWidth, floorZ), FloorAt(q3 - alpha));
    }

    /// <summary>رویهٔ تختِ شیار تکی: دو قطعه — ساعتگرد (نرمال −Z)؛ رأس‌ها فقط از حلقهٔ رویه.</summary>
    private static void BuildSlotTop(
        MeshBuilder mesh,
        double[] angles,
        int[] ringTop,
        double radius,
        double floorZ,
        double topZ,
        double alpha)
    {
        // قطعهٔ بالا: کمان بالایی نزولی (π−α→α)؛ یال بستن = وتر شیار.
        var upper = new List<(double X, double Y, int V)>();
        AddArc(upper, angles, ringTop, radius, Math.PI - alpha, alpha);
        TriangulatePlanar(mesh, upper, clockwise: true);

        // قطعهٔ پایین: کمان پایینی صعودی (π+α→2π−α)؛ یال بستن = وتر.
        var lower = new List<(double X, double Y, int V)>();
        AddArc(lower, angles, ringTop, radius, Math.PI + alpha, 2.0 * Math.PI - alpha);
        TriangulatePlanar(mesh, lower, clockwise: true);
    }

    /// <summary>رویهٔ تخت صلیبی: چهار قطعهٔ گوشه‌ای — ساعتگرد.</summary>
    private static void BuildPhillipsTop(
        MeshBuilder mesh,
        double[] angles,
        int[] ringTop,
        double radius,
        double halfWidth,
        double topZ,
        double alpha,
        Func<double, double, double, int> inner)
    {
        var q1 = Math.PI / 2.0;
        var q3 = 3.0 * Math.PI / 2.0;

        // هر گوشه: رأس داخلی + کمان نزولی بین دو خروج شیار؛ یال‌های بستن = ضلع‌های مستقیم.
        var upperRight = new List<(double X, double Y, int V)>
        {
            (halfWidth, halfWidth, inner(halfWidth, halfWidth, topZ)),
        };
        AddArc(upperRight, angles, ringTop, radius, q1 - alpha, alpha);
        TriangulatePlanar(mesh, upperRight, clockwise: true);

        var upperLeft = new List<(double X, double Y, int V)>
        {
            (-halfWidth, halfWidth, inner(-halfWidth, halfWidth, topZ)),
        };
        AddArc(upperLeft, angles, ringTop, radius, Math.PI - alpha, q1 + alpha);
        TriangulatePlanar(mesh, upperLeft, clockwise: true);

        var lowerLeft = new List<(double X, double Y, int V)>
        {
            (-halfWidth, -halfWidth, inner(-halfWidth, -halfWidth, topZ)),
        };
        AddArc(lowerLeft, angles, ringTop, radius, q3 - alpha, Math.PI + alpha);
        TriangulatePlanar(mesh, lowerLeft, clockwise: true);

        var lowerRight = new List<(double X, double Y, int V)>
        {
            (halfWidth, -halfWidth, inner(halfWidth, -halfWidth, topZ)),
        };
        AddArc(lowerRight, angles, ringTop, radius, 2.0 * Math.PI - alpha, q3 + alpha);
        TriangulatePlanar(mesh, lowerRight, clockwise: true);
    }

    /// <summary>اندیس حلقه در زاویهٔ هدف — زوایای ویژهٔ شیار همیشه در شبکه هستند.</summary>
    private static int RingIndexAt(double[] angles, int[] ring, double targetAngle)
    {
        for (var i = 0; i < angles.Length; i++)
        {
            if (Math.Abs(angles[i] - targetAngle) < 1e-9) return ring[i];
        }
        throw new InvalidOperationException($"زوایای ویژهٔ شیار ({targetAngle:0.####} رادیان) در شبکه نیست.");
    }

    /// <summary>نقاط کمان زوایای شبکه در بازهٔ [from, to] — شامل هر دو سر؛ جهت پیمایش حفظ می‌شود.</summary>
    private static void AddArc(
        List<(double X, double Y, int V)> polygon,
        double[] angles,
        int[] ring,
        double radius,
        double fromAngle,
        double toAngle)
    {
        if (toAngle >= fromAngle)
        {
            for (var i = 0; i < angles.Length; i++)
            {
                if (angles[i] < fromAngle - 1e-12) continue;
                if (angles[i] > toAngle + 1e-12) break;
                polygon.Add((radius * Math.Cos(angles[i]), radius * Math.Sin(angles[i]), ring[i]));
            }
        }
        else
        {
            // نزولی: از زاویهٔ بزرگ به کوچک (پیمایش ساعتگرد چندضلعی‌های این کلاس).
            for (var i = angles.Length - 1; i >= 0; i--)
            {
                if (angles[i] > fromAngle + 1e-12) continue;
                if (angles[i] < toAngle - 1e-12) break;
                polygon.Add((radius * Math.Cos(angles[i]), radius * Math.Sin(angles[i]), ring[i]));
            }
        }
    }

    /// <summary>چندضلعی مسطح افقی را مثلث‌بندی می‌کند؛ clockwise یعنی نرمال −Z.</summary>
    private static void TriangulatePlanar(MeshBuilder mesh, List<(double X, double Y, int V)> polygon, bool clockwise)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(polygon.Count, 3);

        var indices = polygon.Select(p => p.V).ToArray();
        var coords = polygon.Select(p => (p.X, p.Y)).ToArray();

        foreach (var (a, b, c) in PolygonTriangulation.Triangulate(polygon.Count, i => coords[i]))
        {
            if (clockwise)
                mesh.AddTriangle(indices[a], indices[c], indices[b]);
            else
                mesh.AddTriangle(indices[a], indices[b], indices[c]);
        }
    }
}

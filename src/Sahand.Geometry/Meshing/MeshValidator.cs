using System.Numerics;

namespace Sahand.Geometry.Meshing;

/// <summary>
/// نتیجهٔ اعتبارسنجی مش — وضعیت هر بررسی جداگانه اعلام می‌شود.
/// </summary>
/// <param name="IsWatertight">هر یال ناموجه دقیقاً دو وجه دارد و جهت‌ها مخالف‌اند.</param>
/// <param name="HasConsistentOrientation">وجه‌های مجاور روی یال مشترک جهت یکسان «بیرون» دارند.</param>
/// <param name="HasZeroAreaTriangles">وجه‌های بدون مساحت وجود دارد (معیوب ولی مسدودکننده نیست).</param>
/// <param name="SelfIntersections">تعداد برخوردهای خود-تقاطع تشخیص‌ داده‌شده (هیوریستیکی).</param>
/// <param name="Issues">شرح فارسی مسائل برای گزارش/هشدار UI.</param>
public sealed record MeshValidationResult(
    bool IsWatertight,
    bool HasConsistentOrientation,
    bool HasZeroAreaTriangles,
    int SelfIntersections,
    IReadOnlyList<string> Issues)
{
    /// <summary>خلاصهٔ «سالم برای چاپ»: بسته + جهت‌دار + بدون تقاطع.</summary>
    public bool IsPrintSafe => IsWatertight && HasConsistentOrientation && SelfIntersections == 0;
}

/// <summary>
/// اعتبارسنج مش: بسته‌بودن (watertight)، مانیفولد یالی، جهت‌دهی و تقاطع خودی (الزام EXP-03).
/// </summary>
/// <remarks>
/// بررسی تقاطع خودی با شبکهٔ فضایی یکنواخت انجام می‌شود و «هیوریستیکی» است: کشف قطعی
/// تضمین‌شده نیست (محدودیت صادقانه سند ۴-۲). یال‌ها با کلید مرتب‌شده مقایسه می‌شوند تا
/// ترتیب رأس‌ها معیار نباشد. پیچیدگی: یال‌ها O(n)؛ تقاطع حدوداً O(n·k) با k میانگین همسایه‌های سلول.
/// </remarks>
public static class MeshValidator
{
    /// <summary>
    /// سقف تعداد وجه برای بررسی تقاطع خودی (گام ۰٫۴٫۰) — بالاتر از این، بررسی جفتی سلولی
    /// حافظه/زمان انفجاری دارد و با پیام صادقانه رد می‌شود (مش‌های کیفیت فوق‌دقیق/حدی).
    /// </summary>
    public const int MaxSelfIntersectionFaceCount = 250_000;

    /// <summary>
    /// مش را کامل بررسی می‌کند.
    /// </summary>
    /// <param name="mesh">مش ورودی.</param>
    /// <param name="checkSelfIntersection">بررسی تقاطع خودی (پرهزینه) — برای خروجی نهایی روشن باشد.</param>
    public static MeshValidationResult Validate(Mesh mesh, bool checkSelfIntersection = true)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var issues = new List<string>();

        if (mesh.Triangles.Length == 0)
            return new MeshValidationResult(false, false, false, 0, ["مش بدون وجه است."]);

        var (edgeCount, boundaryEdges, badEdges, zeroArea) = AnalyzeEdges(mesh);

        var watertight = badEdges == 0 && boundaryEdges == 0;
        if (boundaryEdges > 0)
            issues.Add($"{boundaryEdges} یال مرزی پیدا شد؛ مش باز است (watertight نیست).");
        if (badEdges > 0)
            issues.Add($"{badEdges} یال نامانیفولد/بدجهت پیدا شد؛ هر یال باید دقیقاً دو وجه با جهت مخالف داشته باشد.");

        if (zeroArea > 0)
            issues.Add($"{zeroArea} وجه بدون مساحت پیدا شد (رأس‌های هم‌مکان)؛ خروجی ممکن است توسط اسلایسر رد شود.");

        var orientationOk = badEdges == 0 && boundaryEdges == 0;

        // حجم امضادار: برای مش بستهٔ درست باید مثبت باشد؛ منفی یعنی کلیت winding معکوس است.
        if (watertight)
        {
            var volume = mesh.SignedVolumeMm3();
            if (volume <= 0)
            {
                issues.Add("حجم امضادار غیرمثبت است؛ جهت کلی وجه‌ها معکوس یا هندسهٔ خود-متقاطع است.");
                orientationOk = false;
            }
        }

        var selfIntersections = 0;
        if (checkSelfIntersection)
        {
            if (mesh.Triangles.Length > MaxSelfIntersectionFaceCount)
            {
                // گارد مقیاس (گام ۰٫۴٫۰): با کیفیت‌های فوق‌دقیق/حدی، مش به میلیون‌ها وجه می‌رسد؛
                // بررسی جفتیِ سلولی روی چنین تراکمی حافظه/زمان انفجاری دارد و میزبان را می‌اندازد.
                // صداقت به‌جای عدد جعلی: بررسی انجام نشد و در Issues اعلام می‌شود (SelfIntersections==0 نیست).
                selfIntersections = -1;
                issues.Add(
                    $"بررسی تقاطع خودی برای مش‌های بزرگ‌تر از {MaxSelfIntersectionFaceCount:N0} وجه به دلیل هزینهٔ محاسباتی انجام نشد؛ " +
                    "بدون-تقاطع‌بودن این مش تأیید نشده است.");
            }
            else
            {
                selfIntersections = CountSelfIntersections(mesh);
                if (selfIntersections > 0)
                    issues.Add($"{selfIntersections} مورد تقاطع خودی مشکوک پیدا شد (بررسی هیوریستیکی).");
            }
        }

        return new MeshValidationResult(watertight, orientationOk, zeroArea > 0, selfIntersections, issues);
    }

    /// <summary>تحلیل یال‌ها: مرزی، نامانیفولد و وجه‌های بی‌مساحت.</summary>
    private static (int TotalEdges, int BoundaryEdges, int BadEdges, int ZeroAreaTriangles) AnalyzeEdges(Mesh mesh)
    {
        // کلید یال ناموجه: (min,max) اندیس رأس‌ها؛ مقدار: جهت‌های مشاهده‌شده.
        var edges = new Dictionary<(int, int), int>(mesh.Triangles.Length * 3);
        var zeroArea = 0;

        foreach (var t in mesh.Triangles)
        {
            if (IsDegenerate(mesh, t)) zeroArea++;

            AddDirectedEdge(edges, t.A, t.B);
            AddDirectedEdge(edges, t.B, t.C);
            AddDirectedEdge(edges, t.C, t.A);
        }

        var boundary = 0;
        var bad = 0;
        foreach (var kvp in edges)
        {
            // کدگذاری: بیت ۰ (۱) = یک‌بار جهت مستقیم؛ بیت ۱ (۲) = یک‌بار جهت معکوس.
            // ۳ = یک‌بار هر جهت (سالم)؛ ۱ = مرزی؛ سایر مقادیر = نامانیفولد یا جهت ناسازگار.
            if (kvp.Value == 1) boundary++;
            else if (kvp.Value != 3) bad++;
        }

        return (edges.Count, boundary, bad, zeroArea);
    }

    /// <summary>یال جهت‌دار را به شمارندهٔ یال ناموجه می‌افزاید: مستقیم = +۱، معکوس = +۲.</summary>
    private static void AddDirectedEdge(Dictionary<(int, int), int> edges, int from, int to)
    {
        var key = from < to ? (from, to) : (to, from);
        var forward = from < to;
        // مستقیم = +۱ (بیت ۰)؛ معکوس = +۲ (بیت ۱).
        edges[key] = edges.TryGetValue(key, out var current) ? current + (forward ? 1 : 2) : (forward ? 1 : 2);
    }

    private static bool IsDegenerate(Mesh mesh, MeshTriangle t)
    {
        var a = mesh.Vertices[t.A].ToVector();
        var b = mesh.Vertices[t.B].ToVector();
        var c = mesh.Vertices[t.C].ToVector();
        return Vector3.Cross(b - a, c - a).LengthSquared() < 1e-18;
    }

    /// <summary>
    /// شمارش تقاطع خودی با شبکهٔ فضایی — تقاطع «درون‌متنی» یال‌های غیرهمسایه با وجه‌ها.
    /// </summary>
    /// <remarks>
    /// فقط زوج‌های وجه/یال که رأس مشترک ندارند بررسی می‌شوند (یال‌های همسایه همیشه «تقاطع» دارند).
    /// دقت float کافی است چونا نتیجهٔ گزارشی/هشداری است نه دادهٔ هندسی خروجی.
    /// </remarks>
    private static int CountSelfIntersections(Mesh mesh)
    {
        const float cellSize = 2.0f; // میلی‌متر — تعادل دقت/سرعت برای قطعات ریز تا متوسط.
        var grid = new Dictionary<(int, int, int), List<int>>();

        for (var tri = 0; tri < mesh.Triangles.Length; tri++)
        {
            var (minX, minY, minZ, maxX, maxY, maxZ) = TriangleBounds(mesh, tri);
            for (var x = (int)Math.Floor(minX / cellSize); x <= (int)Math.Floor(maxX / cellSize); x++)
            for (var y = (int)Math.Floor(minY / cellSize); y <= (int)Math.Floor(maxY / cellSize); y++)
            for (var z = (int)Math.Floor(minZ / cellSize); z <= (int)Math.Floor(maxZ / cellSize); z++)
            {
                var key = (x, y, z);
                if (!grid.TryGetValue(key, out var list))
                {
                    list = [];
                    grid[key] = list;
                }
                list.Add(tri);
            }
        }

        var checkedPairs = new HashSet<(int, int)>();
        var intersections = 0;

        foreach (var cell in grid.Values)
        {
            // در هر سلول، همهٔ جفت‌های وجه بررسی می‌شوند؛ جفت‌های تکراری با مجموعهٔ دیده‌شده حذف می‌شوند.
            for (var i = 0; i < cell.Count; i++)
            for (var j = i + 1; j < cell.Count; j++)
            {
                var a = Math.Min(cell[i], cell[j]);
                var b = Math.Max(cell[i], cell[j]);
                if (!checkedPairs.Add((a, b))) continue;

                if (TrianglesIntersect(mesh, a, b)) intersections++;
            }
        }

        return intersections;
    }

    private static (float, float, float, float, float, float) TriangleBounds(Mesh mesh, int index)
    {
        var t = mesh.Triangles[index];
        var a = mesh.Vertices[t.A];
        var b = mesh.Vertices[t.B];
        var c = mesh.Vertices[t.C];
        return (
            (float)Math.Min(a.X, Math.Min(b.X, c.X)),
            (float)Math.Min(a.Y, Math.Min(b.Y, c.Y)),
            (float)Math.Min(a.Z, Math.Min(b.Z, c.Z)),
            (float)Math.Max(a.X, Math.Max(b.X, c.X)),
            (float)Math.Max(a.Y, Math.Max(b.Y, c.Y)),
            (float)Math.Max(a.Z, Math.Max(b.Z, c.Z)));
    }

    /// <summary>آزمون برخورد دو مثلث با رأس‌های مشترک مجاز (همسایه‌ها) — الگوریتم مبسوط Seg-Tri.</summary>
    private static bool TrianglesIntersect(Mesh mesh, int t1Index, int t2Index)
    {
        var t1 = mesh.Triangles[t1Index];
        var t2 = mesh.Triangles[t2Index];

        // وجه‌های همسایه که رأس مشترک دارند در این بررسی «تقاطع» حساب نمی‌شوند.
        if (SharesVertex(t1, t2)) return false;

        var p1 = mesh.Vertices[t1.A].ToVector();
        var q1 = mesh.Vertices[t1.B].ToVector();
        var r1 = mesh.Vertices[t1.C].ToVector();
        var p2 = mesh.Vertices[t2.A].ToVector();
        var q2 = mesh.Vertices[t2.B].ToVector();
        var r2 = mesh.Vertices[t2.C].ToVector();

        // آزمون کامل: هر ۳ یال مثلث اول با صفحهٔ دومی و بالعکس؛ سپس تقاطع پاره‌خط-مثلث.
        return SegmentIntersectsTriangle(p1, q1, p2, q2, r2) ||
               SegmentIntersectsTriangle(q1, r1, p2, q2, r2) ||
               SegmentIntersectsTriangle(r1, p1, p2, q2, r2) ||
               SegmentIntersectsTriangle(p2, q2, p1, q1, r1) ||
               SegmentIntersectsTriangle(q2, r2, p1, q1, r1) ||
               SegmentIntersectsTriangle(r2, p2, p1, q1, r1);
    }

    private static bool SharesVertex(MeshTriangle a, MeshTriangle b) =>
        (a.A == b.A || a.A == b.B || a.A == b.C) ||
        (a.B == b.A || a.B == b.B || a.B == b.C) ||
        (a.C == b.A || a.C == b.B || a.C == b.C);

    /// <summary>تقاطع پاره‌خط با مثلث — با آزمون صفحه و باریمتریک؛ تلرانس float.</summary>
    private static bool SegmentIntersectsTriangle(Vector3 s0, Vector3 s1, Vector3 a, Vector3 b, Vector3 c)
    {
        var normal = Vector3.Cross(b - a, c - a);
        var d0 = Vector3.Dot(normal, s0 - a);
        var d1 = Vector3.Dot(normal, s1 - a);

        // دو سر در یک سمت صفحه: تقاطع نیست (تلرانس کوچک برای مماس‌ها).
        if (d0 > 1e-6f && d1 > 1e-6f) return false;
        if (d0 < -1e-6f && d1 < -1e-6f) return false;
        if (MathF.Abs(d0) < 1e-9f && MathF.Abs(d1) < 1e-9f) return false; // هم‌صفحه: از آزمون یال-یال می‌گذریم (هیوریستیکی).

        var t = d0 / (d0 - d1);
        if (t < -1e-6f || t > 1f + 1e-6f) return false;

        var point = s0 + t * (s1 - s0);

        // آزمون باریمتریک داخل مثلث.
        var u = b - a;
        var v = c - a;
        var w = point - a;
        var uu = Vector3.Dot(u, u);
        var vv = Vector3.Dot(v, v);
        var uv = Vector3.Dot(u, v);
        var wu = Vector3.Dot(w, u);
        var wv = Vector3.Dot(w, v);
        var denom = uu * vv - uv * uv;
        if (MathF.Abs(denom) < 1e-12f) return false;

        var s = (vv * wu - uv * wv) / denom;
        var r = (uu * wv - uv * wu) / denom;
        return s >= -1e-5f && r >= -1e-5f && s + r <= 1f + 1e-5f;
    }
}

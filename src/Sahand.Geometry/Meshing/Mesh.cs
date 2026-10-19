using System.Numerics;

namespace Sahand.Geometry.Meshing;

/// <summary>
/// رأس مش با مختصات دکارتی برحسب میلی‌متر.
/// </summary>
/// <param name="X">مختصات X.</param>
/// <param name="Y">مختصات Y.</param>
/// <param name="Z">مختصات Z — محور طولی قطعات.</param>
public readonly record struct MeshVertex(double X, double Y, double Z)
{
    /// <summary>تبدیل به بردار دات‌کِت.</summary>
    public Vector3 ToVector() => new((float)X, (float)Y, (float)Z);
}

/// <summary>
/// وجه مثلثی با قرارداد winding: نرمال با قانون دست راست به «بیرون» جسم اشاره می‌کند.
/// </summary>
/// <param name="A">اندیس رأس اول.</param>
/// <param name="B">اندیس رأس دوم.</param>
/// <param name="C">اندیس رأس سوم.</param>
public readonly record struct MeshTriangle(int A, int B, int C);

/// <summary>
/// مش مثلثی قابل انباشت — واحد میلی‌متر؛ محور طولی قطعه Z.
/// </summary>
/// <remarks>
/// ساخت با انباشت: سازنده‌ها رأس/وجه اضافه می‌کنند و در پایان <see cref="Build"/> نسخهٔ
/// تغییرناپذیر می‌سازد. نرمال‌ها از winding مثلث محاسبه می‌شوند؛ برای مش بستهٔ معتبر،
/// جهت winding مرتب‌کنندهٔ رو به بیرون است (قرارداد سند ۷-۵).
/// </remarks>
public sealed class MeshBuilder
{
    private readonly List<MeshVertex> _vertices = [];
    private readonly List<MeshTriangle> _triangles = [];

    /// <summary>تعداد رأس‌های ثبت‌شده.</summary>
    public int VertexCount => _vertices.Count;

    /// <summary>تعداد وجه‌های ثبت‌شده.</summary>
    public int TriangleCount => _triangles.Count;

    /// <summary>رأس جدید می‌افزاید و اندیسش را برمی‌گرداند.</summary>
    public int AddVertex(double x, double y, double z)
    {
        _vertices.Add(new MeshVertex(x, y, z));
        return _vertices.Count - 1;
    }

    /// <summary>رأس جدید از مختصات استوانه‌ای می‌افزاید (زاویه برحسب رادیان).</summary>
    public int AddCylindrical(double radiusMm, double angleRad, double zMm) =>
        AddVertex(radiusMm * Math.Cos(angleRad), radiusMm * Math.Sin(angleRad), zMm);

    /// <summary>
    /// وجه با winding رو به بیرون می‌افزاید؛ ترتیب (a,b,c) پادساعتگرد از بیرون دیده می‌شود.
    /// </summary>
    public void AddTriangle(int a, int b, int c) => _triangles.Add(new MeshTriangle(a, b, c));

    /// <summary>
    /// چهارگوش (a,b,c,d) به دو مثلث می‌شکند؛ ترتیب رئوس باید رو به بیرون باشد.
    /// </summary>
    /// <remarks>قطری که انتخاب می‌شود (a-c) برای وجه‌های نزدیک مسطح پیچش قابل‌چشم‌پوشی دارد.</remarks>
    public void AddQuad(int a, int b, int c, int d)
    {
        AddTriangle(a, b, c);
        AddTriangle(a, c, d);
    }

    /// <summary>مشِ دیگری را با جابه‌جایی انباشت می‌کند (برای مجموعهٔ جفت).</summary>
    public void Append(Mesh mesh, double dxMm, double dyMm, double dzMm)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var offset = _vertices.Count;
        for (var i = 0; i < mesh.Vertices.Length; i++)
        {
            var v = mesh.Vertices[i];
            _vertices.Add(new MeshVertex(v.X + dxMm, v.Y + dyMm, v.Z + dzMm));
        }

        foreach (var t in mesh.Triangles)
            _triangles.Add(new MeshTriangle(t.A + offset, t.B + offset, t.C + offset));
    }

    /// <summary>نسخهٔ تغییرناپذیر نهایی مش را می‌سازد.</summary>
    public Mesh Build() => new(
        _vertices.ToArray(),
        _triangles.ToArray());
}

/// <summary>
/// مش تغییرناپذیر نهایی — ورودی خروجی‌گیرها، اعتبارسنج و پیش‌نمایش.
/// </summary>
/// <param name="Vertices">رأس‌ها (میلی‌متر).</param>
/// <param name="Triangles">وجه‌ها با winding رو به بیرون.</param>
public sealed record Mesh(MeshVertex[] Vertices, MeshTriangle[] Triangles)
{
    /// <summary>
    /// حجم امضادار جسم را با قضیهٔ گردش (divergence theorem) محاسبه می‌کند.
    /// </summary>
    /// <remarks>
    /// برای مش بستهٔ با winding بیرونی مثبت است؛ علامت منفی یعنی winding معکوس است.
    /// این عدد «حجم هندسی» است و به‌تنهایی جرم یا استحکام را تضمین نمی‌کند.
    /// </remarks>
    public double SignedVolumeMm3()
    {
        double sum = 0;
        foreach (var t in Triangles)
        {
            var a = Vertices[t.A];
            var b = Vertices[t.B];
            var c = Vertices[t.C];
            // عدد ثابت ۶ از فرمول V = Σ det(a,b,c)/۶ برای هر وجه.
            sum += a.X * (b.Y * c.Z - b.Z * c.Y)
                 - a.Y * (b.X * c.Z - b.Z * c.X)
                 + a.Z * (b.X * c.Y - b.Y * c.X);
        }
        return sum / 6.0;
    }

    /// <summary>جعبهٔ محیطی مش (حداقل/حداکثر هر محور).</summary>
    public (double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ) Bounds()
    {
        if (Vertices.Length == 0)
            throw new InvalidOperationException("مش بدون رأس، جعبهٔ محیطی ندارد.");

        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        foreach (var v in Vertices)
        {
            minX = Math.Min(minX, v.X); minY = Math.Min(minY, v.Y); minZ = Math.Min(minZ, v.Z);
            maxX = Math.Max(maxX, v.X); maxY = Math.Max(maxY, v.Y); maxZ = Math.Max(maxZ, v.Z);
        }
        return (minX, minY, minZ, maxX, maxY, maxZ);
    }

    /// <summary>
    /// مساحت کل سطح مش (مجموع مساحت وجه‌ها) — برای گزارش کیفیت و برآورد مش.
    /// </summary>
    public double TotalSurfaceAreaMm2()
    {
        double sum = 0;
        foreach (var t in Triangles)
        {
            var a = Vertices[t.A];
            var b = Vertices[t.B];
            var c = Vertices[t.C];
            var ab = new Vector3((float)(b.X - a.X), (float)(b.Y - a.Y), (float)(b.Z - a.Z));
            var ac = new Vector3((float)(c.X - a.X), (float)(c.Y - a.Y), (float)(c.Z - a.Z));
            sum += Vector3.Cross(ab, ac).Length() / 2.0;
        }
        return sum;
    }
}

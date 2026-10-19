namespace Sahand.Geometry.Meshing;

/// <summary>
/// سازندهٔ سطوح «تابع شعاعی» حول محور Z — زیرساخت مشترک رزوه، سرها، مهره و واشر.
/// </summary>
/// <remarks>
/// قرارداد هندسی (سند ۷-۵): R(θ,z) ≥ ۰ و ۲π-متناوب؛ محور قطعه Z؛ zLevels باید صعودی باشد.
/// کلید watertight بودن: درپوش‌ها و حلقه‌های اتصال باید «همان اندیس رأس‌های» سطح را
/// به اشتراک بگذارند؛ به همین دلیل سازنده‌های سطح، رأس‌های حلقهٔ هر تراز را برمی‌گردانند
/// و درپوش‌های fan/ring روی همان اندیس‌ها ساخته می‌شوند.
/// پیوند دورانی: رأس زاویهٔ ۲π با رأس ۰ یکی است (توابع شعاعی ۲π-متناوب‌اند).
/// </remarks>
public static class RadialSurface
{
    /// <summary>
    /// سطح شعاعی رو به بیرون می‌سازد و رأس‌های حلقهٔ هر تراز z را برمی‌گرداند.
    /// </summary>
    /// <param name="mesh">مش مقصد.</param>
    /// <param name="grid">شبکهٔ زاویه‌ای.</param>
    /// <param name="radiusAt">تابع شعاع (θ رادیان، z میلی‌متر).</param>
    /// <param name="zLevels">ترازهای z اکیداً صعودی (دست‌کم ۲).</param>
    /// <param name="capFirst">درپوش بادبزکی در تراز اول (نرمال −Z؛ ماده در z بزرگ‌تر).</param>
    /// <param name="capLast">درپوش بادبزکی در تراز آخر (نرمال +Z؛ ماده در z کوچک‌تر).</param>
    /// <returns>آرایهٔ اندیس رأس‌ها به‌ازای هر تراز z (طول = تعداد زاویای منحصربه‌فرد).</returns>
    public static int[][] AddOutwardSurface(
        MeshBuilder mesh,
        AngularGrid grid,
        Func<double, double, double> radiusAt,
        IReadOnlyList<double> zLevels,
        bool capFirst = false,
        bool capLast = false)
    {
        var rings = BuildRings(mesh, grid, radiusAt, zLevels);
        var uniqueCount = rings[0].Length;

        for (var zi = 0; zi + 1 < zLevels.Count; zi++)
        {
            for (var ai = 0; ai < uniqueCount; ai++)
            {
                var aNext = (ai + 1) % uniqueCount; // پیوند دورانی.
                // الگوی بیرونی: (θᵢ,zⱼ) → (θᵢ₊₁,zⱼ) → (θᵢ₊₁,zⱼ₊₁) → (θᵢ,zⱼ₊₁) با z صعودی.
                mesh.AddQuad(rings[zi][ai], rings[zi][aNext], rings[zi + 1][aNext], rings[zi + 1][ai]);
            }
        }

        if (capFirst) AddFanCap(mesh, rings[0], zLevels[0], solidBelow: false);
        if (capLast) AddFanCap(mesh, rings[^1], zLevels[^1], solidBelow: true);
        return rings;
    }

    /// <summary>
    /// سطح شعاعی رو به داخل (حفره) می‌سازد و حلقه‌ها را برمی‌گرداند.
    /// </summary>
    /// <param name="mesh">مش مقصد.</param>
    /// <param name="grid">شبکهٔ زاویه‌ای.</param>
    /// <param name="radiusAt">تابع شعاع (θ رادیان، z میلی‌متر).</param>
    /// <param name="zLevels">ترازهای z اکیداً صعودی (دست‌کم ۲).</param>
    /// <param name="capFirst">درپوش تراز اول (نرمال +Z؛ ماده در z کوچک‌تر — کفِ حفرهٔ از پایین).</param>
    /// <param name="capLast">درپوش تراز آخر (نرمال −Z؛ ماده در z بزرگ‌تر — کفِ حفرهٔ از بالا).</param>
    public static int[][] AddInwardSurface(
        MeshBuilder mesh,
        AngularGrid grid,
        Func<double, double, double> radiusAt,
        IReadOnlyList<double> zLevels,
        bool capFirst = false,
        bool capLast = false)
    {
        var rings = BuildRings(mesh, grid, radiusAt, zLevels);
        var uniqueCount = rings[0].Length;

        for (var zi = 0; zi + 1 < zLevels.Count; zi++)
        {
            for (var ai = 0; ai < uniqueCount; ai++)
            {
                var aNext = (ai + 1) % uniqueCount;
                // معکوسِ الگوی بیرونی: نرمال به سمت محور (−r̂).
                mesh.AddQuad(rings[zi][ai], rings[zi + 1][ai], rings[zi + 1][aNext], rings[zi][aNext]);
            }
        }

        if (capFirst) AddFanCap(mesh, rings[0], zLevels[0], solidBelow: true);
        if (capLast) AddFanCap(mesh, rings[^1], zLevels[^1], solidBelow: false);
        return rings;
    }

    /// <summary>
    /// درپوش بادبزکی از محور روی حلقهٔ موجود — با اشتراک اندیس رأس‌ها (الزام watertight).
    /// </summary>
    /// <param name="mesh">مش مقصد.</param>
    /// <param name="ring">حلقهٔ مرز درپوش (اندیس رأس‌ها به‌ترتیب زاویه).</param>
    /// <param name="zMm">تراز درپوش (فقط برای ایجاد رأس مرکز).</param>
    /// <param name="solidBelow">true = نرمال +Z (ماده زیر تراز).</param>
    public static void AddFanCap(MeshBuilder mesh, int[] ring, double zMm, bool solidBelow)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(ring);
        if (ring.Length < 3) throw new ArgumentException("حلقهٔ درپوش دست‌کم ۳ رأس لازم دارد.");

        var center = mesh.AddVertex(0, 0, zMm);
        for (var i = 0; i < ring.Length; i++)
        {
            var next = (i + 1) % ring.Length;
            if (solidBelow)
                mesh.AddTriangle(center, ring[i], ring[next]);
            else
                mesh.AddTriangle(center, ring[next], ring[i]);
        }
    }

    /// <summary>
    /// درپوش حلقه‌ای بین دو حلقهٔ موجود در یک تراز — مثل سطح اتکای مهره یا زیر سر پیچ.
    /// </summary>
    /// <param name="mesh">مش مقصد.</param>
    /// <param name="innerRing">حلقهٔ داخلی (اندیس رأس‌های مشترک سطح داخلی).</param>
    /// <param name="outerRing">حلقهٔ خارجی (اندیس رأس‌های مشترک سطح خارجی).</param>
    /// <param name="solidBelow">true = نرمال +Z (ماده زیر تراز؛ مثل رویهٔ اتکای سر پیچ).</param>
    public static void AddRingCap(MeshBuilder mesh, int[] innerRing, int[] outerRing, bool solidBelow)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(innerRing);
        ArgumentNullException.ThrowIfNull(outerRing);
        if (innerRing.Length != outerRing.Length)
            throw new ArgumentException("دو حلقهٔ درپوش حلقه‌ای باید هم‌اندازه (هم‌شبکه) باشند.");

        for (var i = 0; i < innerRing.Length; i++)
        {
            var next = (i + 1) % innerRing.Length;
            if (solidBelow)
                mesh.AddQuad(outerRing[i], outerRing[next], innerRing[next], innerRing[i]);
            else
                mesh.AddQuad(outerRing[i], innerRing[i], innerRing[next], outerRing[next]);
        }
    }

    /// <summary>حلقه‌های رأس را برای همهٔ ترازها می‌سازد (اشتراک کامل بین ترازها).</summary>
    private static int[][] BuildRings(
        MeshBuilder mesh,
        AngularGrid grid,
        Func<double, double, double> radiusAt,
        IReadOnlyList<double> zLevels)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(radiusAt);
        ArgumentNullException.ThrowIfNull(zLevels);
        if (zLevels.Count < 2)
            throw new ArgumentException("دست‌کم دو تراز z لازم است.", nameof(zLevels));

        for (var i = 1; i < zLevels.Count; i++)
        {
            if (zLevels[i] <= zLevels[i - 1])
                throw new ArgumentException("ترازهای z باید اکیداً صعودی باشند.", nameof(zLevels));
        }

        var uniqueCount = grid.SegmentCount;
        var rings = new int[zLevels.Count][];

        for (var zi = 0; zi < zLevels.Count; zi++)
        {
            rings[zi] = new int[uniqueCount];
            for (var ai = 0; ai < uniqueCount; ai++)
            {
                var theta = grid[ai];
                rings[zi][ai] = mesh.AddCylindrical(radiusAt(theta, zLevels[zi]), theta, zLevels[zi]);
            }
        }

        return rings;
    }
}

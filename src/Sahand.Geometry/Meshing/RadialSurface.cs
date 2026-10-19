namespace Sahand.Geometry.Meshing;

/// <summary>
/// سازندهٔ سطوح «تابع شعاعی» حول محور Z — زیرساخت مشترک رزوه، سرها، مهره و واشر.
/// </summary>
/// <remarks>
/// قرارداد هندسی (سند ۷-۵): R(θ,z) ≥ ۰ و ۲π-متناوب؛ محور قطعه Z؛ z به سمت نوک افزایش می‌یابد.
/// شبکهٔ زاویه‌ای <see cref="AngularGrid"/> با «زوایای ویژه» به فراخوان هم‌راستا می‌شود تا
/// یال‌های مشترک ساختارها دقیقاً منطبق و مش مانیفولد بماند.
/// پیوند دورانی: رأس زاویهٔ ۲π با رأس ۰ یکی است (مقدار توابع شعاعی در ۲π برابر مقدار در ۰ است
/// چون دورهٔ تناوب پیشروی رزوه/شکل‌های دورانی دقیقاً کامل است).
/// </remarks>
public static class RadialSurface
{
    /// <summary>
    /// سطح شعاعی رو به بیرون را روی شبکهٔ (θ × z) می‌سازد — نرمال‌ها از محور دور می‌شوند.
    /// </summary>
    /// <param name="mesh">مش مقصد.</param>
    /// <param name="grid">شبکهٔ زاویه‌ای (شامل ۲π؛ رأس ۲π با ۰ یکی می‌شود).</param>
    /// <param name="radiusAt">تابع شعاع (θ رادیان، z میلی‌متر) → شعاع میلی‌متر.</param>
    /// <param name="zLevels">سطوح z صعودی (دست‌کم ۲).</param>
    public static void AddOutwardSurface(
        MeshBuilder mesh,
        AngularGrid grid,
        Func<double, double, double> radiusAt,
        IReadOnlyList<double> zLevels)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(radiusAt);
        ArgumentNullException.ThrowIfNull(zLevels);
        if (zLevels.Count < 2) throw new ArgumentException("دست‌کم دو سطح z لازم است.", nameof(zLevels));

        // رأس‌ها برای همهٔ (θ منحصربه‌فرد × z). ستون ۲π با ستون ۰ اشتراکی است؛ برای
        // سادگی پیاده‌سازی، رأس ۲π جدا ساخته و در پایان «جوش» داده می‌شود: در عوض،
        // بازهٔ آخر با رأس ۲π تولید و سپس در مپ ادغام می‌شود.
        var uniqueCount = grid.SegmentCount; // تعداد رأس منحصربه‌فرد = تعداد بازه.
        var vertexIndex = new int[zLevels.Count * uniqueCount];

        for (var zi = 0; zi < zLevels.Count; zi++)
        {
            for (var ai = 0; ai < uniqueCount; ai++)
            {
                var theta = grid[ai];
                var radius = radiusAt(theta, zLevels[zi]);
                vertexIndex[zi * uniqueCount + ai] = mesh.AddCylindrical(radius, theta, zLevels[zi]);
            }
        }

        // چهارگوش‌های سطح؛ بازهٔ آخر (θ آخر → ۲π≡θ₀) با ستون ۰ جوش می‌شود.
        for (var zi = 0; zi + 1 < zLevels.Count; zi++)
        {
            for (var ai = 0; ai < uniqueCount; ai++)
            {
                var aNext = (ai + 1) % uniqueCount; // پیوند دورانی.
                var v00 = vertexIndex[zi * uniqueCount + ai];
                var v01 = vertexIndex[(zi + 1) * uniqueCount + ai];
                var v11 = vertexIndex[(zi + 1) * uniqueCount + aNext];
                var v10 = vertexIndex[zi * uniqueCount + aNext];

                // ترتیب (v00, v10, v11, v01): نرمال رو به بیرون (r̂+) — با آزمون ضرب خارجی
                // برداری راستی‌آزمایی شده است: (a_i→a_next)×(a_i→b_i) ∝ +r̂ برای افزایش θ.
                mesh.AddQuad(v00, v10, v11, v01);
            }
        }
    }

    /// <summary>
    /// سطح شعاعی رو به داخل (مثل حفرهٔ مهره) — winding معکوس تا نرمال به سمت محور باشد.
    /// </summary>
    public static void AddInwardSurface(
        MeshBuilder mesh,
        AngularGrid grid,
        Func<double, double, double> radiusAt,
        IReadOnlyList<double> zLevels)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(radiusAt);
        ArgumentNullException.ThrowIfNull(zLevels);
        if (zLevels.Count < 2) throw new ArgumentException("دست‌کم دو سطح z لازم است.", nameof(zLevels));

        var uniqueCount = grid.SegmentCount;
        var vertexIndex = new int[zLevels.Count * uniqueCount];

        for (var zi = 0; zi < zLevels.Count; zi++)
        {
            for (var ai = 0; ai < uniqueCount; ai++)
            {
                var theta = grid[ai];
                vertexIndex[zi * uniqueCount + ai] =
                    mesh.AddCylindrical(radiusAt(theta, zLevels[zi]), theta, zLevels[zi]);
            }
        }

        for (var zi = 0; zi + 1 < zLevels.Count; zi++)
        {
            for (var ai = 0; ai < uniqueCount; ai++)
            {
                var aNext = (ai + 1) % uniqueCount;
                var v00 = vertexIndex[zi * uniqueCount + ai];
                var v01 = vertexIndex[(zi + 1) * uniqueCount + ai];
                var v11 = vertexIndex[(zi + 1) * uniqueCount + aNext];
                var v10 = vertexIndex[zi * uniqueCount + aNext];

                // ترتیب معکوس سطح بیرونی: نرمال رو به محور (−r̂) — قرارداد مش بیرونیِ جسم (حفره).
                mesh.AddQuad(v00, v01, v11, v10);
            }
        }
    }

    /// <summary>
    /// درپوش تختِ «بادبزکی از محور» در سطح z داده‌شده — مرز از تابع شعاع خوانده می‌شود.
    /// </summary>
    /// <param name="mesh">مش مقصد.</param>
    /// <param name="grid">شبکهٔ زاویه‌ای مرز.</param>
    /// <param name="radiusAt">شعاع مرز در هر θ.</param>
    /// <param name="zMm">تراز درپوش.</param>
    /// <param name="solidBelow">اگر جسم در z کمتر باشد، نرمال باید +Z باشد (درپوش بالایی).</param>
    /// <remarks>مرز باید نسبت به محور ستاره‌ای باشد؛ برای پروفیل‌های رزوه/شش‌گوش برقرار است.</remarks>
    public static void AddDiscCap(
        MeshBuilder mesh,
        AngularGrid grid,
        Func<double, double> radiusAt,
        double zMm,
        bool solidBelow)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(radiusAt);

        var uniqueCount = grid.SegmentCount;
        var center = mesh.AddVertex(0, 0, zMm);
        var ring = new int[uniqueCount];
        for (var ai = 0; ai < uniqueCount; ai++)
            ring[ai] = mesh.AddCylindrical(radiusAt(grid[ai]), grid[ai], zMm);

        for (var ai = 0; ai < uniqueCount; ai++)
        {
            var aNext = (ai + 1) % uniqueCount;
            // درپوش بالایی (نرمال +Z): (مرکز، θᵢ، θᵢ₊₁)؛ پایینی معکوس.
            if (solidBelow)
                mesh.AddTriangle(center, ring[ai], ring[aNext]);
            else
                mesh.AddTriangle(center, ring[aNext], ring[ai]);
        }
    }

    /// <summary>
    /// درپوش حلقه‌ای بین دو تابع شعاعی در یک تراز z — مثل سطح اتکای مهره یا زیر سر پیچ.
    /// </summary>
    /// <param name="mesh">مش مقصد.</param>
    /// <param name="grid">شبکهٔ زاویه‌ای مشترک دو مرز.</param>
    /// <param name="innerRadiusAt">شعاع داخلی مرز.</param>
    /// <param name="outerRadiusAt">شعاع خارجی مرز.</param>
    /// <param name="zMm">تراز درپوش.</param>
    /// <param name="solidBelow">نرمال +Z (درپوش بالایی) اگر جسم زیر تراز باشد.</param>
    public static void AddRingCap(
        MeshBuilder mesh,
        AngularGrid grid,
        Func<double, double> innerRadiusAt,
        Func<double, double> outerRadiusAt,
        double zMm,
        bool solidBelow)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(innerRadiusAt);
        ArgumentNullException.ThrowIfNull(outerRadiusAt);

        var uniqueCount = grid.SegmentCount;
        var inner = new int[uniqueCount];
        var outer = new int[uniqueCount];
        for (var ai = 0; ai < uniqueCount; ai++)
        {
            inner[ai] = mesh.AddCylindrical(innerRadiusAt(grid[ai]), grid[ai], zMm);
            outer[ai] = mesh.AddCylindrical(outerRadiusAt(grid[ai]), grid[ai], zMm);
        }

        for (var ai = 0; ai < uniqueCount; ai++)
        {
            var aNext = (ai + 1) % uniqueCount;
            // ترتیب رو به +Z: (خارجیᵢ، خارجیᵢ₊₁، داخلیᵢ₊₁، داخلیᵢ)؛ برای −Z معکوس.
            if (solidBelow)
                mesh.AddQuad(outer[ai], outer[aNext], inner[aNext], inner[ai]);
            else
                mesh.AddQuad(outer[ai], inner[ai], inner[aNext], outer[aNext]);
        }
    }
}

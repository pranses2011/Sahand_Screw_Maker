namespace Sahand.Geometry.Meshing;

/// <summary>
/// نقطهٔ پروفیل دوبعدی در صفحهٔ (شعاع، محور) — واحد میلی‌متر.
/// </summary>
/// <param name="RadiusMm">شعاع از محور Z (نامنفی).</param>
/// <param name="ZMm">ارتفاع محوری.</param>
public readonly record struct ProfilePoint(double RadiusMm, double ZMm);

/// <summary>
/// سازندهٔ جامد دورانی از پروفیل بستهٔ دوبعدی — برای واشر، سرهای استوانه‌ای/نیم‌گرد/خزینه.
/// </summary>
/// <remarks>
/// قرارداد: پروفیل باید بسته و ساده باشد (بدون خود-تقاطع) و در نیم‌صفحهٔ r ≥ ۰.
/// یال‌های پروفیل هنگام دوران به سطح تبدیل می‌شوند؛ یال روی محور (r=۰ دو سر) به‌طور
/// خودکار مثلث‌های درپوشِ بادبزکی می‌سازد، پس پروفیلِ جسمِ بدون حفره هم پشتیبانی می‌شود.
/// مثلث‌های تقریباً‌هم‌صفحه (نیم‌رخ‌های چندخطی مثل گنبد) رو به بیرون تولید می‌شوند.
/// </remarks>
public static class RevolvedSolid
{
    /// <summary>
    /// پروفیل بسته را حول محور Z با شبکهٔ زاویه‌ای می‌چرخاند و جامد بسته می‌سازد.
    /// </summary>
    /// <param name="mesh">مش مقصد.</param>
    /// <param name="profile">رئوس پروفیل به‌ترتیب (جهت دلخواه؛ جهت‌دهی خودکار اصلاح می‌شود).</param>
    /// <param name="grid">شبکهٔ زاویه‌ای.</param>
    public static void AddRevolved(MeshBuilder mesh, IReadOnlyList<ProfilePoint> profile, AngularGrid grid)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(grid);
        if (profile.Count < 3)
            throw new ArgumentException("پروفیل دورانی دست‌کم ۳ نقطه لازم دارد.", nameof(profile));

        // جهت پروفیل باید «پادساعتگرد در صفحهٔ (r,z)» باشد تا نرمال‌ها رو به بیرون جسم شوند.
        var ordered = EnsureCounterClockwise(profile);

        var ringCount = grid.SegmentCount;
        var rings = new int[ordered.Count][];
        for (var i = 0; i < ordered.Count; i++)
        {
            rings[i] = new int[ringCount];
            for (var a = 0; a < ringCount; a++)
            {
                rings[i][a] = mesh.AddCylindrical(ordered[i].RadiusMm, grid[a], ordered[i].ZMm);
            }
        }

        // هر یال پروفیل بین رأس i و i+1 دوران می‌کند و یک نوار سطح می‌سازد.
        for (var i = 0; i < ordered.Count; i++)
        {
            var j = (i + 1) % ordered.Count;
            AddSweptEdge(mesh, grid, rings[i], rings[j], ordered[i], ordered[j]);
        }
    }

    /// <summary>
    /// یک یال پروفیل را می‌چرخاند — با مدیریت خودکار یال روی محور (درپوش بادبزکی).
    /// </summary>
    private static void AddSweptEdge(
        MeshBuilder mesh,
        AngularGrid grid,
        int[] ringA,
        int[] ringB,
        ProfilePoint a,
        ProfilePoint b)
    {
        var ringCount = grid.SegmentCount;

        for (var seg = 0; seg < ringCount; seg++)
        {
            var next = (seg + 1) % ringCount;

            if (a.RadiusMm <= 1e-12 && b.RadiusMm <= 1e-12)
            {
                continue; // یال کاملاً روی محور: سطح بدون مساحت؛ درپوش از یال‌های مجاور ساخته می‌شود.
            }

            if (a.RadiusMm <= 1e-12)
            {
                // یک سر روی محور: هر بخش فقط یک مثلث (درپوش بادبزکی) با winding بیرونی.
                mesh.AddTriangle(ringA[seg], ringB[next], ringB[seg]);
            }
            else if (b.RadiusMm <= 1e-12)
            {
                mesh.AddTriangle(ringA[seg], ringA[next], ringB[seg]);
            }
            else
            {
                // نوار معمولی: چهارگوش رو به بیرون. جهت (a→b) پادساعتگرد در (r,z) تضمین شده است.
                mesh.AddQuad(ringA[seg], ringA[next], ringB[next], ringB[seg]);
            }
        }
    }

    /// <summary>
    /// جهت رئوس پروفیل را به پادساعتگرد در صفحهٔ (r,z) نرمال می‌کند.
    /// </summary>
    /// <remarks>
    /// قرارداد جهت‌دهی: با مساحت امضادار در صفحهٔ (r به‌عنوان x و z به‌عنوان y) تشخیص می‌دهیم؛
    /// پادساعتگرد (مساحت مثبت) تضمین می‌کند نرمال نوارهای جاروب‌شده رو به بیرون جسم باشد.
    /// </remarks>
    private static IReadOnlyList<ProfilePoint> EnsureCounterClockwise(IReadOnlyList<ProfilePoint> profile)
    {
        double area = 0;
        for (var i = 0; i < profile.Count; i++)
        {
            var j = (i + 1) % profile.Count;
            area += profile[i].RadiusMm * profile[j].ZMm - profile[j].RadiusMm * profile[i].ZMm;
        }

        if (area > 0)
        {
            // پادساعتگرد — همان‌طور که لازم است.
            return profile;
        }

        var reversed = new ProfilePoint[profile.Count];
        for (var i = 0; i < profile.Count; i++)
            reversed[i] = profile[profile.Count - 1 - i];
        return reversed;
    }
}

namespace Sahand.Core.Threads;

/// <summary>
/// تابع شعاع مقطع رزوه به‌صورت خطی-تکه‌ای و تناوبی روی یک بازهٔ «پیشروی» (lead).
/// </summary>
/// <remarks>
/// این نمایش، قرارداد مشترک میان دامنه و موتور هندسه است: سطح رزوه در هر زاویهٔ θ
/// برابر ρ(z − lead·θ/2π) است. تناوب با lead (نه pitch) است تا رزوهٔ چندشروع بدون
/// منطق جداگانه درست ساخته شود. نقاط شکست باید اکیداً صعودی باشند.
/// </remarks>
public sealed class RadialProfileFunction
{
    private readonly double[] _u;
    private readonly double[] _r;

    /// <summary>دورهٔ تناوب برحسب میلی‌متر — همیشه برابر پیشروی (گام × شروع‌ها).</summary>
    public double Period { get; }

    /// <summary>بیشینهٔ شعاع پروفیل.</summary>
    public double MaxRadius { get; }

    /// <summary>کمینهٔ شعاع پروفیل.</summary>
    public double MinRadius { get; }

    /// <param name="breakpoints">نقاط شکست (u, r) صعودی؛ آخرین نقطه پایان دوره را تعیین می‌کند.</param>
    public RadialProfileFunction(IReadOnlyList<ThreadProfilePoint> breakpoints)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);
        if (breakpoints.Count < 2)
            throw new ArgumentException("پروفیل دست‌کم دو نقطهٔ شکست لازم دارد.", nameof(breakpoints));

        _u = new double[breakpoints.Count];
        _r = new double[breakpoints.Count];
        for (var i = 0; i < breakpoints.Count; i++)
        {
            _u[i] = breakpoints[i].AxialMm;
            _r[i] = breakpoints[i].RadiusMm;
            if (i > 0 && _u[i] <= _u[i - 1])
                throw new ArgumentException("نقاط شکست پروفیل باید اکیداً صعودی باشند.", nameof(breakpoints));
            if (_r[i] < 0)
                throw new ArgumentException("شعاع پروفیل نمی‌تواند منفی باشد.", nameof(breakpoints));
        }

        Period = _u[^1] > 0 ? _u[^1] : throw new ArgumentException("دورهٔ پروفیل باید مثبت باشد.");
        MaxRadius = _r.Max();
        MinRadius = _r.Min();
    }

    /// <summary>
    /// شعاع پروفیل در موقعیت محوری <paramref name="uMm"/> با تناوب مدولار را برمی‌گرداند.
    /// </summary>
    /// <remarks>ورودی می‌تواند منفی یا بزرگ‌تر از دوره باشد؛ تناوب به‌صورت مدولار اعمال می‌شود.</remarks>
    public double Evaluate(double uMm)
    {
        // نرمال‌سازی به [0, Period) — عملیات % دات‌کِت برای منفی نتیجهٔ منفی می‌دهد؛ جبران می‌شود.
        var u = uMm % Period;
        if (u < 0) u += Period;

        // بازهٔ پیچشی ابتدای دوره: میان آخرین نقطهٔ دورهٔ قبل و نخستین نقطهٔ دورهٔ جاری.
        if (u < _u[0])
        {
            var prevU = _u[^1] - Period; // موقعیت آخرین نقطه در دورهٔ قبلی (منفی یا صفر).
            if (u <= prevU) return _r[^1];
            var span = _u[0] - prevU;
            var t = (u - prevU) / span;
            return _r[^1] + t * (_r[0] - _r[^1]);
        }

        var index = Array.BinarySearch(_u, u);
        if (index >= 0) return _r[index];

        var insert = ~index;
        if (insert >= _u.Length)
        {
            // بازهٔ پایانی بدون نقطهٔ پایان: از آخرین شکست تا پایان دوره به نقطهٔ اول پیچ می‌خورد.
            var span = Period - _u[^1];
            if (span <= 1e-12) return _r[^1];
            var t = (u - _u[^1]) / span;
            return _r[^1] + t * (_r[0] - _r[^1]);
        }

        var u0 = _u[insert - 1];
        var u1 = _u[insert];
        var fraction = (u - u0) / (u1 - u0);
        return _r[insert - 1] + fraction * (_r[insert] - _r[insert - 1]);
    }
}

/// <summary>
/// سازندهٔ پروفیل مقطع رزوه بر اساس <see cref="ThreadSpec"/> برای همهٔ خانواده‌های پشتیبانی‌شده.
/// </summary>
/// <remarks>
/// پروفیل‌ها «بر پایهٔ پروفیل پایهٔ» خانواده‌ها ساخته می‌شوند (ISO 68 برای متریک/یونیفاید و
/// مشابه برای بقیه)؛ گردی ریشه/تاج ویتوورث و کلاس‌های تلرانس به‌صورت پخ‌شکسته تقریب زده می‌شوند و
/// این تقریب باید در یادداشت منبع دادهٔ مشخصات ثبت شود. ادعای انطباق رسمی با استاندارد مطرح نمی‌شود.
/// قرارداد خروجی: نقاط یک دورهٔ کامل «پیشروی» را می‌پوشانند و در ابتدا/انتهای دوره روی شعاع ریشه‌اند؛
/// برای رزوهٔ داخلی «ریشه» یعنی درهٔ پروفیل روی دیوارهٔ سوراک و برای رزوهٔ خارجی یعنی کف دندانه.
/// </remarks>
public static class ThreadProfileFactory
{
    /// <summary>
    /// پروفیل شعاعی تناوبی رزوه را می‌سازد.
    /// </summary>
    /// <param name="spec">مشخصات رزوه؛ باید پیش‌تر معتبر بوده باشد.</param>
    /// <returns>هندسهٔ پروفیل شامل نقاط یک دوره و قطرهای مشتق‌شده.</returns>
    /// <exception cref="ArgumentException">اگر پروفیل سفارشی نامعتبر یا خانواده پشتیبانی‌نشده باشد.</exception>
    public static ThreadProfileGeometry BuildProfile(ThreadSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (spec.Family == ThreadFamily.Custom)
            return BuildCustomProfile(spec);

        var (depthMm, crestWidthMm, valleyWidthMm, halfAngleDeg) = GetFamilyParameters(spec);
        var pitch = spec.PitchMm;
        var lead = spec.LeadMm;
        var nominalR = spec.NominalDiameterMm / 2.0;

        // رزوهٔ خارجی: دندانه از کف (ریشه) به قطر اسمی می‌رسد. رزوهٔ داخلی: دره روی دیوارهٔ سوراخ
        // (قطر اسمی جفت) است و نوک دندانه به سمت محور می‌رود؛ پهنای نوک بین دو نوع جابه‌جا می‌شود
        // چون برش پروفیل پایهٔ ISO 68 برای نوکِ داخلی H/4 و برای تاجِ خارجی H/8 است.
        // (جایگاه نقاط دندانه برای هر دو نوع یکی است: دره در لبهٔ سلول، نوک در مرکز.)
        bool isExternal = spec.Kind == ThreadKind.External;
        double valleyR, tipR, tipWidthMm;
        if (isExternal)
        {
            valleyR = nominalR - depthMm; // کف دندانهٔ خارجی زیر قطر اسمی.
            tipR = nominalR;              // تاج دندانهٔ خارجی روی قطر اسمی.
            tipWidthMm = crestWidthMm;    // پهنای تاج خارجی (برش H/8).
        }
        else
        {
            valleyR = nominalR;           // درهٔ داخلی روی دیوارهٔ سوراخ.
            tipR = nominalR - depthMm;    // نوک دندانهٔ داخلی به سمت محور.
            tipWidthMm = valleyWidthMm;    // پهنای نوک داخلی (برش H/4) — جابه‌جایی ISO 68.
        }

        var points = new List<ThreadProfilePoint>();
        var teeth = Math.Max(1, spec.Starts);
        for (var tooth = 0; tooth < teeth; tooth++)
        {
            AppendTooth(points, tooth * pitch, pitch, valleyR, tipR, tipWidthMm, halfAngleDeg);
        }

        // نقطهٔ پایان دوره روی شعاع دره؛ پیوستگی تناوب با نقطهٔ ابتدای دوره برقرار است.
        points.Add(new ThreadProfilePoint(valleyR, lead));

        return new ThreadProfileGeometry(
            points,
            2 * Math.Max(valleyR, tipR),
            2 * Math.Min(valleyR, tipR),
            valleyR + tipR); // قطر گام تقریبی: مجموع شعاع دره و نوک.
    }

    /// <summary>
    /// پارامترهای پروفیل پایهٔ هر خانواده را برمی‌گرداند (عمق، پهنای تاج/دره، نیم‌زاویه).
    /// </summary>
    /// <remarks>
    /// مقادیر از روابط پروفیل پایهٔ شناخته‌شدهٔ مهندسی محاسبه می‌شوند و تقریب پخ‌دار جایگزین
    /// شعاع‌های واقعی می‌شوند؛ منبع در یادداشت دادهٔ ThreadSpec ثبت شود. مخروط NPT/BSP جداگانه
    /// در لایهٔ هندسه با ضریب مقیاس شعاعی اعمال می‌شود.
    /// </remarks>
    private static (double DepthMm, double CrestWidthMm, double ValleyWidthMm, double HalfAngleDeg) GetFamilyParameters(ThreadSpec spec)
    {
        var p = spec.PitchMm;
        return spec.Family switch
        {
            // ISO 68: ارتفاع مثلث H=0.866P؛ برش تاج بیرونی H/8 و درهٔ داخلی H/4.
            ThreadFamily.IsoMetric or ThreadFamily.Unified =>
                (0.625 * 0.86602540378 * p, p / 8.0, p / 4.0, 30.0),

            // ویتوورث/BSP: زاویهٔ ۵۵ درجه؛ H = P/(2·tan27.5°)؛ پخ جایگزین گردی تقریبی.
            ThreadFamily.Whitworth or ThreadFamily.Bsp =>
                (0.64 * p / (2.0 * Math.Tan(27.5 * Math.PI / 180.0)), p / 6.0, p / 6.0, 27.5),

            // NPT: پروفیل ۶۰ درجه با برش تاج و ریشه (تقریب پخ‌دار جای شعاع).
            ThreadFamily.Npt =>
                (0.75 * 0.86602540378 * p, p / 8.0, p / 6.0, 30.0),

            // ذوزنقه‌ای ISO: نیم‌زاویهٔ ۳۰ درجه، عمق پایهٔ ۰٫۵P.
            ThreadFamily.IsoTrapezoidal =>
                (0.5 * p, 0.335 * p, 0.31 * p, 30.0),

            // ACME: نیم‌زاویهٔ ۱۴٫۵ درجه (زاویهٔ کل ۲۹)، عمق ۰٫۵P.
            ThreadFamily.Acme =>
                (0.5 * p, 0.335 * p, 0.31 * p, 14.5),

            // مربعی: دندانهٔ مربع با پهنای نصف گام و دیوارهٔ عمودی.
            ThreadFamily.Square =>
                (0.5 * p, 0.5 * p, 0.5 * p, 0.0),

            _ => throw new ArgumentException(
                $"خانوادهٔ «{spec.Family}» پروفیل از پیش تعریف‌شده ندارد؛ از Custom استفاده کنید."),
        };
    }

    /// <summary>
    /// یک دندانهٔ متقارن را در سلول گام [cellStart, cellStart+pitch] به نقاط می‌افزاید.
    /// </summary>
    /// <remarks>
    /// ساختار دندانه برای هر دو نوع داخلی/خارجی یکسان است و فقط ترتیب شعاع فرق می‌کند:
    /// پهنای دره در لبه‌های سلول، سپس دیواره، سپس پهنای نوک در مرکز سلول. نقاط همیشه
    /// صعودی در محورند (الزام RadialProfileFunction).
    /// </remarks>
    private static void AppendTooth(
        List<ThreadProfilePoint> points,
        double cellStart,
        double pitch,
        double valleyR,
        double tipR,
        double tipWidthMm,
        double halfAngleDeg)
    {
        var center = cellStart + pitch / 2.0;

        // گسترش محوری دیواره = عمق × tan(نیم‌زاویه)؛ برای مربعی (۰°) دیواره عمودی است.
        var flankRun = halfAngleDeg <= 0
            ? 0.0
            : Math.Abs(tipR - valleyR) * Math.Tan(halfAngleDeg * Math.PI / 180.0);

        var tipHalf = tipWidthMm / 2.0;

        // گارد هندسی: نوک + دیواره نباید از نیم‌گام بگذرد؛ در حالت مرزی، دیواره فشرده می‌شود
        // (دندانهٔ پُر بدون پهنای دره — تقریب قابل قبول برای گام‌های بسیار درشت).
        var maxRun = pitch / 2.0 - tipHalf;
        if (flankRun > maxRun) flankRun = Math.Max(0.0, maxRun);

        points.Add(new ThreadProfilePoint(valleyR, cellStart));
        points.Add(new ThreadProfilePoint(valleyR, center - tipHalf - flankRun));
        points.Add(new ThreadProfilePoint(tipR, center - tipHalf));
        points.Add(new ThreadProfilePoint(tipR, center + tipHalf));
        points.Add(new ThreadProfilePoint(valleyR, center + tipHalf + flankRun));
        // نقطهٔ u=cellStart+pitch به‌عنوان آغاز سلول بعد افزوده می‌شود.
    }

    /// <summary>
    /// پروفیل سفارشی کاربر را اعتبارسنجی و به تابع تناوبی یک دورهٔ پیشروی تبدیل می‌کند.
    /// </summary>
    /// <exception cref="ArgumentException">اگر نقاط نامرتب، نامعتبر یا دوره‌اش با گام ناسازگار باشد.</exception>
    private static ThreadProfileGeometry BuildCustomProfile(ThreadSpec spec)
    {
        var profile = spec.CustomProfile ?? throw new ArgumentException(
            "برای خانوادهٔ سفارشی باید نقاط پروفیل ارائه شود.");

        if (profile.Count < 3)
            throw new ArgumentException("پروفیل سفارشی دست‌کم ۳ نقطه لازم دارد.");

        for (var i = 0; i < profile.Count; i++)
        {
            if (profile[i].RadiusMm < 0)
                throw new ArgumentException($"شعاع نقطهٔ {i + 1} از پروفیل سفارشی نمی‌تواند منفی باشد.");

            if (i > 0 && profile[i].AxialMm <= profile[i - 1].AxialMm)
                throw new ArgumentException("نقاط پروفیل سفارشی باید اکیداً صعودی در محور باشند.");
        }

        // تداوم تناوب: شعاع نخستین و آخرین نقطه باید یکسان باشد و دوره با گام سازگار باشد.
        if (Math.Abs(profile[0].RadiusMm - profile[^1].RadiusMm) > 1e-9)
            throw new ArgumentException("برای پیوستگی دورهٔ تناوب، شعاع نقطهٔ اول و آخر پروفیل باید برابر باشد.");

        var period = profile[^1].AxialMm;
        if (period <= 0)
            throw new ArgumentException("بازهٔ محوری پروفیل سفارشی باید مثبت باشد.");

        if (Math.Abs(period - spec.PitchMm) > 1e-9)
            throw new ArgumentException(
                $"بازهٔ محوری پروفیل سفارشی ({period:0.###}mm) باید دقیقاً برابر گام ({spec.PitchMm:0.###}mm) باشد.");

        // چندشروع سفارشی: نقاط در هر سلول گام تکرار می‌شوند؛ فاز زاویه‌ای از جابه‌جایی مارپیچی می‌آید.
        // نقطهٔ آغاز هر سلول با نقطهٔ پایان سلول قبل تکراری است و حذف می‌شود.
        var points = new List<ThreadProfilePoint>();
        var teeth = Math.Max(1, spec.Starts);
        for (var tooth = 0; tooth < teeth; tooth++)
        {
            var offset = tooth * spec.PitchMm;
            for (var i = 0; i < profile.Count; i++)
            {
                if (tooth > 0 && i == 0) continue;                       // تکرار با پایان سلول قبل.
                if (tooth == teeth - 1 && i == profile.Count - 1) continue; // نقطهٔ پایان دوره جداگانه افزوده می‌شود.
                points.Add(new ThreadProfilePoint(profile[i].RadiusMm, profile[i].AxialMm + offset));
            }
        }

        points.Add(new ThreadProfilePoint(profile[0].RadiusMm, spec.LeadMm));

        return new ThreadProfileGeometry(
            points,
            2 * points.Max(x => x.RadiusMm),
            2 * points.Min(x => x.RadiusMm),
            points.Max(x => x.RadiusMm) + points.Min(x => x.RadiusMm));
    }
}

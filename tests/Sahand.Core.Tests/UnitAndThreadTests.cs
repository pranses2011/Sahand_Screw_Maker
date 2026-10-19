using Sahand.Core.Threads;
using Sahand.Core.Units;
using Sahand.Core.Validation;
using Xunit;

namespace Sahand.Core.Tests;

/// <summary>
/// آزمون‌های واحد، تبدیل واحد و ورود کسر اینچ (پوشش GEO-08).
/// </summary>
public class UnitSystemTests
{
    [Fact]
    public void InchToMillimeter_UsesExactFactor()
    {
        // ۱ اینچ باید دقیقاً ۲۵٫۴ میلی‌متر شود؛ گردکردن بی‌صدا ممنوع است.
        var mm = LengthUnitConverter.ToMillimeters(1.0, LengthUnit.Inch);
        Assert.Equal(25.4, mm, precision: 12);
    }

    [Fact]
    public void RoundTrip_InchMillimeter_NoAccumulatedError()
    {
        // تبدیل رفت‌وبرگشت تکراری نباید خطای تجمعی ناموجه بسازد (GEO-08).
        var value = 12.7;
        for (var i = 0; i < 1000; i++)
        {
            value = LengthUnitConverter.FromMillimeters(
                LengthUnitConverter.ToMillimeters(value, LengthUnit.Inch), LengthUnit.Inch);
        }
        Assert.Equal(12.7, value, precision: 9);
    }

    [Theory]
    [InlineData("1/2", 12.7)]
    [InlineData("3/4", 19.05)]
    [InlineData("1 1/2", 38.1)]
    [InlineData("0.75", 19.05)]
    [InlineData(".5", 12.7)]
    public void FractionInch_ParsesToExactMillimeters(string input, double expectedMm)
    {
        var mm = FractionInch.ParseToMillimeters(input);
        Assert.Equal(expectedMm, mm, precision: 9);
    }

    [Fact]
    public void FractionInch_PersianDigits_AreAccepted()
    {
        // ورودی فارسی «۱٫۲۵» باید مثل 1.25 تفسیر شود.
        var mm = FractionInch.ParseToMillimeters("۱٫۲۵");
        Assert.Equal(31.75, mm, precision: 9);
    }

    [Theory]
    [InlineData("0/4")]
    [InlineData("1/")]
    [InlineData("abc")]
    public void FractionInch_InvalidInput_Throws(string input)
    {
        Assert.Throws<FormatException>(() => FractionInch.ParseToMillimeters(input));
    }

    [Fact]
    public void PersianDigits_NormalizesToLatin()
    {
        var normalized = PersianDigits.NormalizeDigits("۱۲۳۴۵");
        Assert.Equal("12345", normalized);
    }

    [Fact]
    public void PersianDigits_ArabicDigits_NormalizeToLatin()
    {
        var normalized = PersianDigits.NormalizeDigits("٠١٢");
        Assert.Equal("012", normalized);
    }

    [Fact]
    public void PersianDigits_ToPersianRoundTrip()
    {
        var persian = PersianDigits.ToPersian("2026");
        Assert.Equal("۲۰۲۶", persian);
        Assert.Equal("2026", PersianDigits.NormalizeDigits(persian));
    }
}

/// <summary>
/// آزمون‌های کارخانهٔ پروفیل رزوه — پایهٔ هندسی محصول (پوشش GEO-04/05).
/// </summary>
public class ThreadProfileFactoryTests
{
    private static ThreadSpec Metric(double diameter = 6, double pitch = 1, ThreadKind kind = ThreadKind.External) => new()
    {
        Family = ThreadFamily.IsoMetric,
        Kind = kind,
        NominalDiameterMm = diameter,
        PitchMm = pitch,
        ThreadLengthMm = 10,
        HalfAngleDegrees = 30,
    };

    [Fact]
    public void MetricExternal_ProfileSpansExactlyOneLead_AndPeriodicityHolds()
    {
        var profile = ThreadProfileFactory.BuildProfile(Metric());
        var function = profile.ToProfileFunction();

        // طول دوره باید دقیقاً برابر پیشروی (تک‌شروع = گام) باشد.
        Assert.Equal(1.0, function.Period, precision: 9);

        // تناوب: مقدار u و u+P باید یکی باشد (پیوستگی دوره).
        Assert.Equal(function.Evaluate(0.37), function.Evaluate(1.37), precision: 9);

        // قطرها: تاج روی اسمی و ریشه زیر آن برای پروفیل خارجی.
        Assert.Equal(6.0, profile.MajorDiameterMm, precision: 9);
        Assert.Equal(6.0 - 1.08253175, profile.MinorDiameterMm, precision: 6);
    }

    [Fact]
    public void MetricInternal_TipRadiusMatchesExternalRoot()
    {
        // پروفیل داخلی (مهره) باید نوکش به ریشهٔ پروفیل خارجی (پیچ) برسد — جفت‌شدگی پایه.
        var external = ThreadProfileFactory.BuildProfile(Metric(kind: ThreadKind.External));
        var internalProfile = ThreadProfileFactory.BuildProfile(Metric(kind: ThreadKind.Internal));

        Assert.Equal(external.MinorDiameterMm, internalProfile.MinorDiameterMm, precision: 6);
        Assert.Equal(external.MajorDiameterMm, internalProfile.MajorDiameterMm, precision: 6);
    }

    [Fact]
    public void MultiStart_PeriodEqualsLead_AndTeethCountCorrect()
    {
        var spec = Metric() with { Starts = 3 };
        var profile = ThreadProfileFactory.BuildProfile(spec);
        var function = profile.ToProfileFunction();

        // دورهٔ تناوب = پیشروی = گام × شروع‌ها (تفکیک pitch/lead — سند ۷-۵).
        Assert.Equal(3.0, function.Period, precision: 9);
        Assert.Equal(3.0, spec.LeadMm, precision: 9);

        // شمار دندانه‌ها در یک دوره باید برابر تعداد شروع‌ها باشد.
        var crossings = 0;
        for (var u = 0.0; u < function.Period - 1e-9; u += function.Period / 1000.0)
        {
            var r1 = function.Evaluate(u);
            var r2 = function.Evaluate(u + function.Period / 1000.0);
            if (r1 < 2.9 && r2 >= 2.9) crossings++; // عبور از نزدیکی تاج.
        }
        Assert.Equal(3, crossings);
    }

    [Fact]
    public void CustomProfile_InvalidInputs_RejectedWithPersianMessage()
    {
        // نقاط ناهم‌مرتب.
        var unordered = Metric() with
        {
            Family = ThreadFamily.Custom,
            CustomProfile = [new ThreadProfilePoint(3, 0), new ThreadProfilePoint(3, 0.4), new ThreadProfilePoint(3, 0.2)],
        };
        var ex = Assert.Throws<ArgumentException>(() => ThreadProfileFactory.BuildProfile(unordered));
        Assert.Contains("صعودی", ex.Message);

        // دوره با گام ناسازگار.
        var wrongPeriod = Metric() with
        {
            Family = ThreadFamily.Custom,
            CustomProfile =
            [
                new ThreadProfilePoint(3, 0),
                new ThreadProfilePoint(3.2, 0.4),
                new ThreadProfilePoint(3, 0.8),
                new ThreadProfilePoint(3, 2.0),
            ],
        };
        Assert.Throws<ArgumentException>(() => ThreadProfileFactory.BuildProfile(wrongPeriod));
    }

    [Fact]
    public void CustomProfile_ValidPeriodic_BuildsWithPeriodicity()
    {
        var spec = Metric() with
        {
            Family = ThreadFamily.Custom,
            CustomProfile =
            [
                new ThreadProfilePoint(2.5, 0),
                new ThreadProfilePoint(3.0, 0.25),
                new ThreadProfilePoint(3.0, 0.6),
                new ThreadProfilePoint(2.5, 0.9),
                new ThreadProfilePoint(2.5, 1.0),
            ],
        };
        var profile = ThreadProfileFactory.BuildProfile(spec);
        var function = profile.ToProfileFunction();

        Assert.Equal(1.0, function.Period, precision: 9);
        Assert.Equal(function.Evaluate(0.1), function.Evaluate(1.1), precision: 9);
    }
}

/// <summary>
/// آزمون‌های اعتبارسنجی ورودی (پوشش GEO-07).
/// </summary>
public class ThreadValidatorTests
{
    [Fact]
    public void InvalidNumericInputs_RejectedBeforeGeometry()
    {
        var nan = new ThreadSpec { NominalDiameterMm = double.NaN, PitchMm = 1 };
        Assert.True(ThreadValidator.Validate(nan).HasErrors);

        var negative = new ThreadSpec { NominalDiameterMm = -5, PitchMm = 1 };
        Assert.True(ThreadValidator.Validate(negative).HasErrors);

        var infinity = new ThreadSpec { NominalDiameterMm = 6, PitchMm = double.PositiveInfinity };
        Assert.True(ThreadValidator.Validate(infinity).HasErrors);

        var huge = new ThreadSpec { NominalDiameterMm = 1e9, PitchMm = 1 };
        Assert.True(ThreadValidator.Validate(huge).HasErrors);
    }

    [Fact]
    public void OversizedPitch_WarnedOrRejected()
    {
        // گام بزرگ‌تر از نصف قطر: رد صریح با پیام فارسی.
        var coarse = new ThreadSpec { NominalDiameterMm = 4, PitchMm = 2.5 };
        var result = ThreadValidator.Validate(coarse);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Messages, m => m.ParameterName == "PitchMm");
    }

    [Fact]
    public void SimplifiedMode_ProducesHonestWarning()
    {
        // رزوهٔ نمایشی باید هشدار روشن بدهد (سند ۳-۵).
        var spec = new ThreadSpec { NominalDiameterMm = 6, PitchMm = 1, ModelingMode = ThreadModelingMode.Simplified };
        var result = ThreadValidator.Validate(spec);
        Assert.False(result.HasErrors);
        Assert.Contains(result.Messages, m => m.Severity == ValidationSeverity.Warning && m.Message.Contains("نمایشی"));
    }

    [Fact]
    public void NptWithoutTaper_Warned()
    {
        // NPT ذاتاً مخروطی است؛ بدون مخروط هشدار می‌دهد.
        var spec = new ThreadSpec { Family = ThreadFamily.Npt, NominalDiameterMm = 20, PitchMm = 1.8 };
        var result = ThreadValidator.Validate(spec);
        Assert.Contains(result.Messages, m => m.Severity == ValidationSeverity.Warning && m.Message.Contains("مخروط"));
    }
}

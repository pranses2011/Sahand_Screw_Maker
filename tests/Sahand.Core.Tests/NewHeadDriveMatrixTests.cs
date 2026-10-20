using Sahand.Core.Parts;
using Sahand.Core.Threads;
using Sahand.Core.Validation;
using Xunit;

namespace Sahand.Core.Tests;

/// <summary>
/// آزمون‌های ماتریس سازگاری سر×محرک ۰٫۵٫۰ — سرهای خاص جدید و محرک‌های امنیتی جدید.
/// </summary>
public class NewHeadDriveMatrixTests
{
    private static ThreadSpec M6External => new()
    {
        Family = ThreadFamily.IsoMetric,
        Kind = ThreadKind.External,
        NominalDiameterMm = 6.0,
        PitchMm = 1.0,
        ThreadLengthMm = 12.0,
    };

    private static BoltSpec Base(HeadStyle head, DriveStyle drive) => new()
    {
        Head = head,
        Drive = drive,
        Thread = M6External,
        LengthUnderHeadMm = 12.0,
        HeadHeightMm = 4.0,
        HeadDiameterMm = head is HeadStyle.Cheese or HeadStyle.Truss or HeadStyle.Fillister or
                         HeadStyle.Wafer ? 10.0 :
            head is HeadStyle.RaisedCountersunk ? 14.0 : null,
        HeadAcrossFlatsMm = head is HeadStyle.TwelvePoint ? 9.5 : null,
        FlangeDiameterMm = head == HeadStyle.TwelvePoint ? 11.5 : null,
        FlangeThicknessMm = head == HeadStyle.TwelvePoint ? 1.0 : null,
        HexSocketAcrossFlatsMm = drive is DriveStyle.HexSocket or DriveStyle.PinHex ? 3.2 : null,
        HexSocketDepthMm = drive is DriveStyle.HexSocket or DriveStyle.PinHex ? 2.0 : null,
        TorxAcrossLobesMm = drive is DriveStyle.Torx or DriveStyle.SecurityTorx or
                DriveStyle.Pentalobe or DriveStyle.TriWing or DriveStyle.TorqSet ? 3.9 : null,
        TorxDepthMm = drive is DriveStyle.Torx or DriveStyle.SecurityTorx or
                DriveStyle.Pentalobe or DriveStyle.TriWing or DriveStyle.TorqSet ? 2.0 : null,
        SecurityPinDiameterMm = drive is DriveStyle.PinHex or DriveStyle.SecurityTorx ? 1.4 : null,
        SpannerHoleDiameterMm = drive == DriveStyle.Spanner ? 1.3 : null,
        SpannerHoleDepthMm = drive == DriveStyle.Spanner ? 1.6 : null,
        SlotWidthMm = drive is DriveStyle.Slot or DriveStyle.Phillips or DriveStyle.OneWay ? 1.1 : null,
        SlotDepthMm = drive is DriveStyle.Slot or DriveStyle.Phillips or DriveStyle.OneWay ? 1.2 : null,
    };

    /// <summary>سرهای رویه‌تختِ جدید باید همهٔ محرک‌های حفره‌ای/سوراخ‌دار را بپذیرند.</summary>
    [Theory]
    [InlineData(HeadStyle.Cheese, DriveStyle.None)]
    [InlineData(HeadStyle.Cheese, DriveStyle.HexSocket)]
    [InlineData(HeadStyle.Cheese, DriveStyle.PinHex)]
    [InlineData(HeadStyle.Cheese, DriveStyle.Torx)]
    [InlineData(HeadStyle.Cheese, DriveStyle.SecurityTorx)]
    [InlineData(HeadStyle.Cheese, DriveStyle.Pentalobe)]
    [InlineData(HeadStyle.Cheese, DriveStyle.TriWing)]
    [InlineData(HeadStyle.Cheese, DriveStyle.TorqSet)]
    [InlineData(HeadStyle.Cheese, DriveStyle.Spanner)]
    [InlineData(HeadStyle.Cheese, DriveStyle.OneWay)]
    [InlineData(HeadStyle.Cheese, DriveStyle.Slot)]
    [InlineData(HeadStyle.Cheese, DriveStyle.Phillips)]
    [InlineData(HeadStyle.Truss, DriveStyle.Pentalobe)]
    [InlineData(HeadStyle.Truss, DriveStyle.Spanner)]
    [InlineData(HeadStyle.Truss, DriveStyle.OneWay)]
    [InlineData(HeadStyle.Fillister, DriveStyle.TorqSet)]
    [InlineData(HeadStyle.Fillister, DriveStyle.Spanner)]
    [InlineData(HeadStyle.Wafer, DriveStyle.TriWing)]
    [InlineData(HeadStyle.Wafer, DriveStyle.OneWay)]
    [InlineData(HeadStyle.Wafer, DriveStyle.HexSocket)]
    [InlineData(HeadStyle.TwelvePoint, DriveStyle.TorqSet)]
    [InlineData(HeadStyle.TwelvePoint, DriveStyle.Spanner)]
    [InlineData(HeadStyle.TwelvePoint, DriveStyle.HexSocket)]
    public void NewHeads_AcceptSupportedDrives(HeadStyle head, DriveStyle drive)
    {
        var result = PartValidator.Validate(Base(head, drive));
        Assert.False(result.HasErrors,
            $"{head}/{drive}: {string.Join("; ", result.Messages.Select(m => m.Message))}");
    }

    /// <summary>محدودیت‌های مستند: سرهای بدون پشتیبانی محرک باید با پیام فارسی رد شوند.</summary>
    [Theory]
    [InlineData(HeadStyle.RaisedCountersunk, DriveStyle.HexSocket)]
    [InlineData(HeadStyle.RaisedCountersunk, DriveStyle.Spanner)]
    [InlineData(HeadStyle.FlatCountersunk, DriveStyle.Pentalobe)]
    [InlineData(HeadStyle.Pan, DriveStyle.TriWing)]
    [InlineData(HeadStyle.Truss, DriveStyle.Slot)]
    [InlineData(HeadStyle.Fillister, DriveStyle.Phillips)]
    [InlineData(HeadStyle.Wafer, DriveStyle.Slot)]
    [InlineData(HeadStyle.TwelvePoint, DriveStyle.Slot)]
    [InlineData(HeadStyle.Button, DriveStyle.Slot)]
    public void UnsupportedCombos_RejectedWithMessage(HeadStyle head, DriveStyle drive)
    {
        var result = PartValidator.Validate(Base(head, drive));
        Assert.True(result.HasErrors, $"{head}/{drive} باید رد شود.");
    }

    /// <summary>محرک اسپنر: قطر/عمق سوراخ الزامی و گسترش چیدمان گارد می‌خورد.</summary>
    [Fact]
    public void Spanner_RequiresHoleDiameterAndDepth()
    {
        var spec = Base(HeadStyle.SocketCap, DriveStyle.Spanner) with { SpannerHoleDiameterMm = null };
        var result = PartValidator.Validate(spec);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Messages, i => i.Message.Contains("قطر سوراخ‌های اسپنر"));

        spec = Base(HeadStyle.SocketCap, DriveStyle.Spanner) with { SpannerHoleDepthMm = null };
        result = PartValidator.Validate(spec);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Messages, i => i.Message.Contains("عمق سوراخ‌های اسپنر"));
    }

    [Fact]
    public void Spanner_TooLargeForHead_IsRejected()
    {
        // گسترش ~۲٫۹ برابر قطر سوراخ؛ برای سر ۱۰mm سوراخ ۴mm بیرون می‌زند.
        var spec = Base(HeadStyle.SocketCap, DriveStyle.Spanner) with
        {
            HeadDiameterMm = 10.0,
            SpannerHoleDiameterMm = 4.0,
        };
        var result = PartValidator.Validate(spec);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Messages, i => i.Message.Contains("بیرون می‌زند"));
    }

    /// <summary>محرک یک‌طرفه: پهنای/عمق پره از فیلدهای شیار استفاده می‌کند و الزامی است.</summary>
    [Fact]
    public void OneWay_RequiresBladeWidthAndDepth()
    {
        var spec = Base(HeadStyle.SocketCap, DriveStyle.OneWay) with { SlotWidthMm = null };
        var result = PartValidator.Validate(spec);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Messages, i => i.Message.Contains("پهنای پره"));

        spec = Base(HeadStyle.SocketCap, DriveStyle.OneWay) with { SlotDepthMm = 4.5 };
        result = PartValidator.Validate(spec);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Messages, i => i.Message.Contains("عمق پره"));
    }

    /// <summary>سر دوازده‌گوش: عرض آچارخور الزامی؛ فلنج کوچک رد می‌شود.</summary>
    [Fact]
    public void TwelvePoint_RequiresAcrossFlats()
    {
        var spec = Base(HeadStyle.TwelvePoint, DriveStyle.None) with { HeadAcrossFlatsMm = null };
        var result = PartValidator.Validate(spec);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Messages, i => i.Message.Contains("دوازده‌گوش"));
    }

    [Fact]
    public void TwelvePoint_FlangeMustExceedCorners()
    {
        // قطر گوشه‌های ۱۲گوش با af=9.5 ≈ 9.83؛ فلنج ۱۰mm کوچک است.
        var spec = Base(HeadStyle.TwelvePoint, DriveStyle.None) with { FlangeDiameterMm = 10.0 };
        var result = PartValidator.Validate(spec);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Messages, i => i.Message.Contains("گوشه‌های بدنهٔ دوازده‌گوش"));
    }

    /// <summary>پنتالوب/تری‌وینگ/تورک‌ست همان قیود قطر/عمق محرک گلبرگی را دارند.</summary>
    [Fact]
    public void NewLobedDrives_RequireTorxFields()
    {
        var spec = Base(HeadStyle.SocketCap, DriveStyle.Pentalobe) with { TorxAcrossLobesMm = null };
        var result = PartValidator.Validate(spec);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Messages, i => i.Message.Contains("قطر اسمی محرک گلبرگی"));

        spec = Base(HeadStyle.SocketCap, DriveStyle.TriWing) with { TorxDepthMm = null };
        result = PartValidator.Validate(spec);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Messages, i => i.Message.Contains("عمق حفرهٔ محرک گلبرگی"));
    }
}

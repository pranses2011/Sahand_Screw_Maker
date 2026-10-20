using Sahand.Core.Parts;
using Sahand.Core.Threads;
using Sahand.Geometry.Builders;
using Sahand.Geometry.Meshing;
using Xunit;

namespace Sahand.Geometry.Tests;

/// <summary>آزمون دودی سریع سرها/محرک‌های جدید ۰٫۵٫۰ (نسخهٔ موقت — زیرمجموعهٔ آزمون‌های نهایی).</summary>
public class SmokeNewHeadsDrivesTests
{
    private static ThreadSpec M6 => new()
    {
        Family = ThreadFamily.IsoMetric,
        Kind = ThreadKind.External,
        NominalDiameterMm = 6.0,
        PitchMm = 1.0,
        ThreadLengthMm = 12.0,
    };

    private static BoltSpec Bolt(HeadStyle head, DriveStyle drive, double? headD = null, double? af = null) => new()
    {
        Head = head,
        Drive = drive,
        Thread = M6,
        LengthUnderHeadMm = 12.0,
        HeadHeightMm = 4.0,
        HeadDiameterMm = headD,
        HeadAcrossFlatsMm = af,
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

    [Theory]
    [InlineData(HeadStyle.Cheese, DriveStyle.None)]
    [InlineData(HeadStyle.Cheese, DriveStyle.Pentalobe)]
    [InlineData(HeadStyle.Cheese, DriveStyle.TriWing)]
    [InlineData(HeadStyle.Cheese, DriveStyle.TorqSet)]
    [InlineData(HeadStyle.Cheese, DriveStyle.Spanner)]
    [InlineData(HeadStyle.Cheese, DriveStyle.OneWay)]
    [InlineData(HeadStyle.Truss, DriveStyle.Pentalobe)]
    [InlineData(HeadStyle.Fillister, DriveStyle.Spanner)]
    [InlineData(HeadStyle.Wafer, DriveStyle.TriWing)]
    [InlineData(HeadStyle.Wafer, DriveStyle.OneWay)]
    [InlineData(HeadStyle.RaisedCountersunk, DriveStyle.None)]
    [InlineData(HeadStyle.TwelvePoint, DriveStyle.None)]
    [InlineData(HeadStyle.TwelvePoint, DriveStyle.TorqSet)]
    [InlineData(HeadStyle.TwelvePoint, DriveStyle.Spanner)]
    public void NewHeadDriveCombos_BuildWatertight(HeadStyle head, DriveStyle drive)
    {
        // خزینهٔ گنبدی: با زاویهٔ ۹۰° و ارتفاع ۴، قطر سر باید ≥۱۴ باشد تا قله از ساق بزرگ‌تر بماند.
        var spec = Bolt(head, drive,
            headD: head is HeadStyle.Cheese or HeadStyle.Truss or HeadStyle.Fillister or
                    HeadStyle.Wafer ? 10.0 :
                head == HeadStyle.RaisedCountersunk ? 14.0 : null,
            af: head is HeadStyle.TwelvePoint ? 9.5 : null);
        spec = spec with { HeadHeightMm = head == HeadStyle.Wafer ? 2.4 : 4.0 };

        var mesh = BoltBuilder.Build(spec, MeshQualityPreset.Draft);
        var validation = MeshValidator.Validate(mesh, checkSelfIntersection: true);

        Assert.True(validation.IsWatertight,
            $"{head}/{drive}: {string.Join("; ", validation.Issues)}");
        Assert.True(validation.HasConsistentOrientation, $"{head}/{drive}: جهت‌دهی ناسازگار.");
        Assert.Equal(0, validation.SelfIntersections);
        Assert.True(mesh.SignedVolumeMm3() > 50, $"{head}/{drive}: حجم غیرمنتظره {mesh.SignedVolumeMm3():0.#}");
    }
}

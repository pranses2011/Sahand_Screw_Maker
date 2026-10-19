using Sahand.Core.Parts;
using Sahand.Core.Threads;
using Sahand.Core.Validation;
using Sahand.Geometry.Builders;
using Sahand.Geometry.Meshing;
using Xunit;

namespace Sahand.Geometry.Tests;

/// <summary>
/// آزمون‌های سر دکمه‌ای/فلنجی و محرک‌های امنیتی پین‌دار (گسترش ۰٫۴٫۰).
/// </summary>
/// <remarks>
/// پوشش: ساخت watertight همهٔ ترکیب‌های مجاز جدید، اثر حجمی پین امنیتی (افزایش نسبت به
/// حفرهٔ ساده)، پوشش قطر فلنج، رد ترکیب‌های نامعتبر در PartValidator، و گاردهای سازنده.
/// </remarks>
public class ButtonFlangeSecurityTests
{
    private static void AssertValidMesh(Mesh mesh, double minExpectedVolume)
    {
        var heavy = mesh.Triangles.Length > MeshValidator.MaxSelfIntersectionFaceCount;
        var validation = MeshValidator.Validate(mesh, checkSelfIntersection: true);
        Assert.True(validation.IsWatertight, $"مش بسته نیست: {string.Join("; ", validation.Issues)}");
        Assert.True(validation.HasConsistentOrientation, "جهت‌دهی ناسازگار است.");
        if (!heavy)
            Assert.Equal(0, validation.SelfIntersections);
        Assert.True(mesh.SignedVolumeMm3() > minExpectedVolume,
            $"حجم {mesh.SignedVolumeMm3():0.##} غیرمنتظره کم است.");
    }

    private static ThreadSpec MetricThread(double diameter = 6, double pitch = 1, double length = 14) => new()
    {
        Family = ThreadFamily.IsoMetric,
        Kind = ThreadKind.External,
        NominalDiameterMm = diameter,
        PitchMm = pitch,
        ThreadLengthMm = length,
    };

    // ---------- سر دکمه‌ای ----------

    [Fact]
    public void ButtonHead_NoDrive_BuildsWatertightDome()
    {
        var spec = new BoltSpec
        {
            Head = HeadStyle.Button,
            Drive = DriveStyle.None,
            Thread = MetricThread(),
            LengthUnderHeadMm = 14,
            HeadHeightMm = 3.6,
            HeadDiameterMm = 10.5,
            TipChamferMm = 0.6,
        };

        Assert.False(PartValidator.Validate(spec).HasErrors);
        var mesh = BoltBuilder.Build(spec, MeshQualityPreset.Draft);
        AssertValidMesh(mesh, minExpectedVolume: 300);

        // پهنای سر دکمه‌ای از قطر ساق بزرگ‌تر است.
        var bounds = mesh.Bounds();
        Assert.True(bounds.MaxX - bounds.MinX > 10.0,
            $"پهنای مدل ({bounds.MaxX - bounds.MinX:0.##}) باید ~قطر سر (۱۰٫۵) باشد.");
    }

    [Fact]
    public void ButtonHead_WithHexSocket_PocketCutsVolume()
    {
        var thread = MetricThread();
        var solid = new BoltSpec
        {
            Head = HeadStyle.Button, Drive = DriveStyle.None, Thread = thread,
            LengthUnderHeadMm = 14, HeadHeightMm = 3.6, HeadDiameterMm = 10.5, TipChamferMm = 0.6,
        };
        var socketed = solid with
        {
            Drive = DriveStyle.HexSocket,
            HexSocketAcrossFlatsMm = 4.0,
            HexSocketDepthMm = 2.2,
        };

        Assert.False(PartValidator.Validate(socketed).HasErrors);
        var solidMesh = BoltBuilder.Build(solid, MeshQualityPreset.Draft);
        var socketMesh = BoltBuilder.Build(socketed, MeshQualityPreset.Draft);
        AssertValidMesh(socketMesh, minExpectedVolume: 280);

        Assert.True(solidMesh.SignedVolumeMm3() > socketMesh.SignedVolumeMm3(),
            "حفرهٔ آلن روی سر دکمه‌ای باید حجم را کم کند.");
    }

    [Fact]
    public void ButtonHead_WithTorx_BuildsWatertight()
    {
        var spec = new BoltSpec
        {
            Head = HeadStyle.Button,
            Drive = DriveStyle.Torx,
            Thread = MetricThread(),
            LengthUnderHeadMm = 14,
            HeadHeightMm = 3.6,
            HeadDiameterMm = 10.5,
            TorxAcrossLobesMm = 4.6,
            TorxDepthMm = 2.0,
            TipChamferMm = 0.6,
        };

        Assert.False(PartValidator.Validate(spec).HasErrors);
        AssertValidMesh(BoltBuilder.Build(spec, MeshQualityPreset.Draft), minExpectedVolume: 280);
    }

    [Fact]
    public void ButtonHead_Slot_IsRejected()
    {
        var spec = new BoltSpec
        {
            Head = HeadStyle.Button,
            Drive = DriveStyle.Slot,
            Thread = MetricThread(),
            LengthUnderHeadMm = 14,
            HeadHeightMm = 3.6,
            HeadDiameterMm = 10.5,
            SlotWidthMm = 1.2,
            SlotDepthMm = 1.0,
        };

        Assert.True(PartValidator.Validate(spec).HasErrors);
    }

    // ---------- سر فلنجی ----------

    [Fact]
    public void FlangeHead_NoDrive_BuildsWatertightWithFlangeBreadth()
    {
        var spec = new BoltSpec
        {
            Head = HeadStyle.Flange,
            Drive = DriveStyle.None,
            Thread = MetricThread(),
            LengthUnderHeadMm = 14,
            HeadHeightMm = 5.2,
            HeadAcrossFlatsMm = 9.3,
            FlangeDiameterMm = 12.5,
            FlangeThicknessMm = 1.6,
            TipChamferMm = 0.6,
        };

        Assert.False(PartValidator.Validate(spec).HasErrors);
        var mesh = BoltBuilder.Build(spec, MeshQualityPreset.Draft);
        AssertValidMesh(mesh, minExpectedVolume: 350);

        // دیسک فلنج پهنای کلی مدل را تعیین می‌کند (~۱۲٫۵).
        var bounds = mesh.Bounds();
        Assert.True(bounds.MaxX - bounds.MinX > 11.5,
            $"پهنای مدل ({bounds.MaxX - bounds.MinX:0.##}) باید ~قطر فلنج (۱۲٫۵) باشد.");
    }

    [Fact]
    public void FlangeHead_WithSecurityTorx_BuildsWatertight()
    {
        var spec = new BoltSpec
        {
            Head = HeadStyle.Flange,
            Drive = DriveStyle.SecurityTorx,
            Thread = MetricThread(),
            LengthUnderHeadMm = 14,
            HeadHeightMm = 5.2,
            HeadAcrossFlatsMm = 9.3,
            FlangeDiameterMm = 12.5,
            FlangeThicknessMm = 1.6,
            TorxAcrossLobesMm = 5.0,
            TorxDepthMm = 2.6,
            SecurityPinDiameterMm = 1.4,
            TipChamferMm = 0.6,
        };

        Assert.False(PartValidator.Validate(spec).HasErrors);
        AssertValidMesh(BoltBuilder.Build(spec, MeshQualityPreset.Draft), minExpectedVolume: 330);
    }

    [Fact]
    public void FlangeHead_SmallFlangeDiameter_IsRejected()
    {
        var spec = new BoltSpec
        {
            Head = HeadStyle.Flange,
            Drive = DriveStyle.None,
            Thread = MetricThread(),
            LengthUnderHeadMm = 14,
            HeadHeightMm = 5.2,
            HeadAcrossFlatsMm = 9.3,
            FlangeDiameterMm = 9.0, // کوچک‌تر از آچارخور.
            FlangeThicknessMm = 1.6,
        };

        Assert.True(PartValidator.Validate(spec).HasErrors);
    }

    [Fact]
    public void FlangeHead_TooThickFlange_IsRejected()
    {
        var spec = new BoltSpec
        {
            Head = HeadStyle.Flange,
            Drive = DriveStyle.None,
            Thread = MetricThread(),
            LengthUnderHeadMm = 14,
            HeadHeightMm = 5.2,
            HeadAcrossFlatsMm = 9.3,
            FlangeDiameterMm = 12.5,
            FlangeThicknessMm = 5.2, // برابر ارتفاع سر.
        };

        Assert.True(PartValidator.Validate(spec).HasErrors);
    }

    // ---------- محرک‌های امنیتی پین‌دار ----------

    public static IEnumerable<object[]> PinDriveHeads()
    {
        yield return new object[] { HeadStyle.SocketCap, DriveStyle.PinHex };
        yield return new object[] { HeadStyle.Hex, DriveStyle.PinHex };
        yield return new object[] { HeadStyle.SocketCap, DriveStyle.SecurityTorx };
        yield return new object[] { HeadStyle.Hex, DriveStyle.SecurityTorx };
        yield return new object[] { HeadStyle.Button, DriveStyle.PinHex };
        yield return new object[] { HeadStyle.Button, DriveStyle.SecurityTorx };
        yield return new object[] { HeadStyle.Flange, DriveStyle.PinHex };
    }

    [Theory]
    [MemberData(nameof(PinDriveHeads))]
    public void SecurityPinDrive_BuildsWatertight(HeadStyle head, DriveStyle drive)
    {
        var isTorx = drive == DriveStyle.SecurityTorx;
        var spec = new BoltSpec
        {
            Head = head,
            Drive = drive,
            Thread = MetricThread(),
            LengthUnderHeadMm = 14,
            HeadHeightMm = head == HeadStyle.Flange ? 5.2 : head == HeadStyle.Button ? 3.6 : 3.9,
            HeadAcrossFlatsMm = head is HeadStyle.Hex or HeadStyle.Flange ? 9.3 : null,
            HeadDiameterMm = head is HeadStyle.SocketCap or HeadStyle.Button ? 9.3 : null,
            FlangeDiameterMm = head == HeadStyle.Flange ? 12.5 : null,
            FlangeThicknessMm = head == HeadStyle.Flange ? 1.6 : null,
            HexSocketAcrossFlatsMm = isTorx ? null : 4.0,
            HexSocketDepthMm = isTorx ? null : 2.2,
            TorxAcrossLobesMm = isTorx ? 4.9 : null,
            TorxDepthMm = isTorx ? 2.2 : null,
            SecurityPinDiameterMm = 1.4,
            TipChamferMm = 0.6,
        };

        Assert.False(PartValidator.Validate(spec).HasErrors,
            string.Join("; ", PartValidator.Validate(spec).Messages.Select(m => m.Message)));
        AssertValidMesh(BoltBuilder.Build(spec, MeshQualityPreset.Draft), minExpectedVolume: 250);
    }

    [Fact]
    public void SecurityPin_AddsVolumeComparedToPlainSocket()
    {
        // پین مادهٔ اضافه در حفره است: حجم PinHex > حجم HexSocket ساده.
        var plain = new BoltSpec
        {
            Head = HeadStyle.SocketCap,
            Drive = DriveStyle.HexSocket,
            Thread = MetricThread(),
            LengthUnderHeadMm = 14,
            HeadHeightMm = 3.9,
            HeadDiameterMm = 9.3,
            HexSocketAcrossFlatsMm = 4.0,
            HexSocketDepthMm = 2.2,
            TipChamferMm = 0.6,
        };
        var pinned = plain with
        {
            Drive = DriveStyle.PinHex,
            SecurityPinDiameterMm = 1.6,
        };

        var plainMesh = BoltBuilder.Build(plain, MeshQualityPreset.Draft);
        var pinnedMesh = BoltBuilder.Build(pinned, MeshQualityPreset.Draft);
        AssertValidMesh(pinnedMesh, minExpectedVolume: 250);

        Assert.True(pinnedMesh.SignedVolumeMm3() > plainMesh.SignedVolumeMm3(),
            $"حجم پین‌دار ({pinnedMesh.SignedVolumeMm3():0.##}) باید از ساده ({plainMesh.SignedVolumeMm3():0.##}) بیشتر باشد.");
    }

    [Fact]
    public void OversizedSecurityPin_IsRejected()
    {
        var hexPin = new BoltSpec
        {
            Head = HeadStyle.SocketCap,
            Drive = DriveStyle.PinHex,
            Thread = MetricThread(),
            LengthUnderHeadMm = 14,
            HeadHeightMm = 3.9,
            HeadDiameterMm = 9.3,
            HexSocketAcrossFlatsMm = 4.0,
            HexSocketDepthMm = 2.2,
            SecurityPinDiameterMm = 2.6, // ≥ نصف آچارخور (۲٫۰).
            TipChamferMm = 0.6,
        };
        Assert.True(PartValidator.Validate(hexPin).HasErrors);

        var torxPin = hexPin with
        {
            Drive = DriveStyle.SecurityTorx,
            HexSocketAcrossFlatsMm = null,
            HexSocketDepthMm = null,
            TorxAcrossLobesMm = 4.9,
            TorxDepthMm = 2.2,
            SecurityPinDiameterMm = 2.0, // ≥ ۰٫۷۲×۴٫۹/۲ = ۱٫۷۶.
        };
        Assert.True(PartValidator.Validate(torxPin).HasErrors);
    }

    [Fact]
    public void SecurityDrive_MissingPinDiameter_IsRejected()
    {
        var spec = new BoltSpec
        {
            Head = HeadStyle.SocketCap,
            Drive = DriveStyle.PinHex,
            Thread = MetricThread(),
            LengthUnderHeadMm = 14,
            HeadHeightMm = 3.9,
            HeadDiameterMm = 9.3,
            HexSocketAcrossFlatsMm = 4.0,
            HexSocketDepthMm = 2.2,
            TipChamferMm = 0.6,
        };

        Assert.True(PartValidator.Validate(spec).HasErrors);
    }
}

using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Sahand.Core.Catalog;
using Sahand.Core.Parts;
using Sahand.Core.Project;
using Sahand.Core.Threading;
using Sahand.Core.Validation;
using Sahand.Geometry.Builders;
using Sahand.Geometry.Meshing;

namespace Sahand.App.Views;

/// <summary>
/// نمای خانه — ایجاد پروژه از نمونه/کاتالوگ و بازکردن فایل ssnd.
/// </summary>
public partial class HomeView : UserControl
{
    private readonly MainWindow _owner;

    public HomeView(MainWindow owner)
    {
        InitializeComponent();
        _owner = owner;
    }

    private async void OnNewBoltClick(object sender, RoutedEventArgs e) =>
        await BuildSampleAsync("M6", PartKind.Bolt, length: 20, head: HeadStyle.Hex, drive: DriveStyle.None);

    private async void OnNewNutClick(object sender, RoutedEventArgs e) =>
        await BuildSampleAsync("M6", PartKind.Nut, length: 4.8);

    private async void OnSampleM6Click(object sender, RoutedEventArgs e) =>
        await BuildSampleAsync("M6", PartKind.Bolt, length: 20, head: HeadStyle.Hex, drive: DriveStyle.None);

    private async void OnSampleSocketClick(object sender, RoutedEventArgs e) =>
        await BuildSampleAsync("M8", PartKind.Bolt, length: 16, head: HeadStyle.SocketCap, drive: DriveStyle.HexSocket);

    private async void OnSampleNutClick(object sender, RoutedEventArgs e) =>
        await BuildSampleAsync("M10", PartKind.Nut, length: 8);

    private async System.Threading.Tasks.Task BuildSampleAsync(
        string size, PartKind kind, double length, HeadStyle head = HeadStyle.Hex, DriveStyle drive = DriveStyle.None)
    {
        var spec = ThreadCatalog.BuildSpec(ThreadFamily.IsoMetric, size,
            kind == PartKind.Nut ? ThreadKind.Internal : ThreadKind.External);
        if (spec is null)
        {
            MessageBox.Show($"اندازهٔ «{size}» در کاتالوگ نیست.", "نمونه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var diameter = spec.NominalDiameterMm;
        var document = kind == PartKind.Nut
            ? new ProjectDocument
            {
                ProjectName = $"مهرهٔ {size}",
                Kind = PartKind.Nut,
                Nut = new NutSpec
                {
                    Thread = spec,
                    AcrossFlatsMm = diameter * 1.55,
                    HeightMm = length,
                    ChamferMm = Math.Min(0.4, diameter * 0.08),
                },
            }
            : new ProjectDocument
            {
                ProjectName = $"پیچ {size} × {length:0.#}",
                Kind = PartKind.Bolt,
                Bolt = new BoltSpec
                {
                    Head = head,
                    Drive = drive,
                    Thread = spec,
                    LengthUnderHeadMm = length,
                    HeadHeightMm = diameter * 0.65,
                    HeadAcrossFlatsMm = head == HeadStyle.Hex ? diameter * 1.55 : null,
                    HeadDiameterMm = head is HeadStyle.SocketCap or HeadStyle.Pan or HeadStyle.FlatCountersunk ? diameter * 1.55 : null,
                    HexSocketAcrossFlatsMm = drive == DriveStyle.HexSocket ? diameter * 0.55 : null,
                    HexSocketDepthMm = drive == DriveStyle.HexSocket ? diameter * 0.35 : null,
                    TipChamferMm = spec.PitchMm * 0.6,
                },
            };

        var validation = document.Kind == PartKind.Nut
            ? PartValidator.Validate(document.Nut!)
            : PartValidator.Validate(document.Bolt!);
        if (validation.HasErrors)
        {
            MessageBox.Show(string.Join("\n", validation.Messages
                    .Where(m => m.Severity == ValidationSeverity.Error).Select(m => m.Message)),
                "نمونهٔ نامعتبر", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var mesh = await System.Threading.Tasks.Task.Run(() =>
                PartMeshFactory.Build(document, MeshQualityPreset.Balanced));
            _owner.Session.Document = document;
            _owner.Session.NominalMesh = mesh;
            _owner.Session.PrintMesh = null;
            _owner.Session.LastPrintDiff = null;
            _owner.Session.OpenFilePath = null;
            _owner.Services.Autosave.WriteRecovery(document);

            MessageBox.Show(
                $"نمونهٔ «{document.ProjectName}» ساخته شد ({mesh.Triangles.Length:N0} وجه). به «طراحی قطعه» بروید تا ابعاد را ویرایش کنید.",
                "نمونه ساخته شد", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ساخت نمونه ناموفق بود: {ex.Message}", "خطا",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnOpenClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "بازکردن پروژهٔ سهند",
            Filter = "پروژهٔ سهند (ssnd)|*.ssnd|همهٔ فایل‌ها|*.*",
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var document = Core.Project.SsndFile.Load(dialog.FileName);
            var mesh = PartMeshFactory.Build(document, MeshQualityPreset.Balanced);

            _owner.Session.Document = document;
            _owner.Session.NominalMesh = mesh;
            _owner.Session.PrintMesh = null;
            _owner.Session.LastPrintDiff = null;
            _owner.Session.OpenFilePath = dialog.FileName;

            MessageBox.Show(
                $"پروژهٔ «{document.ProjectName}» باز شد ({mesh.Triangles.Length:N0} وجه).",
                "پروژه باز شد", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            // فایل آسیب‌دیده: پیام امن و بدون جزئیات فنی خام (سند ۴-۶).
            MessageBox.Show(
                $"بازکردن پروژه ممکن نشد:\n{ex.Message}\n\nاگر فایل نسخهٔ پشتیبان دارد، از آن استفاده کنید.",
                "خطای بازکردن", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

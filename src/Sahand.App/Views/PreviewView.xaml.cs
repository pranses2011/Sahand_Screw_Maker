using System.Windows;
using System.Windows.Controls;
using Sahand.App.Controls;
using Sahand.Geometry.Appearance;
using Sahand.Geometry.Meshing;

namespace Sahand.App.Views;

/// <summary>
/// نمای پیش‌نمایش سه‌بعدی — نمایش مش اسمی/چاپی + اعتبارسنجی هندسی + متریال نمایشی.
/// </summary>
/// <remarks>
/// اعتبارسنجی مش (watertight/جهت‌دهی/تقاطع) در پس‌زمینه اجرا می‌شود تا UI قفل نشود.
/// نسخهٔ چاپی فقط اگر از بخش «آماده‌سازی چاپ» ساخته باشد نمایش داده می‌شود.
/// </remarks>
public partial class PreviewView : UserControl
{
    private readonly MainWindow _owner;

    public PreviewView(MainWindow owner)
    {
        InitializeComponent();
        _owner = owner;

        foreach (var material in MaterialAppearance.Presets)
        {
            MaterialBox.Items.Add(material.PersianName);
        }
        MaterialBox.SelectedIndex = 0;

        Loaded += (_, _) => RefreshDisplay();
    }

    private void OnVersionChanged(object sender, RoutedEventArgs e)
    {
        // NominalRadio در XAML با IsChecked=True است؛ رخداد Checked در حین
        // InitializeComponent پیش از ساخت PrintRadio/Viewport می‌گیرد — گارد بارگذاری (بازبینی ۰٫۲٫۰، D2).
        if (PrintRadio is null || Viewport is null) return;
        RefreshDisplay();
    }

    private void OnMaterialChanged(object sender, SelectionChangedEventArgs e)
    {
        // همان گارد برای رخداد زودهنگام/تغییر متریال پیش از آمادگی نما.
        if (PrintRadio is null || Viewport is null) return;
        RefreshDisplay();
    }

    private Mesh? SelectedMesh => PrintRadio.IsChecked == true
        ? _owner.Session.PrintMesh ?? _owner.Session.NominalMesh
        : _owner.Session.NominalMesh;

    /// <summary>نمایش مجدد مدل انتخابی با رنگ متریال و به‌روزرسانی خلاصه‌ها.</summary>
    private async void RefreshDisplay()
    {
        // کل مسیر UI پوشش‌دار است تا خطای نمایش، برنامه را نبندد (بازبینی ۰٫۲٫۰، D5).
        try
        {
            await RefreshDisplayCore();
        }
        catch (Exception ex)
        {
            try
            {
                ValidationInfoText.Text = $"نمایش ناموفق: {ex.Message}";
            }
            catch
            {
                // نما در حال بسته‌شدن است؛ خروج بی‌صدا امن است.
            }
        }
    }

    private async System.Threading.Tasks.Task RefreshDisplayCore()
    {
        var mesh = SelectedMesh;
        var isPrint = PrintRadio.IsChecked == true && _owner.Session.PrintMesh is not null;

        if (mesh is null)
        {
            Viewport.Clear();
            ModelInfoText.Text = "مدلی ساخته نشده است. از بخش «طراحی قطعه» مدل بسازید.";
            ValidationInfoText.Text = "—";
            return;
        }

        // رنگ متریال نمایشی (فقط ظاهر).
        var material = MaterialAppearance.Presets[Math.Max(0, MaterialBox.SelectedIndex)];
        Viewport.MeshColor = System.Windows.Media.Color.FromRgb(material.R, material.G, material.B);
        Viewport.ShowMesh(mesh);

        var document = _owner.Session.Document;
        var bounds = mesh.Bounds();
        ModelInfoText.Text =
            $"پروژه: {document?.ProjectName ?? "—"}\n" +
            $"نسخه: {(isPrint ? "بهینه‌شده برای چاپ" : "اسمی")}\n" +
            $"وجه‌ها: {mesh.Triangles.Length:N0}\nرأس‌ها: {mesh.Vertices.Length:N0}\n" +
            $"حجم تقریبی: {mesh.SignedVolumeMm3():0.##} mm³\n" +
            $"مساحت سطح: {mesh.TotalSurfaceAreaMm2():0.##} mm²\n" +
            $"جعبهٔ محیطی: {bounds.MaxX - bounds.MinX:0.##}×{bounds.MaxY - bounds.MinY:0.##}×{bounds.MaxZ - bounds.MinZ:0.##} mm";

        ValidationInfoText.Text = "در حال بررسی…";
        try
        {
            // اعتبارسنجی در پس‌زمینه — بررسی تقاطع خودی فعال است (خروجی چاپ).
            var result = await System.Threading.Tasks.Task.Run(() =>
                MeshValidator.Validate(mesh, checkSelfIntersection: true));

            ValidationInfoText.Text =
                $"بسته (watertight): {(result.IsWatertight ? "بله ✓" : "خیر ✗")}\n" +
                $"جهت‌دهی وجه‌ها: {(result.HasConsistentOrientation ? "سالم ✓" : "ناسازگار ✗")}\n" +
                $"تقاطع خودی: {result.SelfIntersections} مورد {(result.SelfIntersections == 0 ? "✓" : "(بررسی هیوریستیکی)")}\n" +
                $"وجه‌های بی‌مساحت: {(result.HasZeroAreaTriangles ? "دارد (هشدار)" : "ندارد ✓")}";
        }
        catch (Exception ex)
        {
            ValidationInfoText.Text = $"بررسی ناموفق: {ex.Message}";
        }
    }
}

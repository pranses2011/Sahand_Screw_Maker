using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Sahand.Core.Parts;
using Sahand.Core.Printing;
using Sahand.Core.Project;
using Sahand.Core.Threads;
using Sahand.Core.Units;
using Sahand.Geometry.Builders;
using Sahand.Geometry.Meshing;

namespace Sahand.App.Views;

/// <summary>
/// نمای آماده‌سازی چاپ — ساخت نسخهٔ مشتق‌شدهٔ چاپی از روی پروژهٔ اسمی (سند ۴-۲).
/// </summary>
/// <remarks>
/// مدل اسمی دست‌نخورده می‌ماند؛ نسخهٔ چاپی در Session.PrintMesh جدا نگه داشته می‌شود.
/// نمونهٔ آزمون رزوه (کوپن) با همان مشخصات رزوهٔ پروژه ساخته و برچسب آزمون می‌گیرد.
/// </remarks>
public partial class PrintView : UserControl, INotifyPropertyChanged
{
    private readonly MainWindow _owner;

    /// <summary>ردیف‌های جدول تفاوت.</summary>
    public ObservableCollection<PrintDiffRow> DiffRows { get; } = [];

    private Visibility _diffVisibility = Visibility.Collapsed;

    /// <summary>نمایش جدول تفاوت پس از اولین ساخت نسخهٔ چاپی.</summary>
    public Visibility DiffVisibility
    {
        get => _diffVisibility;
        private set
        {
            _diffVisibility = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DiffVisibility)));
        }
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    public PrintView(MainWindow owner)
    {
        InitializeComponent();
        _owner = owner;
        DiffGrid.ItemsSource = DiffRows;
    }

    private PrintProfile BuildProfileFromForm()
    {
        var fit = ((ComboBoxItem)FitBox.SelectedItem)?.Tag as string ?? "Snug";
        var process = (((ComboBoxItem)ProcessBox.SelectedItem)?.Tag as string) switch
        {
            "Sla" => PrintProcess.Sla,
            "Sls" => PrintProcess.Sls,
            _ => PrintProcess.Fdm,
        };

        return new PrintProfile
        {
            Name = "پروفایل جاری",
            Process = process,
            NozzleDiameterMm = Parse(NozzleBox.Text) ?? 0.4,
            LayerHeightMm = Parse(LayerBox.Text) ?? 0.2,
            FitMode = fit switch
            {
                "Loose" => PrintFitMode.Loose,
                "Press" => PrintFitMode.Press,
                "Custom" => PrintFitMode.Custom,
                _ => PrintFitMode.Snug,
            },
            CustomClearanceMm = Parse(CustomClearanceBox.Text) ?? 0.2,
            ExternalThreadRadialCompensationMm = Parse(ExternalCompBox.Text) ?? 0,
            InternalThreadRadialCompensationMm = Parse(InternalCompBox.Text) ?? 0,
            ThreadLeadInChamferMm = Parse(LeadInBox.Text) ?? 0,
            ShrinkagePercent = Parse(ShrinkBox.Text) ?? 0,
            ApplyShrinkage = ApplyShrinkCheck.IsChecked == true,
        };
    }

    private async void OnDeriveClick(object sender, RoutedEventArgs e)
    {
        var document = _owner.Session.Document;
        if (document?.Bolt is null)
        {
            MessageBox.Show(
                "ابتدا از بخش «طراحی قطعه» یک پیچ (یا مجموعه) بسازید؛ نسخهٔ چاپی از روی رزوهٔ پیچ مشتق می‌شود.",
                "پروژهٔ پیچ موجود نیست",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            var profile = BuildProfileFromForm();
            var adjusted = await System.Threading.Tasks.Task.Run(() =>
                PrintAdjuster.Adjust(document.Bolt, document.Assembly?.Nut ?? document.Nut, profile));

            // ساخت مش نسخهٔ چاپی در پس‌زمینه.
            var printDoc = new ProjectDocument
            {
                ProjectName = adjusted.Bolt.Name,
                Kind = document.Kind,
                Bolt = adjusted.Bolt,
                Nut = adjusted.Nut ?? document.Nut,
                Washer = document.Washer,
                Assembly = document.Assembly is null ? null : document.Assembly with
                {
                    Bolt = adjusted.Bolt,
                    Nut = adjusted.Nut ?? document.Assembly.Nut,
                },
            };

            var mesh = await System.Threading.Tasks.Task.Run(() =>
                PartMeshFactory.Build(printDoc, MeshQualityPreset.Balanced));

            _owner.Session.PrintMesh = mesh;
            _owner.Session.LastPrintDiff = adjusted.Diff;

            DiffRows.Clear();
            foreach (var row in adjusted.Diff)
                DiffRows.Add(row);
            DiffVisibility = Visibility.Visible;

            MessageBox.Show(
                $"نسخهٔ چاپی ساخته شد ({mesh.Triangles.Length:N0} وجه).\nجدول تفاوت را بازبینی کنید؛ مدل اسمی دست‌نخورده است.",
                "نسخهٔ چاپی",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ساخت نسخهٔ چاپی ناموفق بود:\n{ex.Message}",
                "خطا", MessageBoxButton.OK, MessageBoxImage.Error);

            if (_owner.Services.Settings.Current.AutoReportEnabled)
                _owner.Services.Reports.ReportError("چاپ", ex.Message, userConsented: true);
        }
    }

    private async void OnCouponClick(object sender, RoutedEventArgs e)
    {
        var document = _owner.Session.Document;
        var thread = document?.Bolt?.Thread ?? document?.Assembly?.Bolt.Thread;
        if (thread is null)
        {
            MessageBox.Show("ابتدا قطعه‌ای با رزوه بسازید.", "رزوه‌ای موجود نیست",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            // کوپن: ۴ گام رزوه با همان مشخصات — برچسب صریح «نمونهٔ آزمون» (سند ۴-۲).
            var couponLength = Math.Max(4.0 * thread.PitchMm, 4.0);
            var rod = new ThreadedRodSpec
            {
                Name = $"نمونهٔ آزمون رزوه d={thread.NominalDiameterMm:0.###} P={thread.PitchMm:0.###} (استاندارد نیست)",
                Thread = thread,
                TotalLengthMm = couponLength,
                EndChamferMm = thread.PitchMm * 0.5,
            };

            var mesh = await System.Threading.Tasks.Task.Run(() =>
                ThreadedRodBuilder.Build(rod, MeshQualityPreset.Fine));

            _owner.Session.PrintMesh = mesh;
            _owner.Session.LastPrintDiff =
            [
                new PrintDiffRow("نمونهٔ آزمون رزوه", thread.PitchMm * 4, couponLength,
                    "کوپن کوتاه برای تنظیم فیت — استاندارد تولیدی نیست"),
            ];

            DiffRows.Clear();
            DiffRows.Add(_owner.Session.LastPrintDiff[0]);
            DiffVisibility = Visibility.Visible;

            MessageBox.Show(
                "نمونهٔ آزمون رزوه ساخته شد. برای خروجی، به بخش «خروجی» بروید.\nاین قطعه صرفاً برای تنظیم فیت چاپ است.",
                "نمونهٔ آزمون",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ساخت نمونهٔ آزمون ناموفق بود:\n{ex.Message}",
                "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static double? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = PersianDigits.NormalizeDigits(text.Trim()).Replace('٫', '.');
        return double.TryParse(normalized, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}

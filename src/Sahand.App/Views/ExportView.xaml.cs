using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Sahand.Core.Units;
using Sahand.Exporters;
using Sahand.Geometry.Builders;
using Sahand.Geometry.Meshing;

namespace Sahand.App.Views;

/// <summary>
/// نمای خروجی — ماتریس قابلیت صادقانه، انتخاب کیفیت/واحد/مسیر و اجرای خروجی‌گیرها.
/// </summary>
/// <remarks>
/// فرمت‌های «برنامهٔ توسعه» با دلیل فارسی غیرفعال می‌مانند (EXP-09). جلوگیری از
/// overwrite: اگر فایل مقصد وجود داشته باشد، تأیید صریح کاربر خواسته می‌شود (EXP-05).
/// پس از نوشتن، وجود/اندازهٔ فایل کنترل و هشدارهای فرمت نمایش داده می‌شود.
/// </remarks>
public partial class ExportView : UserControl
{
    private readonly MainWindow _owner;
    private readonly Dictionary<string, RadioButton> _formatButtons = new(StringComparer.OrdinalIgnoreCase);
    private string _selectedFormat = "STL";

    public ExportView(MainWindow owner)
    {
        InitializeComponent();
        _owner = owner;
        BuildFormatList();
        UpdateDefaultPath();
        // وضعیت قطعی پس از بارگذاری کامل XAML — رخداد زودهنگام SelectionChanged (حین InitializeComponent)
        // با گارد null کنار گذاشته می‌شود (الگوی رفع کرش ۰٫۲٫۰).
        UpdateQualityNote();
    }

    /// <summary>ساخت فهرست فرمت‌ها از ماتریس قابلیت — منبع واحد حقیقت.</summary>
    private void BuildFormatList()
    {
        foreach (var info in ExportCapabilityMatrix.Formats)
        {
            var isEnabled = ExportCapabilityMatrix.IsExportable(info.FormatKey);

            var radio = new RadioButton
            {
                GroupName = "Format",
                IsEnabled = isEnabled,
                Margin = new Thickness(0, 3, 0, 3),
            };

            var statusLabel = info.Status switch
            {
                ExportCapabilityStatus.Supported => "پشتیبانی‌شده",
                ExportCapabilityStatus.Limited => "محدود",
                ExportCapabilityStatus.Planned => "در برنامهٔ توسعه",
                ExportCapabilityStatus.RequiresLicense => "وابسته به مجوز",
                _ => "—",
            };

            // متن فرمت + وضعیت + دلیل (شفافیت ماتریس قابلیت سند ۴-۳).
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock
            {
                Text = $"{info.PersianName} — {statusLabel}",
                VerticalAlignment = VerticalAlignment.Center,
            });
            if (!isEnabled)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $" · {info.StatusReason}",
                    Foreground = (System.Windows.Media.Brush)FindResource("Brush.TextSecondary"),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0),
                    ToolTip = info.UnitNote,
                });
            }

            radio.Content = panel;
            radio.Tag = info.FormatKey;
            radio.Checked += OnFormatChecked;
            if (info.FormatKey == "STL") radio.IsChecked = true;

            FormatListPanel.Children.Add(radio);
            _formatButtons[info.FormatKey] = radio;
        }
    }

    private void OnFormatChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string key } && ((RadioButton)sender).IsChecked == true)
        {
            _selectedFormat = key;
            UpdateDefaultPath();
        }
    }

    private void UpdateDefaultPath()
    {
        var folder = _owner.Services.Settings.Current.LastExportFolder;
        var baseName = System.IO.Path.GetFileNameWithoutExtension(
            _owner.Session.OpenFilePath ?? "part") ?? "part";
        var extension = ExportCapabilityMatrix.Find(_selectedFormat)?.Extension ?? "bin";
        PathBox.Text = System.IO.Path.Combine(folder, $"{baseName}-اسمی.{extension}");
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "انتخاب مسیر خروجی",
            FileName = System.IO.Path.GetFileName(PathBox.Text),
            Filter = BuildFilter(),
            OverwritePrompt = false, // کنترل بازنویسی خودمان انجام می‌شود (EXP-05).
        };
        if (dialog.ShowDialog() == true)
        {
            PathBox.Text = dialog.FileName;
        }
    }

    private string BuildFilter()
    {
        var filters = ExportCapabilityMatrix.Formats
            .Where(f => ExportCapabilityMatrix.IsExportable(f.FormatKey))
            .Select(f => $"{f.PersianName}|*.{f.Extension}");
        return string.Join("|", filters) + "|همهٔ فایل‌ها|*.*";
    }

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        var session = _owner.Session;

        // نسخهٔ چاپی فقط اگر ساخته شده باشد؛ در غیر این صورت برگشت به اسمی.
        var wantsPrint = PrintRadio.IsChecked == true;
        if (session.NominalMesh is null && session.PrintMesh is null)
        {
            MessageBox.Show("مدلی برای خروجی وجود ندارد؛ ابتدا از «طراحی قطعه» مدل بسازید.",
                "مدل موجود نیست", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var path = PathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show("مسیر فایل خروجی خالی است.", "مسیر نامعتبر",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // جلوگیری از overwrite بی‌اجازه (EXP-05).
        if (System.IO.File.Exists(path))
        {
            var overwrite = MessageBox.Show(
                $"فایل «{System.IO.Path.GetFileName(path)}» وجود دارد. بازنویسی شود؟",
                "بازنویسی فایل",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);
            if (overwrite != MessageBoxResult.Yes) return;

            // حذف ایمن پیش از نوشتن اتمیک.
            try { System.IO.File.Delete(path); }
            catch (Exception ex)
            {
                MessageBox.Show($"حذف فایل قبلی ممکن نشد: {ex.Message}", "خطا",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        var quality = ParseQuality();
        var qualityTag = ToMeshPreset(quality);

        // انتخاب نسخهٔ خروجی: نسخهٔ چاپی از مش موجود PrintMesh؛ اسمی از Document با کیفیت انتخابی.
        Mesh? mesh;
        var rebuilt = false;
        if (wantsPrint && session.PrintMesh is not null)
        {
            mesh = session.PrintMesh;
        }
        else if (session.Document is not null)
        {
            // بازسازی با کیفیت انتخابی — کیفیت انتخابی واقعاً روی فایل اعمال می‌شود (گام ۰٫۴٫۰):
            // مش Session با کیفیت ساخت پیش‌نمایش ساخته شده؛ خروجی دوباره ساخته می‌شود.
            // snapshot از سند برای ایمنی closure در برابر تغییر هم‌زمان توسط کاربر.
            var doc = session.Document;
            ResultText.Text = $"وضعیت: در حال ساخت هندسه با کیفیت «{QualityLabel(quality)}»…";
            ResultDetailsText.Text = "قطعه با تفکیک انتخابی بازسازی می‌شود — برای کیفیت‌های سنگین چند لحظه صبر کنید.";
            try
            {
                mesh = await System.Threading.Tasks.Task.Run(() =>
                    PartMeshFactory.Build(doc, qualityTag));
                rebuilt = true;
            }
            catch (Exception ex)
            {
                ResultText.Text = "وضعیت: خروجی ناموفق ✗";
                ResultDetailsText.Text = $"بازسازی مش با کیفیت انتخابی ناموفق بود:\n{ex.Message}";
                return;
            }
        }
        else
        {
            mesh = session.NominalMesh;
        }

        if (mesh is null || mesh.Triangles.Length == 0)
        {
            MessageBox.Show("مدلی برای خروجی وجود ندارد؛ ابتدا از «طراحی قطعه» مدل بسازید.",
                "مدل موجود نیست", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ResultText.Text = "وضعیت: در حال نوشتن فایل…";
        ResultDetailsText.Text = string.Empty;

        try
        {
            var options = new ExportOptions
            {
                FormatKey = _selectedFormat,
                Quality = ParseQuality(),
                TargetUnit = ((ComboBoxItem)UnitBox.SelectedItem)?.Tag as string == "Inch"
                    ? LengthUnit.Inch
                    : LengthUnit.Millimeter,
                PartName = _owner.Session.Document?.ProjectName ?? "قطعه",
                AsciiStl = AsciiCheck.IsChecked == true,
                AsciiPly = AsciiCheck.IsChecked == true,
            };

            var exporter = new ExportManager();

            // خروجی در پس‌زمینه — فایل‌های بزرگ UI را قفل نمی‌کنند (سند ۴-۵).
            var result = await System.Threading.Tasks.Task.Run(() =>
                exporter.ExportMesh(mesh, path, options, FirstThreadSpec()));

            // به‌روزرسانی پوشهٔ آخر در تنظیمات.
            _owner.Services.Settings.Save(_owner.Services.Settings.Current with
            {
                LastExportFolder = System.IO.Path.GetDirectoryName(path)
                    ?? _owner.Services.Settings.Current.LastExportFolder,
            });

            ResultText.Text = $"وضعیت: خروجی موفق ✓ — {result.Format} ({result.FileBytes:N0} بایت)";
            ResultDetailsText.Text =
                $"مسیر: {result.FilePath}\nواحد ثبت‌شده: {result.UnitNote}\nکیفیت: {result.Quality}" +
                (rebuilt
                    ? $"\nوجه‌های فایل: {mesh.Triangles.Length:N0} — رأس‌ها: {mesh.Vertices.Length:N0} (بازسازی با تفکیک انتخابی)"
                    : "\nنسخهٔ چاپی: مش موجود به‌عنوان‌هست استفاده شد (کیفیت ساخت نسخهٔ چاپی).") +
                (qualityTag is MeshQualityPreset.Ultra or MeshQualityPreset.Extreme
                    ? "\nکیفیت سنگین: فایل بزرگ است؛ برای چاپ قطعات ریز/نمایش نهایی مناسب است."
                    : "");
            if (result.Warnings.Count > 0)
            {
                ResultDetailsText.Text += "\nهشدارها:\n• " + string.Join("\n• ", result.Warnings);
            }
        }
        catch (Exception ex)
        {
            ResultText.Text = "وضعیت: خروجی ناموفق ✗";
            ResultDetailsText.Text = ex.Message;

            if (_owner.Services.Settings.Current.AutoReportEnabled)
                _owner.Services.Reports.ReportError("خروجی", ex.Message, userConsented: true);
        }
    }

    private async void OnDxfClick(object sender, RoutedEventArgs e)
    {
        var thread = FirstThreadSpec();
        if (thread is null)
        {
            MessageBox.Show("رزوه‌ای برای نقشهٔ پروفیل وجود ندارد.", "رزوه موجود نیست",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "خروجی DXF پروفیل رزوه",
            FileName = "thread-profile.dxf",
            Filter = "DXF دوبعدی|*.dxf",
            OverwritePrompt = false,
        };
        if (dialog.ShowDialog() != true) return;

        if (System.IO.File.Exists(dialog.FileName) &&
            MessageBox.Show("فایل موجود است؛ بازنویسی شود؟", "بازنویسی",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            if (System.IO.File.Exists(dialog.FileName)) System.IO.File.Delete(dialog.FileName);

            var exporter = new ExportManager();
            var result = await System.Threading.Tasks.Task.Run(() =>
                exporter.ExportThreadProfileDxf(thread, dialog.FileName,
                    _owner.Session.Document?.ProjectName ?? "پروفیل رزوه"));

            ResultText.Text = $"وضعیت: خروجی DXF موفق ✓ ({result.FileBytes:N0} بایت)";
            ResultDetailsText.Text = $"مسیر: {result.FilePath}\n" + string.Join("\n", result.Warnings);
        }
        catch (Exception ex)
        {
            ResultText.Text = "وضعیت: خروجی DXF ناموفق ✗";
            ResultDetailsText.Text = ex.Message;
        }
    }

    private Core.Threads.ThreadSpec? FirstThreadSpec() => _owner.Session.Document?.Kind switch
    {
        Core.Parts.PartKind.Bolt => _owner.Session.Document.Bolt?.Thread,
        Core.Parts.PartKind.Nut => _owner.Session.Document.Nut?.Thread,
        Core.Parts.PartKind.ThreadedRod => _owner.Session.Document.ThreadedRod?.Thread,
        Core.Parts.PartKind.Assembly => _owner.Session.Document.Assembly?.Bolt.Thread,
        _ => null,
    };

    private ExportQualityPreset ParseQuality() => (((ComboBoxItem)QualityBox.SelectedItem)?.Tag as string) switch
    {
        "Draft" => ExportQualityPreset.Draft,
        "Fine" => ExportQualityPreset.Fine,
        "VeryFine" => ExportQualityPreset.VeryFine,
        "Ultra" => ExportQualityPreset.Ultra,
        "Extreme" => ExportQualityPreset.Extreme,
        _ => ExportQualityPreset.Balanced,
    };

    /// <summary>نگاشت preset گزارش خروجی به preset واقعی موتور مش.</summary>
    private static MeshQualityPreset ToMeshPreset(ExportQualityPreset preset) => preset switch
    {
        ExportQualityPreset.Draft => MeshQualityPreset.Draft,
        ExportQualityPreset.Fine => MeshQualityPreset.Fine,
        ExportQualityPreset.VeryFine => MeshQualityPreset.VeryFine,
        ExportQualityPreset.Ultra => MeshQualityPreset.Ultra,
        ExportQualityPreset.Extreme => MeshQualityPreset.Extreme,
        _ => MeshQualityPreset.Balanced,
    };

    /// <summary>برچسب فارسی کیفیت برای پیام وضعیت.</summary>
    private static string QualityLabel(ExportQualityPreset preset) => preset switch
    {
        ExportQualityPreset.Draft => "پیش‌نویس",
        ExportQualityPreset.Fine => "دقیق",
        ExportQualityPreset.VeryFine => "بسیار دقیق",
        ExportQualityPreset.Ultra => "فوق‌دقیق",
        ExportQualityPreset.Extreme => "حدی",
        _ => "متعادل",
    };

    /// <summary>یادداشت راهنمای کیفیت انتخابی — بخش‌های زاویه‌ای و سنگینی هر سطح.</summary>
    private void OnQualityChanged(object sender, SelectionChangedEventArgs e)
    {
        // رخداد زودهنگام حین InitializeComponent (آیتم Fine دارای IsSelected است) — گارد null.
        if (QualityNoteText is null) return;
        UpdateQualityNote();
    }

    private void UpdateQualityNote() => QualityNoteText.Text = ParseQuality() switch
    {
        ExportQualityPreset.Draft => "۴۸ بخش زاویه‌ای — بازبینی سریع؛ رزوه درشت نمایش داده می‌شود.",
        ExportQualityPreset.Fine => "۱۹۲ بخش زاویه‌ای — پیش‌فرض؛ تعادل نرمی و اندازهٔ فایل.",
        ExportQualityPreset.VeryFine => "۳۲۰ بخش زاویه‌ای — رزوهٔ نرم؛ فایل و زمان ساخت سنگین‌تر.",
        ExportQualityPreset.Ultra => "۵۱۲ بخش زاویه‌ای و گام مارپیچ ریزتر — نرمی عالی برای چاپ قطعات ریز؛ خیلی سنگین.",
        ExportQualityPreset.Extreme => "۷۶۸ بخش زاویه‌ای و ریزترین گام — بیشترین نرمی قطعه؛ مناسب رندر/چاپ نهایی؛ سنگین‌ترین حالت.",
        _ => "۹۶ بخش زاویه‌ای — تعادل سرعت و کیفیت.",
    };
}

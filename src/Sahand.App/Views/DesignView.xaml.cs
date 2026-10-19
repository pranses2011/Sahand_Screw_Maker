using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Sahand.Core.Catalog;
using Sahand.Core.Parts;
using Sahand.Core.Threads;
using Sahand.Core.Units;
using Sahand.Core.Validation;
using Sahand.Core.Project;
using ValidationResult = Sahand.Core.Validation.ValidationResult;
using Sahand.Geometry.Builders;
using Sahand.Geometry.Meshing;

namespace Sahand.App.Views;

/// <summary>
/// نمای طراحی قطعه — ورودی پارامترها، اعتبارسنجی زنده و ساخت مش در پس‌زمینه.
/// </summary>
/// <remarks>
/// ساخت مش رزوهٔ واقعی سنگین است؛ در Task پس‌زمینه اجرا می‌شود و UI قفل نمی‌شود (GEO-09).
/// نتیجهٔ قدیمی پس از تغییر پارامتر نمایش داده نمی‌شود (کلید نسخه = زمان ساخت).
/// </remarks>
public partial class DesignView : UserControl
{
    private readonly MainWindow _owner;
    private int _buildTicket; // شمارهٔ ساخت — پاسخ‌های قدیمی کنار گذاشته می‌شوند.

    /// <summary>پیام‌های اعتبارسنجی برای نمایش.</summary>
    public ObservableCollection<string> ValidationMessages { get; } = [];

    public DesignView(MainWindow owner)
    {
        InitializeComponent();
        _owner = owner;
        // وضعیت قطعی پس از بارگذاری کامل XAML — رخداد زودهنگام SelectionChanged
        // در حین InitializeComponent کنار گذاشته می‌شود و اینجا دوباره اعمال می‌شود (رفع کرش ۰٫۲٫۰).
        UpdateHeadGroupState();
        // اتصال در XAML (ItemsSource روی ItemsControl) پس از InitializeComponent معتبر است.
        Loaded += (_, _) => RefreshSizes();
    }

    private string SelectedKind => ((ComboBoxItem)PartKindBox.SelectedItem)?.Tag as string ?? "Bolt";

    /// <summary>پرکردن فهرست اندازه‌های کاتالوگ متریک با یادداشت منبع.</summary>
    private void RefreshSizes()
    {
        SizeBox.Items.Clear();
        foreach (var entry in ThreadCatalog.GetEntries(ThreadFamily.IsoMetric))
        {
            var item = new ComboBoxItem
            {
                Content = entry.SizeLabel,
                Tag = entry,
            };
            SizeBox.Items.Add(item);
        }

        if (SizeBox.Items.Count > 0) SizeBox.SelectedIndex = 0;
    }

    private void OnPartKindChanged(object sender, SelectionChangedEventArgs e)
    {
        // در حین InitializeComponent آیتم «پیچ/بولت» IsSelected دارد و رخداد پیش از ساخت
        // عناصر بعدی XAML (مثل HeadGroup) می‌گیرد — بدون این گارد، کرش برنامه (بازبینی ۰٫۲٫۰، D1).
        if (HeadGroup is null) return;
        UpdateHeadGroupState();
    }

    /// <summary>فیلدهای سر فقط برای پیچ/مجموعه معنا دارند.</summary>
    private void UpdateHeadGroupState()
    {
        var isBolt = SelectedKind is "Bolt" or "Assembly";
        HeadGroup.IsEnabled = isBolt;
        HeadGroup.Opacity = isBolt ? 1.0 : 0.45;
    }

    private void OnSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        // رخداد زودهنگام در حین InitializeComponent ممکن است؛ فیلدها هنوز ساخته نشده‌اند.
        if (SourceNoteText is null || DiameterBox is null) return;
        ApplyCatalogDefaults();
    }

    /// <summary>
    /// مقادیر پیش‌فرض هوشمند بر اساس مدخل کاتالوگ — با قالب عددی ثابت (InvariantCulture)
    /// تا روی ویندوزهای با منطقهٔ فارسی ممیز اعشار «/» یا «٫» درج نشود (بازبینی ۰٫۲٫۰، D4).
    /// </summary>
    private void ApplyCatalogDefaults()
    {
        if (SizeBox.SelectedItem is not ComboBoxItem { Tag: CatalogEntry entry }) return;

        SourceNoteText.Text = entry.SourceNote;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        DiameterBox.Text = entry.NominalDiameterMm.ToString("0.###", inv);
        PitchBox.Text = entry.PitchMm.ToString("0.###", inv);
        AcrossFlatsBox.Text = (entry.NominalDiameterMm * 1.55).ToString("0.###", inv);
        HeadHeightBox.Text = (entry.NominalDiameterMm * 0.65).ToString("0.###", inv);
        HeadDiameterBox.Text = (entry.NominalDiameterMm * 1.55).ToString("0.###", inv);
    }

    private async void OnBuildClick(object sender, RoutedEventArgs e)
    {
        ValidationMessages.Clear();
        var ticket = ++_buildTicket;

        try
        {
            var document = BuildDocumentFromForm();
            if (document is null) return;

            var validation = ValidateDocument(document);
            foreach (var message in validation.Messages)
            {
                var label = message.Severity == ValidationSeverity.Error ? "خطا" : "هشدار";
                ValidationMessages.Add($"{label}: {message.Message}");
            }

            if (validation.HasErrors)
            {
                ValidationSummaryText.Text = "وضعیت: ساخت انجام نشد — خطاها را اصلاح کنید.";
                return;
            }

            ValidationSummaryText.Text = "وضعیت: در حال ساخت هندسه…";
            BuildButton.IsEnabled = false;

            // ساخت در پس‌زمینه — رشتهٔ UI آزاد می‌ماند (GEO-09).
            var mesh = await System.Threading.Tasks.Task.Run(() =>
                PartMeshFactory.Build(document, MeshQualityPreset.Balanced));

            // پاسخ قدیمی کنار گذاشته می‌شود (کاربر دوباره کلیک کرده).
            if (ticket != _buildTicket) return;

            _owner.Session.Document = document;
            _owner.Session.NominalMesh = mesh;
            _owner.Session.PrintMesh = null;
            _owner.Session.LastPrintDiff = null;

            // Autosafe پس از هر ساخت موفق.
            _owner.Services.Autosave.WriteRecovery(document);

            var bounds = mesh.Bounds();
            ValidationSummaryText.Text = "وضعیت: مدل ساخته شد ✓ (نسخهٔ اسمی)";
            MeshStatsText.Text =
                $"وجه‌ها: {mesh.Triangles.Length:N0} — رأس‌ها: {mesh.Vertices.Length:N0} — " +
                $"حجم تقریبی: {mesh.SignedVolumeMm3():0.##} mm³ — " +
                $"ابعاد: {bounds.MaxX - bounds.MinX:0.##}×{bounds.MaxY - bounds.MinY:0.##}×{bounds.MaxZ - bounds.MinZ:0.##} mm " +
                $"(حجم/ابعاد «تقریبی» است و گواهی مهندسی نیست)";
        }
        catch (Exception ex)
        {
            ValidationSummaryText.Text = "وضعیت: خطا در ساخت مدل";
            ValidationMessages.Add($"خطای ساخت: {ex.Message}");

            // ثبت خطا در صف گزارش فقط با رضایت کاربر (پیش‌فرض خاموش — سند ۵-۴).
            if (_owner.Services.Settings.Current.AutoReportEnabled)
                _owner.Services.Reports.ReportError("هندسه", ex.Message, userConsented: true);
        }
        finally
        {
            BuildButton.IsEnabled = true;
        }
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        LengthBox.Text = "20";
        ThreadRunoutBox.Text = string.Empty;
        StartsBox.Text = "1";
        HandednessBox.SelectedIndex = 0;
        ModelingModeBox.SelectedIndex = 0;
        // به‌جای فراخوانی مستقیم رخدادگردان با آرگومان null، منطق مشترک صدا زده می‌شود (بازبینی ۰٫۲٫۰، D9).
        ApplyCatalogDefaults();
    }

    /// <summary>ساخت سند پروژه از روی فرم — با پارسر عددی امن (ارقام فارسی/اعشار فارسی).</summary>
    private ProjectDocument? BuildDocumentFromForm()
    {
        // پس از کنترل null، مقادیر غیرتهی محلی برای سادگی نوع‌ها استفاده می‌شوند.
        var diameterOpt = ParseNumber(DiameterBox.Text, "قطر اسمی");
        var pitchOpt = ParseNumber(PitchBox.Text, "گام");
        var lengthOpt = ParseNumber(LengthBox.Text, "طول");
        if (diameterOpt is null || pitchOpt is null || lengthOpt is null) return null;

        var diameter = diameterOpt.Value;
        var pitch = pitchOpt.Value;
        var length = lengthOpt.Value;

        var handedness = ((ComboBoxItem)HandednessBox.SelectedItem)?.Tag as string == "Left"
            ? ThreadHandedness.Left
            : ThreadHandedness.Right;
        var modeling = ((ComboBoxItem)ModelingModeBox.SelectedItem)?.Tag as string == "Simplified"
            ? ThreadModelingMode.Simplified
            : ThreadModelingMode.RealHelical;

        var acrossFlats = ParseNumber(AcrossFlatsBox.Text, "عرض آچارخور") ?? diameter * 1.55;
        var headHeight = ParseNumber(HeadHeightBox.Text, "ارتفاع سر") ?? diameter * 0.65;
        var headDiameter = ParseNumber(HeadDiameterBox.Text, "قطر سر") ?? diameter * 1.55;
        var slotWidth = ParseNumber(SlotWidthBox.Text, "پهنای شیار") ?? diameter * 0.18;
        var slotDepth = ParseNumber(SlotDepthBox.Text, "عمق شیار") ?? diameter * 0.15;
        var threadRunout = string.IsNullOrWhiteSpace(ThreadRunoutBox.Text)
            ? (double?)null
            : ParseNumber(ThreadRunoutBox.Text, "طول رزوه");
        var starts = (int?)ParseNumber(StartsBox.Text, "تعداد شروع‌ها") ?? 1;

        var catalogEntry = (SizeBox.SelectedItem as ComboBoxItem)?.Tag as CatalogEntry;
        var sourceNote = catalogEntry?.SourceNote;

        var thread = new ThreadSpec
        {
            Family = ThreadFamily.IsoMetric,
            Kind = ThreadKind.External,
            NominalDiameterMm = diameter,
            PitchMm = pitch,
            Starts = Math.Max(1, starts),
            Handedness = handedness,
            ThreadLengthMm = threadRunout ?? length,
            ModelingMode = modeling,
            HalfAngleDegrees = 30,
            StandardId = catalogEntry is null ? null : $"CAT-{catalogEntry.SizeLabel}-{catalogEntry.Series}",
            DataSourceNote = sourceNote ?? "ابعاد ورودی کاربر — استاندارد نیست",
        };

        var kind = SelectedKind;
        var head = ParseEnum<HeadStyle>(HeadBox);
        var drive = ParseEnum<DriveStyle>(DriveBox);

        try
        {
            return kind switch
            {
                "Bolt" => new ProjectDocument
                {
                    ProjectName = $"پیچ {catalogEntry?.SizeLabel ?? diameter.ToString("0.#")}×{length:0.#}",
                    Kind = PartKind.Bolt,
                    Bolt = new BoltSpec
                    {
                        Head = head,
                        Drive = drive,
                        Thread = thread,
                        LengthUnderHeadMm = length,
                        HeadHeightMm = headHeight,
                        HeadAcrossFlatsMm = head == HeadStyle.Hex ? acrossFlats : null,
                        HeadDiameterMm = head is HeadStyle.SocketCap or HeadStyle.Pan or HeadStyle.FlatCountersunk ? headDiameter : null,
                        SlotWidthMm = drive is DriveStyle.Slot or DriveStyle.Phillips ? slotWidth : null,
                        SlotDepthMm = drive is DriveStyle.Slot or DriveStyle.Phillips ? slotDepth : null,
                        HexSocketAcrossFlatsMm = drive == DriveStyle.HexSocket ? diameter * 0.55 : null,
                        HexSocketDepthMm = drive == DriveStyle.HexSocket ? diameter * 0.35 : null,
                        ThreadRunoutFromTipMm = threadRunout,
                        TipChamferMm = pitch * 0.6,
                    },
                },
                "Nut" => new ProjectDocument
                {
                    ProjectName = $"مهره {catalogEntry?.SizeLabel ?? diameter.ToString("0.#")}",
                    Kind = PartKind.Nut,
                    Nut = new NutSpec
                    {
                        Thread = thread with { Kind = ThreadKind.Internal },
                        AcrossFlatsMm = acrossFlats,
                        HeightMm = length,
                        ChamferMm = Math.Min(0.4, diameter * 0.08),
                    },
                },
                "Washer" => new ProjectDocument
                {
                    ProjectName = "واشر تخت",
                    Kind = PartKind.Washer,
                    Washer = new WasherSpec
                    {
                        InnerDiameterMm = diameter * 1.1,
                        OuterDiameterMm = diameter * 2.0,
                        ThicknessMm = length > 5 ? 1.6 : Math.Max(length, 0.3),
                    },
                },
                "ThreadedRod" => new ProjectDocument
                {
                    ProjectName = $"میل رزوه‌دار {catalogEntry?.SizeLabel ?? diameter.ToString("0.#")}",
                    Kind = PartKind.ThreadedRod,
                    ThreadedRod = new ThreadedRodSpec
                    {
                        Thread = thread,
                        TotalLengthMm = length,
                        EndChamferMm = pitch * 0.5,
                    },
                },
                "Assembly" => new ProjectDocument
                {
                    ProjectName = $"مجموعه {catalogEntry?.SizeLabel ?? diameter.ToString("0.#")}",
                    Kind = PartKind.Assembly,
                    Assembly = new AssemblySpec
                    {
                        Bolt = new BoltSpec
                        {
                            Head = head,
                            Drive = drive,
                            Thread = thread,
                            LengthUnderHeadMm = length,
                            HeadHeightMm = headHeight,
                            HeadAcrossFlatsMm = head == HeadStyle.Hex ? acrossFlats : null,
                            HeadDiameterMm = head is HeadStyle.SocketCap or HeadStyle.Pan or HeadStyle.FlatCountersunk ? headDiameter : null,
                            TipChamferMm = pitch * 0.6,
                        },
                        Nut = new NutSpec
                        {
                            Thread = thread with { Kind = ThreadKind.Internal },
                            AcrossFlatsMm = acrossFlats,
                            HeightMm = diameter * 0.8,
                            ChamferMm = Math.Min(0.4, diameter * 0.08),
                        },
                        Washer = new WasherSpec
                        {
                            InnerDiameterMm = diameter * 1.1,
                            OuterDiameterMm = diameter * 2.0,
                            ThicknessMm = 1.0,
                        },
                        DisplayGapMm = 1.0,
                    },
                },
                _ => null,
            };
        }
        catch (Exception ex)
        {
            ValidationMessages.Add($"خطای ساخت سند: {ex.Message}");
            return null;
        }
    }

    private static ValidationResult ValidateDocument(ProjectDocument document) => document.Kind switch
    {
        PartKind.Bolt => PartValidator.Validate(document.Bolt!),
        PartKind.Nut => PartValidator.Validate(document.Nut!),
        PartKind.Washer => PartValidator.Validate(document.Washer!),
        PartKind.ThreadedRod => PartValidator.Validate(document.ThreadedRod!),
        PartKind.Assembly => PartValidator.Validate(document.Assembly!),
        _ => new ValidationResult(),
    };

    /// <summary>تجزیهٔ عدد با پذیرش ارقام فارسی و ممیز فارسی (GEO-08).</summary>
    private double? ParseNumber(string text, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = PersianDigits.NormalizeDigits(text.Trim()).Replace('٫', '.');
        if (double.TryParse(normalized, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        ValidationMessages.Add($"مقدار «{text}» برای {fieldName} عدد معتبر نیست.");
        return null;
    }

    private static T ParseEnum<T>(ComboBox box)
        where T : struct, Enum
    {
        var tag = (box.SelectedItem as ComboBoxItem)?.Tag as string;
        return Enum.TryParse<T>(tag, ignoreCase: true, out var value) ? value : default;
    }
}

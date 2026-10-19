using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Sahand.App.Services;
using Sahand.App.Views;
using Sahand.Core.Printing;
using Sahand.Core.Project;
using Sahand.Geometry.Meshing;
using Sahand.Reporting;

namespace Sahand.App;

/// <summary>
/// وضعیت مشترک جلسهٔ کاری میان نماها — سند پروژه، مش جاری و نسخهٔ چاپی.
/// </summary>
/// <remarks>
/// مدل اسمی هرگز بی‌صدا تغییر نمی‌کند: نسخهٔ چاپی در مشتق جدا نگه داشته می‌شود (سند ۴-۱).
/// </remarks>
public sealed class WorkSession
{
    /// <summary>سند پروژهٔ جاری (اگر پروژه‌ای باز باشد).</summary>
    public ProjectDocument? Document { get; set; }

    /// <summary>مش نسخهٔ اسمی (آخرین ساخت موفق).</summary>
    public Mesh? NominalMesh { get; set; }

    /// <summary>مش نسخهٔ چاپی مشتق‌شده (اگر ساخته باشد).</summary>
    public Mesh? PrintMesh { get; set; }

    /// <summary>جدول تفاوت آخرین نسخهٔ چاپی.</summary>
    public IReadOnlyList<PrintDiffRow>? LastPrintDiff { get; set; }

    /// <summary>مسیر فایل پروژهٔ باز (برای ذخیرهٔ سریع).</summary>
    public string? OpenFilePath { get; set; }
}

/// <summary>
/// پنجرهٔ اصلی — ناوبری راست‌چین و میزبانی نماها با وضعیت جلسهٔ مشترک.
/// </summary>
public partial class MainWindow : Window
{
    private readonly Dictionary<int, UserControl> _viewCache = [];

    /// <summary>سرویس‌های برنامه.</summary>
    public AppServices Services { get; }

    /// <summary>وضعیت جلسهٔ کاری مشترک نماها.</summary>
    public WorkSession Session { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        Services = ((App)Application.Current).Services;

        // نمایش نسخهٔ در حال اجرا در عنوان پنجره (بازبینی ۰٫۲٫۰، D8) — از نسخهٔ اسمبلی خوانده می‌شود.
        var version = GetType().Assembly.GetName().Version;
        if (version is not null)
            Title = $"{Title} — نسخهٔ {version.ToString(3)}";

        // شروع در «خانه».
        NavList.SelectedIndex = 0;

        // پیشنهاد بازیابی پس از بسته‌شدن غیرمنتظره (Autosafe) — با رضایت کاربر.
        if (Services.Autosave.HasRecovery && Services.Autosave.TryReadRecovery() is { } recovered)
        {
            var messageBoxResult = MessageBox.Show(
                this,
                $"نسخهٔ بازیابی از پروژهٔ «{recovered.ProjectName}» پیدا شد. بازیابی شود؟",
                "بازیابی پروژهٔ ذخیره‌نشده",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.Yes);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                Session.Document = recovered;
                NavList.SelectedIndex = 1; // رفتن به طراحی برای ادامه.
            }
            else
            {
                Services.Autosave.ClearRecovery();
            }
        }
    }

    /// <summary>نام بخش جاری برای عنوان نماها.</summary>
    public string CurrentSection => NavList.SelectedIndex switch
    {
        0 => "خانه",
        1 => "طراحی قطعه",
        2 => "پیش‌نمایش سه‌بعدی",
        3 => "آماده‌سازی چاپ",
        4 => "خروجی",
        5 => "تنظیمات و گزارش",
        _ => "درباره و حمایت",
    };

    private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ContentHost is null) return;

        var index = NavList.SelectedIndex;
        if (index < 0) return;

        if (!_viewCache.TryGetValue(index, out var view))
        {
            view = CreateView(index);
            _viewCache[index] = view;
        }

        ContentHost.Content = view;

        // به‌روزرسانی وضعیت صف گزارش در پایین نوار کناری (شفافیت MAIL).
        var pending = Services.ReportQueue.Reports.Count(r => r.Status is ReportStatus.Queued or ReportStatus.Failed);
        QueueStatusText.Text = pending > 0
            ? $"گزارش‌ها: {pending} مورد در صف محلی"
            : "گزارش‌ها: صف محلی";
    }

    private UserControl CreateView(int index) => index switch
    {
        0 => new HomeView(this),
        1 => new DesignView(this),
        2 => new PreviewView(this),
        3 => new PrintView(this),
        4 => new ExportView(this),
        5 => new SettingsView(this),
        _ => new AboutView(),
    };
}

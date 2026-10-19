using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Sahand.App.Services;
using Sahand.Core.Units;
using Sahand.Reporting;
using Sahand.Reporting.Smtp;

namespace Sahand.App.Views;

/// <summary>
/// نمای تنظیمات و گزارش — ذخیرهٔ تنظیمات، مدیریت صف، پیکربندی/آزمون SMTP.
/// </summary>
/// <remarks>
/// پوشش MAIL-02: بدون پیکربندی، دکمه ارسال وضعیت «ارسال نشده — پیکربندی لازم است»
/// می‌دهد و هیچ موفقیت کاذبی نمایش داده نمی‌شود. آزمون SMTP واقعاً ایمیل آزمون به
/// نشانی خود فرستنده می‌فرستد و نتیجهٔ واقعی را گزارش می‌کند.
/// </remarks>
public partial class SettingsView : UserControl
{
    private readonly MainWindow _owner;

    /// <summary>ردیف‌های نمایش صف گزارش.</summary>
    public ObservableCollection<QueuedReportRow> QueueRows { get; } = [];

    public SettingsView(MainWindow owner)
    {
        InitializeComponent();
        _owner = owner;
        QueueGrid.ItemsSource = QueueRows;

        LoadSettings();
        LoadSmtp();
        RefreshQueue();
    }

    private void LoadSettings()
    {
        var s = _owner.Services.Settings.Current;
        UnitBox.SelectedIndex = s.Unit == LengthUnit.Inch ? 1 : 0;
        PersianDigitsCheck.IsChecked = s.PersianDigits;
        DecimalBox.SelectedIndex = Math.Clamp(s.DecimalPlaces - 2, 0, 2);
        AutosaveBox.Text = s.AutosaveMinutes.ToString();
        AutoReportCheck.IsChecked = s.AutoReportEnabled;
    }

    private void LoadSmtp()
    {
        var smtp = _owner.Services.SmtpSettings.Current;
        if (smtp is null) return;
        SmtpHostBox.Text = smtp.Host;
        SmtpPortBox.Text = smtp.Port.ToString();
        SmtpNameBox.Text = smtp.FromDisplayName;
        SmtpFromBox.Text = smtp.FromAddress;
        SmtpUserBox.Text = smtp.Username;
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (QueueGrid is null) return; // در حین InitializeComponent رویدادهای زودهنگام.

        var autosave = int.TryParse(AutosaveBox.Text.Trim(), out var minutes) ? Math.Clamp(minutes, 0, 60) : 3;
        var decimalPlaces = int.TryParse(((ComboBoxItem)DecimalBox.SelectedItem)?.Tag as string ?? "2",
            out var dp) ? Math.Clamp(dp, 0, 6) : 2;

        _owner.Services.Settings.Save(new AppSettings
        {
            Unit = ((ComboBoxItem)UnitBox.SelectedItem)?.Tag as string == "Inch" ? LengthUnit.Inch : LengthUnit.Millimeter,
            PersianDigits = PersianDigitsCheck.IsChecked == true,
            DecimalPlaces = decimalPlaces,
            AutosaveMinutes = autosave,
            AutoReportEnabled = AutoReportCheck.IsChecked == true,
            IncludeDiagnosticData = _owner.Services.Settings.Current.IncludeDiagnosticData,
            LastExportFolder = _owner.Services.Settings.Current.LastExportFolder,
            MaterialName = _owner.Services.Settings.Current.MaterialName,
        });
    }

    private void RefreshQueue()
    {
        QueueRows.Clear();
        foreach (var report in _owner.Services.ReportQueue.Reports)
        {
            QueueRows.Add(new QueuedReportRow(report));
        }

        var configured = _owner.Services.Reports.IsTransportConfigured();
        QueueStatusText.Text = QueueRows.Count == 0
            ? "صف خالی است."
            : configured
                ? "تنظیمات ارسال کامل است؛ دکمهٔ «ارسال صف» هنگام اتصال شبکه کار می‌کند."
                : "ارسال نشده — پیکربندی لازم است: SMTP/رمز را کامل کنید.";
    }

    private async void OnSendQueueClick(object sender, RoutedEventArgs e)
    {
        if (!_owner.Services.Reports.IsTransportConfigured())
        {
            QueueStatusText.Text = "ارسال نشده — پیکربندی لازم است: تنظیمات SMTP و رمز را کامل کنید.";
            MessageBox.Show("پیکربندی ارسال کامل نیست (سرور/فرستنده/رمز). بخش SMTP را کامل کنید.",
                "پیکربندی لازم است", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        QueueStatusText.Text = "در حال ارسال…";
        try
        {
            var (sent, errors) = await _owner.Services.Reports.SendQueuedAsync();
            RefreshQueue();
            QueueStatusText.Text = sent > 0
                ? $"{sent} گزارش با موفقیت ارسال شد."
                : "هیچ گزارشی ارسال نشد؛ جزئیات خطا در صف.";
            if (errors.Count > 0)
            {
                QueueStatusText.Text += "\n" + string.Join("\n", errors);
            }
        }
        catch (Exception ex)
        {
            QueueStatusText.Text = $"خطای ارسال: {ex.Message}";
        }
    }

    private void OnDeleteReportClick(object sender, RoutedEventArgs e)
    {
        if (QueueGrid.SelectedItem is QueuedReportRow row)
        {
            _owner.Services.ReportQueue.Remove(row.Id);
            RefreshQueue();
        }
    }

    private void OnClearQueueClick(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("کل صف گزارش‌ها پاک شود؟ (قابل بازگشت نیست)",
            "پاک‌کردن صف", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;

        foreach (var row in QueueRows.ToList())
            _owner.Services.ReportQueue.Remove(row.Id);
        RefreshQueue();
    }

    private void OnSaveSmtpClick(object sender, RoutedEventArgs e)
    {
        var settings = BuildSmtpSettingsFromForm();
        if (settings is null)
        {
            SmtpStatusText.Text = "ذخیره نشد — فیلدهای SMTP کامل نیستند (سرور/پورت/فرستنده/نام کاربری).";
            return;
        }

        _owner.Services.SmtpSettings.Save(settings);

        // ذخیرهٔ رمز فقط اگر کاربر مقدار تازه وارد کرده باشد (خالی = دست‌نخورده).
        if (!string.IsNullOrEmpty(SmtpPasswordBox.Password))
        {
            _owner.Services.Secrets.StoreSmtpPassword(SmtpPasswordBox.Password);
            SmtpPasswordBox.Clear();
        }

        SmtpStatusText.Text = "تنظیمات SMTP ذخیره شد (رمز با DPAPI محافظت می‌شود).";
        RefreshQueue();
    }

    private async void OnTestSmtpClick(object sender, RoutedEventArgs e)
    {
        var settings = BuildSmtpSettingsFromForm();
        var password = SmtpPasswordBox.Password;
        if (settings is null || string.IsNullOrEmpty(password))
        {
            SmtpStatusText.Text = "آزمون انجام نشد — تنظیمات و رمز را کامل کنید.";
            return;
        }

        SmtpStatusText.Text = "در حال آزمون اتصال واقعی…";
        try
        {
            // آزمون واقعی: پیام آزمون به نشانی خود فرستنده (بدون موفقیت کاذب — سند ۲-۷).
            var client = new RawSmtpClient();
            var result = await client.SendAsync(
                settings, password, settings.FromAddress,
                "آزمون تنظیمات — طراح پیچ و مهره سهند",
                "این پیام آزمون تنظیمات SMTP از داخل برنامه است؛ نیازی به پاسخ نیست.");

            SmtpStatusText.Text = result.Success
                ? "آزمون موفق ✓ — پیام آزمون به فرستنده ارسال شد."
                : $"آزمون ناموفق ✗ — {result.PersianError}";
        }
        catch (Exception ex)
        {
            SmtpStatusText.Text = $"آزمون ناموفق ✗ — {ex.Message}";
        }
    }

    private SmtpSettings? BuildSmtpSettingsFromForm()
    {
        var host = SmtpHostBox.Text.Trim();
        var from = SmtpFromBox.Text.Trim();
        var user = SmtpUserBox.Text.Trim();
        var port = int.TryParse(SmtpPortBox.Text.Trim(), out var p) ? p : 587;

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(from) || string.IsNullOrEmpty(user))
            return null;

        return new SmtpSettings
        {
            Host = host,
            Port = port,
            FromDisplayName = string.IsNullOrWhiteSpace(SmtpNameBox.Text) ? "کاربر طراح سهند" : SmtpNameBox.Text.Trim(),
            FromAddress = from,
            Username = user,
            UseStartTls = port != 465,
        };
    }
}

/// <summary>ردیف نمایشی صف گزارش برای DataGrid.</summary>
public sealed class QueuedReportRow
{
    private readonly QueuedReport _report;

    public QueuedReportRow(QueuedReport report) => _report = report;

    public Guid Id => _report.Id;

    public string Kind => _report.Kind == ReportKind.Error ? "خطا" : "بازخورد";

    public string Status => _report.Status switch
    {
        ReportStatus.Queued => "در صف",
        ReportStatus.Sending => "در حال ارسال",
        ReportStatus.Sent => "ارسال شد",
        ReportStatus.Failed => "ناموفق",
        ReportStatus.Cancelled => "لغوشده",
        _ => "—",
    };

    public string SubjectOrModule => _report.Subject ?? _report.Module ?? "—";

    public int AttemptCount => _report.AttemptCount;
}

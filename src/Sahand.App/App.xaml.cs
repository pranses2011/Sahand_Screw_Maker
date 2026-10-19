using System.Windows;
using System.Windows.Threading;
using Sahand.App.Services;

namespace Sahand.App;

/// <summary>
/// کلاس برنامه — راه‌اندازی سرویس‌های محلی، رصد خطای سراسری و پنجرهٔ اصلی.
/// </summary>
/// <remarks>
/// آفلاین‌بودن: هیچ دانلود/بررسی به‌روزرسانی/تله‌متری در راه‌اندازی وجود ندارد (سند ۵-۲).
/// بازیابی خودکار (Autosafe) در صورت وجود فایل بازیابی، پیش از نمایش پنجره پیشنهاد می‌شود.
/// رصد خطا (بازبینی ۰٫۲٫۰، D3): هر سه نقطهٔ استاندارد فعال است — خطای رشتهٔ UI
/// مهار می‌شود و برنامه باز می‌ماند؛ همهٔ خطاها در CrashLog فایل می‌شوند.
/// </remarks>
public partial class App : Application
{
    /// <summary>سرویس‌های مشترک برنامه (تنظیمات، گزارش، autosafe).</summary>
    public AppServices Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // مسیر دادهٔ محلی کاربر (بدون هیچ وابستگی شبکه).
        Services = new AppServices();
        CrashLog.Initialize(Services.DataFolder);

        // رصد خطاهای غیرمهارشده — پیش از ساخت هر پنجره فعال می‌شود.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        System.AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // ذخیرهٔ تنظیمات در خروج تمیز؛ خطای ذخیره برنامه را خراب نمی‌کند.
        Services?.Shutdown();
        base.OnExit(e);
    }

    /// <summary>خطای رشتهٔ UI: مهار + لاگ + پیام فارسی؛ برنامه باز می‌ماند.</summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        CrashLog.Write("رشتهٔ رابط کاربری", e.Exception);
        ShowFriendlyError(e.Exception);
        e.Handled = true;
    }

    /// <summary>خطای خارج از رشتهٔ UI (غیرقابل مهار): فقط لاگ می‌شود تا اثر بماند.</summary>
    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        CrashLog.Write("خارج از رشتهٔ UI", e.ExceptionObject as Exception);
    }

    /// <summary>خطای Task رهاشده: ثبت و «مشاهده‌شده» — برنامه بسته نمی‌شود.</summary>
    private void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        CrashLog.Write("پس‌زمینه (Task)", e.Exception);
        e.SetObserved();
    }

    /// <summary>پیام خطای فارسی دوستانه با مسیر لاگ — بدون جزئیات فنی خام.</summary>
    private static void ShowFriendlyError(Exception exception)
    {
        const string title = "خطای غیرمنتظره — طراح پیچ و مهره سهند";
        var message =
            "یک خطای غیرمنتظره رخ داد و مهار شد؛ برنامه تا جای ممکن به کار ادامه می‌دهد.\n" +
            "لطفاً عملیات را تکرار کنید و اگر تکرار شد، فایل لاگ را برای پشتیبانی بفرستید.\n\n" +
            $"شرح کوتاه: {exception.Message}\n\n" +
            $"جزئیات در فایل لاگ ذخیره شد:\n{CrashLog.LogPath}";
        try
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch
        {
            // اگر خودِ پیام‌رسان خطا بخورد، لاگ قبلاً نوشته شده است.
        }
    }
}

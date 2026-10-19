using System.Windows;
using Sahand.App.Services;

namespace Sahand.App;

/// <summary>
/// کلاس برنامه — راه‌اندازی سرویس‌های محلی و پنجرهٔ اصلی.
/// </summary>
/// <remarks>
/// آفلاین‌بودن: هیچ دانلود/بررسی به‌روزرسانی/تله‌متری در راه‌اندازی وجود ندارد (سند ۵-۲).
/// بازیابی خودکار (Autosafe) در صورت وجود فایل بازیابی، پیش از نمایش پنجره پیشنهاد می‌شود.
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
}

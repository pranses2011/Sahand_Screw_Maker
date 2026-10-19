using System.IO;
using System.Text;

namespace Sahand.App.Services;

/// <summary>
/// لاگ فایل خطا — اثری ماندگار برای عیب‌یابی (بازبینی ۰٫۲٫۰، D6).
/// </summary>
/// <remarks>
/// فقط متن خطا/زمان/منبع ثبت می‌شود؛ هیچ داده‌ای ارسال نمی‌شود (آفلاین) و
/// شمارهٔ کارت/محتوای پروژهٔ کاربر در آن نمی‌نشیند (الزام حریم خصوصی سند ۵).
/// نوشتن اتمیک و fail-safe است — خطای خودِ لاگ هرگز برنامه را نمی‌بندد.
/// </remarks>
public static class CrashLog
{
    private static readonly object Gate = new();

    /// <summary>مسیر لاگ (logs/crash-log.txt در پوشهٔ دادهٔ برنامه).</summary>
    public static string LogPath { get; private set; } = "crash-log.txt";

    /// <summary>راه‌اندازی با پوشهٔ دادهٔ برنامه — فراخوانی یک‌بار در راه‌اندازی.</summary>
    public static void Initialize(string dataFolder)
    {
        try
        {
            var logs = Path.Combine(dataFolder, "logs");
            Directory.CreateDirectory(logs);
            LogPath = Path.Combine(logs, "crash-log.txt");
        }
        catch
        {
            // اگر پوشه ساخته نشد، مسیر نسبی می‌ماند — برنامه باز می‌شود.
        }
    }

    /// <summary>ثبت یک خطا با منبع فارسی، پیام و ردّ خلاصه (حداکثر ۶ فریم).</summary>
    public static void Write(string source, Exception? exception)
    {
        if (exception is null) return;
        try
        {
            lock (Gate)
            {
                var sb = new StringBuilder();
                sb.Append('[')
                  .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                  .Append("] ")
                  .Append(source)
                  .AppendLine();
                sb.Append("  پیام: ").AppendLine(exception.Message);

                var inner = exception.InnerException;
                var depth = 0;
                while (inner is not null && depth < 3)
                {
                    sb.Append("  خطای درونی: ").AppendLine(inner.Message);
                    inner = inner.InnerException;
                    depth++;
                }

                if (!string.IsNullOrEmpty(exception.StackTrace))
                {
                    var frames = exception.StackTrace.Split('\n');
                    sb.AppendLine("  ردّ:");
                    var count = Math.Min(6, frames.Length);
                    for (var i = 0; i < count; i++)
                        sb.Append("    ").AppendLine(frames[i].Trim());
                }

                sb.AppendLine();
                File.AppendAllText(LogPath, sb.ToString(), new UTF8Encoding(true));
            }
        }
        catch
        {
            // لاگ نباید منشأ خطای دوم شود؛ بی‌صدا رها می‌شود.
        }
    }
}

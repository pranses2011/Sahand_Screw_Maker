using Sahand.Core.Parts;
using Sahand.Core.Threads;

namespace Sahand.Core.Printing;

/// <summary>
/// یک ردیف جدول مقایسهٔ «اسمی در برابر چاپی» (الزام سند ۴-۲: پارامتر/اسمی/اصلاح/نهایی/علت).
/// </summary>
/// <param name="ParameterLabel">نام فارسی پارامتر برای نمایش.</param>
/// <param name="NominalValue">مقدار اسمی.</param>
/// <param name="PrintValue">مقدار نهایی نسخهٔ چاپی.</param>
/// <param name="Reason">علت تغییر به فارسی؛ خالی یعنی بدون تغییر.</param>
public sealed record PrintDiffRow(string ParameterLabel, double NominalValue, double PrintValue, string Reason);

/// <summary>
/// نتیجهٔ ساخت نسخهٔ چاپی — پارامترهای مشتق‌شده و جدول تفاوت.
/// </summary>
/// <param name="Bolt">پیچ نسخهٔ چاپی (مشتق‌شده؛ مدل اسمی دست‌نخورده می‌ماند).</param>
/// <param name="Nut">مهرهٔ نسخهٔ چاپی اگر ورودی مهره داشته باشیم.</param>
/// <param name="Diff">جدول تفاوت‌ها برای نمایش پیش از تأیید کاربر.</param>
public sealed record PrintAdjustedPart(BoltSpec Bolt, NutSpec? Nut, IReadOnlyList<PrintDiffRow> Diff);

/// <summary>
/// سازندهٔ نسخهٔ «بهینه‌شده برای چاپ» — همیشه نسخهٔ جدید می‌سازد و هرگز ورودی را تغییر نمی‌دهد.
/// </summary>
/// <remarks>
/// مدل اسمی بی‌صدا تغییر نمی‌کند (اصل غیرقابل‌مذاکرهٔ سند ۱-۲). جبران‌ها با علامت قراردادی
/// اعمال می‌شوند: برای رزوهٔ خارجی، مثبت قطر را کم می‌کند؛ برای رزوهٔ داخلی، مثبت حفره را زیاد می‌کند.
/// جبران انقباض به‌صورت مقیاس کلی قطر اعمال و در جدول تفاوت با علت جداگانه ثبت می‌شود.
/// </remarks>
public static class PrintAdjuster
{
    /// <summary>
    /// نسخهٔ چاپی پیچ (و مهرهٔ اختیاری) را از روی پارامترهای اسمی و پروفایل چاپ می‌سازد.
    /// </summary>
    /// <param name="nominalBolt">پیچ اسمی — تغییر نمی‌کند.</param>
    /// <param name="nominalNut">مهرهٔ اسمی اختیاری — تغییر نمی‌کند.</param>
    /// <param name="profile">پروفایل چاپ با جبران‌ها.</param>
    /// <returns>نسخهٔ مشتق‌شده به‌همراه جدول تفاوت.</returns>
    public static PrintAdjustedPart Adjust(BoltSpec nominalBolt, NutSpec? nominalNut, PrintProfile profile)
    {
        ArgumentNullException.ThrowIfNull(nominalBolt);
        ArgumentNullException.ThrowIfNull(profile);

        var diff = new List<PrintDiffRow>();
        var clearance = profile.EffectiveClearanceMm;

        // لقی هدف، نصف آن روی رزوهٔ خارجی (کاهش قطر) و نصف روی داخلی (افزایش قطر) اثر می‌گذارد؛
        // علامت قرارداد: ExternalThreadRadialCompensation مثبت = ریزتر، Internal مثبت = درشت‌تر.
        var externalDeltaMm = profile.ExternalThreadRadialCompensationMm + clearance / 2.0;
        var internalDeltaMm = profile.InternalThreadRadialCompensationMm + clearance / 2.0;

        // جبران انقباض ماده به‌صورت ضریب مقیاس قطرها (ضرب، نه جمع) اعمال می‌شود تا شکل حفظ شود.
        var shrinkScale = profile.ApplyShrinkage
            ? 1.0 + profile.ShrinkagePercent / 100.0
            : 1.0;

        var nominalD = nominalBolt.Thread.NominalDiameterMm;
        var printD = (nominalD - externalDeltaMm) * shrinkScale;

        var printBolt = nominalBolt with
        {
            Name = nominalBolt.Name + " — بهینه‌شده برای چاپ",
            Thread = nominalBolt.Thread with
            {
                NominalDiameterMm = printD,
                // طول مؤثر رزوه با قطر جدید باید متناسب بماند؛ گام تغییر نمی‌کند مگر کاربر بخواهد.
                DataSourceNote = MergeNotes(nominalBolt.Thread.DataSourceNote,
                    $"نسخهٔ چاپی: جبران خارجی {externalDeltaMm:0.###}mm، لقی {clearance:0.###}mm، انقباض ×{shrinkScale:0.###}"),
            },
            // پخ نوک برای ورود بهتر رزوه در چاپ؛ فقط اگر کاربر صریحاً فعال کرده باشد.
            TipChamferMm = profile.ThreadLeadInChamferMm > 0
                ? Math.Max(nominalBolt.TipChamferMm, profile.ThreadLeadInChamferMm)
                : nominalBolt.TipChamferMm,
        };

        diff.Add(new PrintDiffRow("قطر اسمی رزوهٔ پیچ", nominalD, printD,
            $"جبران شعاعی خارجی {-externalDeltaMm:0.###}mm و لقی {-clearance / 2.0:0.###}mm" +
            (profile.ApplyShrinkage ? $" و انقباض ×{shrinkScale:0.###}" : string.Empty)));

        if (nominalBolt.ThreadRunoutFromTipMm is { } runout)
        {
            diff.Add(new PrintDiffRow("طول رزوهٔ پیچ", runout, runout, string.Empty));
        }

        NutSpec? printNut = null;
        if (nominalNut is not null)
        {
            var nutNominalD = nominalNut.Thread.NominalDiameterMm;
            // برای مهره، جبران مثبت «درشت‌تر» است: قطر مرجع اسمی همان می‌ماند (مرجع جفت)،
            // اما عمق درگیری مؤثر با افزایش قطر حفرهٔ داخلی بیشتر می‌شود؛ در این مدل ساده
            // قطر اسمی رزوهٔ داخلی به‌عنوان قطر دیوارهٔ حفره در نظر گرفته می‌شود و جبران روی آن می‌نشیند.
            var nutPrintD = (nutNominalD + internalDeltaMm) * shrinkScale;

            printNut = nominalNut with
            {
                Name = nominalNut.Name + " — بهینه‌شده برای چاپ",
                Thread = nominalNut.Thread with
                {
                    NominalDiameterMm = nutPrintD,
                    DataSourceNote = MergeNotes(nominalNut.Thread.DataSourceNote,
                        $"نسخهٔ چاپی: جبران داخلی {internalDeltaMm:0.###}mm، لقی {clearance / 2.0:0.###}mm، انقباض ×{shrinkScale:0.###}"),
                },
            };

            diff.Add(new PrintDiffRow("قطر حفرهٔ رزوهٔ مهره", nutNominalD, nutPrintD,
                $"جبران شعاعی داخلی {internalDeltaMm:0.###}mm و لقی {clearance / 2.0:0.###}mm" +
                (profile.ApplyShrinkage ? $" و انقباض ×{shrinkScale:0.###}" : string.Empty)));

            // هشدار هندسی: اگر جبران‌ها آن‌قدر بزرگ باشند که عمق مؤثر درگیری از عمق پروفیل بیشتر شود،
            // رزوهٔ چاپی عملاً بی‌دندانه می‌شود — باید در جدول تفاوت دیده شود (EXP-07).
            var threadDepth = 0.625 * 0.86602540378 * nominalNut.Thread.PitchMm;
            if (internalDeltaMm + clearance / 2.0 >= threadDepth)
            {
                diff.Add(new PrintDiffRow("عمق درگیری رزوهٔ مهره", threadDepth,
                    Math.Max(0, threadDepth - internalDeltaMm - clearance / 2.0),
                    "هشدار: جبران/لقی از عمق پروفیل بیشتر است؛ رزوهٔ چاپی دندانهٔ مؤثر نخواهد داشت."));
            }
        }

        if (profile.ThreadLeadInChamferMm > 0)
        {
            diff.Add(new PrintDiffRow("پخ شروع رزوه", nominalBolt.TipChamferMm,
                Math.Max(nominalBolt.TipChamferMm, profile.ThreadLeadInChamferMm),
                "پخ ورود برای آغاز درگیرشدن آسان‌تر در چاپ"));
        }

        return new PrintAdjustedPart(printBolt, printNut, diff);
    }

    /// <summary>یادداشت منبع اسمی و یادداشت جبران چاپ را بدون از دست دادن اطلاع ترکیب می‌کند.</summary>
    private static string MergeNotes(string? original, string addition) =>
        string.IsNullOrWhiteSpace(original) ? addition : $"{original} | {addition}";
}

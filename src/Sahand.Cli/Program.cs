namespace Sahand.Cli;

/// <summary>
/// نقطهٔ ورود ssnd-cli — تجزیهٔ آرگومان دستی (بدون وابستگی) و راهنمای فارسی.
/// </summary>
/// <remarks>
/// خروجی متن فارسی است. کد بازگشت: ۰ موفق، ۱ خطای کاربر/اعتبارسنجی، ۲ خطای غیرمنتظره.
/// </remarks>
public static class Program
{
    /// <summary>اجرای CLI.</summary>
    public static int Main(string[] arguments)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (arguments.Length == 0 || arguments[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
            return 0;
        }

        var command = arguments[0].ToLowerInvariant();
        var options = ParseOptions(arguments.Skip(1));

        try
        {
            return command switch
            {
                "generate" => Commands.Generate(options),
                "export" => Commands.Export(options),
                "validate" => Commands.ValidateProject(options),
                "catalog" => Commands.CatalogList(options),
                "coupon" => Commands.Coupon(options),
                "--version" or "version" => PrintVersion(),
                _ => UnknownCommand(command),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"خطای غیرمنتظره: {ex.Message}");
            Console.Error.WriteLine("برای جزئیات بیشتر، گزارش خطا را از برنامهٔ روان ثبت کنید.");
            return 2;
        }
    }

    private static int PrintVersion()
    {
        Console.WriteLine("Sahand Screw & Nut Designer — ssnd-cli نسخهٔ 0.3.0");
        Console.WriteLine("موتور هندسه: مش پارامتریک داخلی (بدون هستهٔ B-Rep — خروجی STEP در برنامهٔ توسعه)");
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"دستور ناشناخته: «{command}»");
        PrintHelp();
        return 1;
    }

    /// <summary>تجزیهٔ «--کلید مقدار» و پرچم‌های بدون مقدار.</summary>
    private static Dictionary<string, string> ParseOptions(IEnumerable<string> args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            if (!list[i].StartsWith("--")) continue;

            var key = list[i];
            if (i + 1 < list.Count && !list[i + 1].StartsWith("--"))
            {
                result[key] = list[i + 1];
                i++;
            }
            else
            {
                result[key] = "true"; // پرچم بدون مقدار.
            }
        }
        return result;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            ssnd-cli — ابزار خط فرمان «طراح پیچ و مهره سهند»

            دستورها:
              generate  ساخت قطعه و ذخیرهٔ پروژهٔ ssnd (+ خروجی اختیاری مش)
                        --part bolt|nut|washer|rod|assembly   (پیش‌فرض bolt)
                        --size M6|1/4-20|…                    (از کاتالوگ)
                        --length mm --diameter mm --pitch mm
                        --head Hex|SocketCap|FlatCountersunk|Pan
                        --drive None|HexSocket|Slot|Phillips
                        --across-flats mm --head-height mm --height mm
                        --thread mm (طول رزوه) --left (چپ‌گرد) --simplified (بدون رزوهٔ واقعی)
                        --quality draft|balanced|fine|veryfine
                        --export STL|OBJ|PLY|3MF|GLB   --ascii
                        --out مسیر.ssnd   --name نام
              export    خروجی مش از پروژهٔ ssnd
                        --in مسیر.ssnd --format STL|OBJ|PLY|3MF|GLB --out مسیر
                        --quality …   --ascii
              validate  اعتبارسنجی فایل پروژه:  --in مسیر.ssnd
              catalog   فهرست کاتالوگ رزوه:      --family metric|unc
              coupon    نمونهٔ آزمون رزوهٔ کوتاه: --size M6 --export STL --out مسیر
              version   نسخه و وضعیت موتور

            خروجی: ۰ موفق، ۱ خطای کاربر/اعتبارسنجی، ۲ خطای غیرمنتظره.
            راهنمای کامل: docs/USER-GUIDE-FA.md
            """);
    }
}

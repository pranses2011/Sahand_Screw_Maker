# 🔧 راهنمای ساخت و آزمون (Build & Test)

## نیازمندی‌ها

| ابزار | نسخهٔ آزموده‌شده | توضیح |
|---|---|---|
| .NET SDK | ۸٫۰٫۴۲۶ (LTS) | برای build و تست همهٔ پروژه‌ها |
| ویندوز | ۱۰/۱۱ x64 | فقط برای **اجرای** برنامهٔ WPF (ساخت روی Linux هم ممکن است) |

## ساخت

```bash
# ساخت کل راه‌حل (۱۱ پروژه):
dotnet build SahandScrewMaker.sln

# ساخت فقط CLI:
dotnet build src/Sahand.Cli/Sahand.Cli.csproj

# ساخت برنامهٔ روان (روی Linux با EnableWindowsTargeting می‌سازد؛ اجرا فقط روی Windows):
dotnet build src/Sahand.App/Sahand.App.csproj

# انتشار بستهٔ ویندوزی خودکفا (روی ویندوز):
dotnet publish src/Sahand.App/Sahand.App.csproj -c Release -r win-x64 --self-contained true
```

## آزمون

```bash
# اجرای همهٔ ۱۱۳ آزمون:
dotnet test SahandScrewMaker.sln
```

| پروژهٔ آزمون | شمار آزمون | محتوای پوشش |
|---|---|---|
| Sahand.Core.Tests | ۴۳ | واحد/کسر اینچ/پروفیل رزوه/اعتبارسنجی/سازگاری جفت/کاتالوگ/فایل ssnd/نسخهٔ چاپی |
| Sahand.Geometry.Tests | ۲۵ | همهٔ قطعات watertight، چندشروع/چپ‌گرد، کیفیت‌ها، زیرساخت مش |
| Sahand.Exporters.Tests | ۱۶ | round-trip مستقل STL/3MF، صداقت فرمت‌ها، جلوگیری از overwrite |
| Sahand.Reporting.Tests | ۱۹ | پاک‌سازی، صف، SMTP با سرور شبیه‌سازی‌شدهٔ درون‌پردازشی |
| Sahand.IntegrationTests | ۱۰ | اجرای واقعی CLI از ابتدا تا فایل خروجی |

## ساختار پروژه

```text
src/
  Sahand.Core/        دامنهٔ پارامتریک (بدون وابستگی UI/OS)
  Sahand.Geometry/    موتور مش و رزوهٔ مارپیچ واقعی
  Sahand.Exporters/   STL/OBJ/PLY/3MF/GLB/DXF + اعتبارسنجی
  Sahand.Reporting/   صف گزارش و SMTP امن
  Sahand.Cli/         ابزار خط فرمان ssnd-cli
  Sahand.App/         برنامهٔ WPF فارسی/راست‌چین با وزیرمتن
tests/                ۵ پروژهٔ xUnit
samples/              پروژه‌های نمونهٔ ssnd
docs/                 مستندات فارسی
```

## وابستگی‌ها و مجوزها

- **زمان اجرا: هیچ وابستگی NuGet** — همهٔ فرمت‌ها و پروتکل‌ها دستی نوشته شده‌اند تا بستهٔ آفلاین و زنجیرهٔ مجوز ساده بماند.
- تنها دارایی شخص ثالث: **فونت وزیرمتن ۳۳٫۰۰۳** با مجوز SIL OFL (فایل مجوز در `src/Sahand.App/Fonts/OFL-Vazirmatn.txt`).
- وابستگی‌های آزمون (xUnit / Microsoft.NET.Test.Sdk) فقط در build آزمون استفاده می‌شوند.

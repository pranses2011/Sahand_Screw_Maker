using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sahand.App.Views;

/// <summary>
/// نمای درباره و حمایت — اطلاعات سازنده و نمایش پوشیده/کپی آگاهانهٔ شمارهٔ کارت (PRIV-02).
/// </summary>
/// <remarks>
/// شمارهٔ کارت فقط در همین صفحه است و در log/گزارش/فایل پروژه ذخیره نمی‌شود.
/// کلیک ایمیل/سایت نرم‌افزار بیرونی را خودکار باز نمی‌کند؛ نشانی انتخاب/کپی می‌شود
/// یا مرورگر فقط با تأیید کاربر (سند ۵-۷).
/// </remarks>
public partial class AboutView : UserControl
{
    // شمارهٔ کارت حمایت (سند ۵-۷) — فقط همین‌جا؛ هرگز در گزارش/پروژه/لاگ نمی‌نشیند.
    private const string SupportCardNumber = "6219861918934679";
    private const string MaskedDisplay = "6219 •••• •••• 4679";
    private bool _cardRevealed;

    public AboutView()
    {
        InitializeComponent();
        CardText.Text = MaskedDisplay;
    }

    private void OnRevealCardClick(object sender, RoutedEventArgs e)
    {
        // نمایش کامل فقط با اقدام آگاهانهٔ کاربر (سند ۵-۷).
        _cardRevealed = !_cardRevealed;
        CardText.Text = _cardRevealed ? FormatCardNumber(SupportCardNumber) : MaskedDisplay;
        RevealCardButton.Content = _cardRevealed ? "پوشاندن شماره" : "نمایش شمارهٔ کارت";
        CardHintText.Text = _cardRevealed
            ? "شماره اکنون آشکار است؛ پس از کپی، دوباره پوشانید."
            : string.Empty;
    }

    private void OnCopyCardClick(object sender, RoutedEventArgs e)
    {
        // کپی شمارهٔ کامل؛ پس از کپی فقط پیام تأیید کوتاه — بدون ذخیره در هیچ تاریخچهٔ داخلی.
        try
        {
            Clipboard.SetDataObject(SupportCardNumber, copy: true);
            CardHintText.Text = "شمارهٔ کارت کپی شد.";
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException)
        {
            // کلیپ‌بورد ویندوز ممکن است لحظه‌ای قفل باشد؛ محدودیت سیستم اعلام می‌شود.
            CardHintText.Text = "کلیپ‌بورد سیستم موقتاً در دسترس نیست؛ دوباره تلاش کنید.";
        }
    }

    private void OnEmailClick(object sender, MouseButtonEventArgs e)
    {
        // نرم‌افزار ایمیل باز نمی‌شود؛ نشانی انتخاب می‌شود تا کاربر خودش تصمیم بگیرد.
        try
        {
            EmailText.SelectAll();
            CardHintText.Text = "نشانی ایمیل انتخاب شد؛ با Ctrl+C کپی کنید.";
        }
        catch (Exception)
        {
            // انتخاب متن نباید خطا بدهد؛ بی‌صدا رد می‌شود.
        }
    }

    private void OnSiteClick(object sender, MouseButtonEventArgs e)
    {
        // سایت فقط با تأیید صریح در مرورگر بیرونی باز می‌شود (سند ۵-۲/۵-۷).
        var result = MessageBox.Show(
            "نشانی EA-Fixer.ir در مرورگر بیرونی باز شود؟",
            "بازکردن وب‌سایت",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            // نشانی http است؛ طبق سند ۵-۷ بدون تأیید https آن را ارتقا نمی‌دهیم.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "http://EA-Fixer.ir",
                UseShellExecute = true,
            });
        }
        catch (Exception)
        {
            CardHintText.Text = "بازکردن مرورگر ممکن نشد؛ نشانی: EA-Fixer.ir";
        }
    }

    private static string FormatCardNumber(string number)
    {
        // گروه‌بندی ۴تایی برای خوانایی — فقط نمایش.
        return string.Join(" ", Enumerable.Range(0, 4)
            .Select(i => number.Substring(i * 4, 4)));
    }
}

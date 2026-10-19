namespace Sahand.Core.Units;

/// <summary>
/// تبدیل ارقام فارسی/عربی و لاتین در هر دو جهت.
/// </summary>
/// <remarks>
/// نمایش رقم صرفاً ترجیح ظاهری کاربر است؛ مقدار عددی، واحد و خروجی CAD هرگز
/// از روی ظاهر رقم حدس زده نمی‌شوند (سند ۲-۱). این کلاس فقط در لایهٔ متن کار می‌کند.
/// </remarks>
public static class PersianDigits
{
    // کد نقاط: ارقام فارسی ۰..۹ = U+06F0..U+06F9؛ ارقام عربی ۰..۹ = U+0660..U+0669.
    private const int PersianZero = 0x06F0;
    private const int ArabicZero = 0x0660;

    /// <summary>
    /// ارقام فارسی و عربی متن را به لاتین تبدیل می‌کند (برای تجزیهٔ عددی امن).
    /// </summary>
    public static string NormalizeDigits(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var code = (int)chars[i];
            if (code is >= PersianZero and <= PersianZero + 9)
                chars[i] = (char)('0' + code - PersianZero);
            else if (code is >= ArabicZero and <= ArabicZero + 9)
                chars[i] = (char)('0' + code - ArabicZero);
        }
        return new string(chars);
    }

    /// <summary>
    /// ارقام لاتین متن را به ارقام فارسی تبدیل می‌کند (برای نمایش اختیاری).
    /// </summary>
    public static string ToPersian(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= '0' and <= '9')
                chars[i] = (char)(PersianZero + chars[i] - '0');
        }
        return new string(chars);
    }
}

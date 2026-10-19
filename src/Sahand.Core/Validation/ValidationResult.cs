namespace Sahand.Core.Validation;

/// <summary>شدت پیام اعتبارسنجی.</summary>
public enum ValidationSeverity
{
    /// <summary>خطا — ساخت مدل انجام نمی‌شود تا اصلاح شود.</summary>
    Error,

    /// <summary>هشدار — ساخت انجام می‌شود ولی ریسک/محدودیت به کاربر اعلام می‌شود.</summary>
    Warning,
}

/// <summary>
/// یک پیام اعتبارسنجی با ارجاع به پارامتر مربوطه (سند ۳-۷: پیام دقیق با نام پارامتر، محدوده و پیشنهاد).
/// </summary>
/// <param name="Severity">شدت پیام.</param>
/// <param name="ParameterName">نام پارامتر مسئله‌دار (مثل «PitchMm») یا نام زمینه.</param>
/// <param name="Message">متن فارسی قابل اقدام.</param>
public sealed record ValidationMessage(ValidationSeverity Severity, string ParameterName, string Message);

/// <summary>
/// نتیجهٔ اعتبارسنجی مجموع پارامترها — مبنای تصمیم UI برای فعال‌سازی «ساخت مدل».
/// </summary>
public sealed class ValidationResult
{
    private readonly List<ValidationMessage> _messages = [];

    /// <summary>پیام‌های ثبت‌شده به‌ترتیب درج.</summary>
    public IReadOnlyList<ValidationMessage> Messages => _messages;

    /// <summary>آیا خطای مسدودکننده وجود دارد؟</summary>
    public bool HasErrors => _messages.Any(m => m.Severity == ValidationSeverity.Error);

    /// <summary>آیا هشدار غیرمسدودکننده وجود دارد؟</summary>
    public bool HasWarnings => _messages.Any(m => m.Severity == ValidationSeverity.Warning);

    /// <summary>آیا کاملاً پاک است؟</summary>
    public bool IsValid => _messages.Count == 0;

    /// <summary>خطای مسدودکننده با نام پارامتر و متن فارسی می‌افزاید.</summary>
    public void AddError(string parameterName, string message) =>
        _messages.Add(new ValidationMessage(ValidationSeverity.Error, parameterName, message));

    /// <summary>هشدار غیرمسدودکننده می‌افزاید.</summary>
    public void AddWarning(string parameterName, string message) =>
        _messages.Add(new ValidationMessage(ValidationSeverity.Warning, parameterName, message));

    /// <summary>پیام‌های نتیجهٔ دیگر را با حفظ ترتیب ادغام می‌کند.</summary>
    public void Merge(ValidationResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        _messages.AddRange(other.Messages);
    }
}

namespace Sahand.Core.Undo;

/// <summary>
/// یک فرمان قابل برگرداندن — الگوی Command برای تغییرات مهم پارامترها (الزام P0 سند ۱-۶).
/// </summary>
public interface IUndoCommand
{
    /// <summary>عنوان فارسی فرمان برای نمایش در تاریخچهٔ Undo/Redo.</summary>
    string Title { get; }

    /// <summary>اثر فرمان را اعمال می‌کند (برای اجرای مجدد بعد از Undo).</summary>
    void Apply();

    /// <summary>اثر فرمان را برمی‌گرداند.</summary>
    void Revert();
}

/// <summary>
/// پشتهٔ Undo/Redo با سقف حافظهٔ مشخص و بدون نشت.
/// </summary>
/// <remarks>
/// در تغییر پارامترهای پرهزینهٔ هندسه، هر فرمان باید «وضعیت کامل قابل بازگشت» نگه دارد نه
/// deltaهای شکننده؛ سقف تاریخچه از رشد بی‌رویهٔ حافظه در جلسات طولانی جلوگیری می‌کند.
/// این کلاس thread-safe نیست و باید فقط روی رشتهٔ UI استفاده شود (قرارداد سند ۷-۳).
/// </remarks>
public sealed class CommandStack
{
    private readonly List<IUndoCommand> _undo = [];
    private readonly List<IUndoCommand> _redo = [];

    /// <summary>حداکثر تعداد فرمان قابل برگرداندن — قدیمی‌ها بی‌صدا حذف می‌شوند.</summary>
    public int Capacity { get; }

    /// <summary>آیا فرمانی برای برگرداندن هست؟</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>آیا فرمانی برای اجرای مجدد هست؟</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>عنوان فرمان بعدی برای Undo — برای نمایش در منو.</summary>
    public string? NextUndoTitle => CanUndo ? _undo[^1].Title : null;

    /// <summary>عنوان فرمان بعدی برای Redo.</summary>
    public string? NextRedoTitle => CanRedo ? _redo[^1].Title : null;

    public CommandStack(int capacity = 100)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), "ظرفیت باید حداقل ۱ باشد.");
        Capacity = capacity;
    }

    /// <summary>
    /// فرمان جدید را اجرا و روی پشته می‌گذارد؛ پشتهٔ Redo پاک می‌شود (رفتار استاندارد).
    /// </summary>
    public void Execute(IUndoCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Apply();
        _undo.Add(command);
        if (_undo.Count > Capacity)
            _undo.RemoveAt(0); // قدیمی‌ترین فرمان از دست می‌رود — سقف حافظه.
        _redo.Clear();
    }

    /// <summary>
    /// آخرین فرمان را برمی‌گرداند و به پشتهٔ Redo می‌برد.
    /// </summary>
    /// <exception cref="InvalidOperationException">اگر پشته خالی باشد.</exception>
    public void Undo()
    {
        if (!CanUndo) throw new InvalidOperationException("فرمانی برای برگرداندن وجود ندارد.");
        var command = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        command.Revert();
        _redo.Add(command);
    }

    /// <summary>
    /// آخرین فرمان برگردانده‌شده را دوباره اجرا می‌کند.
    /// </summary>
    /// <exception cref="InvalidOperationException">اگر پشتهٔ Redo خالی باشد.</exception>
    public void Redo()
    {
        if (!CanRedo) throw new InvalidOperationException("فرمانی برای اجرای مجدد وجود ندارد.");
        var command = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        command.Apply();
        _undo.Add(command);
    }

    /// <summary>هر دو پشته را خالی می‌کند (مثلاً هنگام بازکردن پروژهٔ جدید).</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}

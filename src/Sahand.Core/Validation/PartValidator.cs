using Sahand.Core.Parts;
using Sahand.Core.Threads;

namespace Sahand.Core.Validation;

/// <summary>
/// اعتبارسنج قطعات (پیچ/مهره/واشر/میل/مجموعه) پیش از ساخت هندسه.
/// </summary>
/// <remarks>
/// قواعد سند ۳-۷: عبور ناممکن اجزا، دیوارهٔ بی‌ضخامت، طول رزوهٔ بیشتر از قطعه،
/// و سازگاری محرک/سر. پیام‌ها نام پارامتر و پیشنهاد اصلاح دارند.
/// </remarks>
public static class PartValidator
{
    /// <summary>اعتبارسنجی کامل پیچ.</summary>
    public static ValidationResult Validate(BoltSpec bolt)
    {
        ArgumentNullException.ThrowIfNull(bolt);
        var result = ThreadValidator.Validate(bolt.Thread);
        var threadErrorsBefore = result.HasErrors;

        CheckPositive(result, "LengthUnderHeadMm", bolt.LengthUnderHeadMm, "طول ساق زیر سر");
        CheckPositive(result, "HeadHeightMm", bolt.HeadHeightMm, "ارتفاع سر");
        CheckNonNegative(result, "TipChamferMm", bolt.TipChamferMm, "پخ نوک");
        CheckNonNegative(result, "HeadTopChamferMm", bolt.HeadTopChamferMm, "پخ سر");
        if (result.HasErrors && !threadErrorsBefore) return result;

        // ماتریس پشتیبانی سر×محرک در این نسخه (محدودیت‌ها صادقانه رد می‌شوند، نه دکمهٔ بی‌عمل).
        if (bolt.Head == HeadStyle.Hex && bolt.Drive is DriveStyle.Slot or DriveStyle.Phillips)
            result.AddError("Drive",
                "شیار روی سر شش‌گوش در این نسخه پشتیبانی نمی‌شود (در برنامهٔ توسعه). محرک «بدون» یا «آلن» را انتخاب کنید.");

        if (bolt.Head is HeadStyle.FlatCountersunk or HeadStyle.Pan && bolt.Drive != DriveStyle.None)
            result.AddError("Drive",
                $"محرک روی سر «{bolt.Head}» در این نسخه پشتیبانی نمی‌شود (در برنامهٔ توسعه). سر استوانه‌ای آلن را انتخاب کنید.");

        // سر خزینه: ظرفیت مخروط باید رویهٔ بالایی را بزرگ‌تر از ساق نگه دارد (گارد هندسی سازنده هم هست).
        if (bolt.Head == HeadStyle.FlatCountersunk && bolt.HeadDiameterMm is { } headD)
        {
            var topRadius = headD / 2.0 - bolt.HeadHeightMm * Math.Tan(bolt.CountersunkAngleDegrees / 2.0 * Math.PI / 180.0);
            if (topRadius <= bolt.Thread.NominalDiameterMm / 2.0)
                result.AddError("HeadHeightMm",
                    $"ارتفاع سر خزینه ({bolt.HeadHeightMm:0.###}mm) برای قطر سر {headD:0.###}mm و زاویهٔ {bolt.CountersunkAngleDegrees:0.#}° بیش از حد بزرگ است؛ رویهٔ بالایی از ساق کوچک‌تر می‌شود. ارتفاع را کم یا قطر سر/زاویه را زیاد کنید.");
        }

        // محرک آلن فقط روی سرهایی معنا دارد که حفره می‌پذیرند.
        if (bolt.Drive == DriveStyle.HexSocket)
        {
            if (bolt.Head is not (HeadStyle.SocketCap or HeadStyle.Hex))
                result.AddError("Drive",
                    $"حفرهٔ آلن روی سر «{bolt.Head}» در این نسخه پشتیبانی نمی‌شود؛ سر استوانه‌ای آلن یا شش‌گوش را انتخاب کنید.");

            if (bolt.HexSocketAcrossFlatsMm is not > 0)
                result.AddError("HexSocketAcrossFlatsMm", "قطر حفرهٔ آلن باید مثبت باشد.");

            if (bolt.HexSocketDepthMm is not > 0)
                result.AddError("HexSocketDepthMm", "عمق حفرهٔ آلن باید مثبت باشد.");
            else if (bolt.HexSocketDepthMm >= bolt.HeadHeightMm)
                result.AddError("HexSocketDepthMm",
                    $"عمق حفرهٔ آلن ({bolt.HexSocketDepthMm:0.###}mm) نباید از ارتفاع سر ({bolt.HeadHeightMm:0.###}mm) بیشتر یا مساوی شود؛ دیوارهٔ ته‌حفره صفر می‌شود.");
            else if (bolt.HexSocketDepthMm > bolt.HeadHeightMm * 0.8)
                result.AddWarning("HexSocketDepthMm",
                    "عمق حفرهٔ آلن بیش از ۸۰٪ ارتفاع سر است؛ دیوارهٔ ته‌حفره نازک می‌شود.");

            if (bolt.HexSocketAcrossFlatsMm is > 0 && bolt.HeadDiameterMm is > 0 &&
                bolt.HexSocketAcrossFlatsMm * 1.1547 >= bolt.HeadDiameterMm)
                result.AddError("HexSocketAcrossFlatsMm",
                    "حفرهٔ آلن از قطر سر بزرگ‌تر می‌شود؛ قطر حفره یا قطر سر را اصلاح کنید.");
        }

        if (bolt.Drive is DriveStyle.Slot or DriveStyle.Phillips)
        {
            if (bolt.SlotWidthMm is not > 0)
                result.AddError("SlotWidthMm", "پهنای شیار باید مثبت باشد.");

            if (bolt.SlotDepthMm is not > 0)
                result.AddError("SlotDepthMm", "عمق شیار باید مثبت باشد.");
            else if (bolt.SlotDepthMm >= bolt.HeadHeightMm)
                result.AddError("SlotDepthMm",
                    $"عمق شیار ({bolt.SlotDepthMm:0.###}mm) نباید از ارتفاع سر ({bolt.HeadHeightMm:0.###}mm) بیشتر شود؛ سر سوراخ می‌شود.");
        }

        if (bolt.Head == HeadStyle.Hex && bolt.HeadAcrossFlatsMm is not > 0)
            result.AddError("HeadAcrossFlatsMm", "عرض آچارخور سر شش‌گوش باید مثبت باشد.");

        if (bolt.Head is HeadStyle.SocketCap or HeadStyle.Pan && bolt.HeadDiameterMm is not > 0)
            result.AddError("HeadDiameterMm", $"قطر سر «{bolt.Head}» باید مثبت باشد.");

        // سر شش‌گوش باید ساق را بپوشاند (بدون برش ناگهانی زیر سر).
        if (bolt.Head == HeadStyle.Hex && bolt.HeadAcrossFlatsMm is > 0 &&
            bolt.HeadAcrossFlatsMm * Math.Cos(Math.PI / 6.0) < bolt.Thread.NominalDiameterMm * 0.9)
            result.AddWarning("HeadAcrossFlatsMm",
                "عرض آچارخور نسبت به قطر ساق کوچک است؛ سر ممکن است در ستون نخ صاف نشود (تقریب نمایشی).");

        // طول رزوه از طول ساق بیشتر نباشد (مگر تمام‌رزوه).
        if (bolt.ThreadRunoutFromTipMm is { } runout)
        {
            if (runout <= 0)
                result.AddError("ThreadRunoutFromTipMm", "طول رزوه از نوک باید مثبت باشد یا برای تمام‌رزوه خالی بماند.");
            else if (runout > bolt.LengthUnderHeadMm)
                result.AddError("ThreadRunoutFromTipMm",
                    $"طول رزوه ({runout:0.###}mm) از طول ساق زیر سر ({bolt.LengthUnderHeadMm:0.###}mm) بیشتر است.");
        }

        if (bolt.TipChamferMm * 2.0 >= bolt.Thread.NominalDiameterMm)
            result.AddError("TipChamferMm",
                "پخ نوک بزرگ‌تر از نصف قطر ساق است؛ نوکی باقی نمی‌ماند.");

        if (bolt.Thread.ModelingMode == ThreadModelingMode.RealHelical &&
            bolt.Thread.ThreadLengthMm > 0 && bolt.Thread.PitchMm > 0)
        {
            // برای رزوهٔ واقعی، حداقل یک دندانهٔ کامل باید جا شود وگرنه هندسه بی‌معناست.
            var effectiveThreadLength = bolt.ThreadRunoutFromTipMm ?? bolt.LengthUnderHeadMm;
            if (effectiveThreadLength < bolt.Thread.PitchMm)
                result.AddWarning("ThreadLengthMm",
                    $"طول مؤثر رزوه ({effectiveThreadLength:0.###}mm) کمتر از یک گام ({bolt.Thread.PitchMm:0.###}mm) است؛ حتی یک دندانهٔ کامل ساخته نمی‌شود.");
        }

        return result;
    }

    /// <summary>اعتبارسنجی کامل مهره.</summary>
    public static ValidationResult Validate(NutSpec nut)
    {
        ArgumentNullException.ThrowIfNull(nut);
        var result = ThreadValidator.Validate(nut.Thread);

        CheckPositive(result, "AcrossFlatsMm", nut.AcrossFlatsMm, "عرض آچارخور مهره");
        CheckPositive(result, "HeightMm", nut.HeightMm, "ارتفاع مهره");
        CheckNonNegative(result, "ChamferMm", nut.ChamferMm, "پخ مهره");
        if (result.HasErrors) return result;

        // دیوارهٔ بی‌ضخامت: عرض آچارخور باید از قطر گوشه‌های حفرهٔ رزوه بزرگ‌تر باشد.
        var threadCrestDiameter = nut.Thread.NominalDiameterMm; // درهٔ داخلی روی قطر اسمی جفت.
        if (nut.AcrossFlatsMm <= threadCrestDiameter)
            result.AddError("AcrossFlatsMm",
                $"عرض آچارخور ({nut.AcrossFlatsMm:0.###}mm) باید از قطر رزوه ({threadCrestDiameter:0.###}mm) بزرگ‌تر باشد؛ دیواره بی‌ضخامت می‌شود.");

        else if (nut.AcrossFlatsMm < threadCrestDiameter * 1.3)
            result.AddWarning("AcrossFlatsMm",
                "عرض آچارخور به قطر رزوه نزدیک است؛ دیوارهٔ مهره نازک خواهد بود (برای مهرهٔ شش‌گوش متداول ~۱٫۵ برابر قطر توصیه می‌شود).");

        if (nut.ChamferMm * 2.0 >= Math.Min(nut.AcrossFlatsMm - threadCrestDiameter, nut.HeightMm))
            result.AddWarning("ChamferMm", "پخ مهره نسبت به ضخامت دیواره یا ارتفاع بزرگ است.");

        if (nut.HeightMm < nut.Thread.PitchMm)
            result.AddWarning("HeightMm",
                $"ارتفاع مهره ({nut.HeightMm:0.###}mm) کمتر از یک گام رزوه ({nut.Thread.PitchMm:0.###}mm) است؛ درگیری کامل نمی‌شود.");

        return result;
    }

    /// <summary>اعتبارسنجی واشر.</summary>
    public static ValidationResult Validate(WasherSpec washer)
    {
        ArgumentNullException.ThrowIfNull(washer);
        var result = new ValidationResult();

        CheckPositive(result, "InnerDiameterMm", washer.InnerDiameterMm, "قطر داخلی واشر");
        CheckPositive(result, "OuterDiameterMm", washer.OuterDiameterMm, "قطر خارجی واشر");
        CheckPositive(result, "ThicknessMm", washer.ThicknessMm, "ضخامت واشر");
        CheckNonNegative(result, "EdgeChamferMm", washer.EdgeChamferMm, "پخ لبه");
        if (result.HasErrors) return result;

        if (washer.InnerDiameterMm >= washer.OuterDiameterMm)
            result.AddError("OuterDiameterMm",
                $"قطر خارجی ({washer.OuterDiameterMm:0.###}mm) باید از قطر داخلی ({washer.InnerDiameterMm:0.###}mm) بزرگ‌تر باشد؛ واشری باقی نمی‌ماند.");

        if (washer.EdgeChamferMm * 2.0 >= washer.ThicknessMm)
            result.AddWarning("EdgeChamferMm", "پخ لبه از نصف ضخامت بزرگ‌تر است؛ ضخامت مؤثر خیلی کم می‌شود.");

        return result;
    }

    /// <summary>اعتبارسنجی میلهٔ رزوه‌دار.</summary>
    public static ValidationResult Validate(ThreadedRodSpec rod)
    {
        ArgumentNullException.ThrowIfNull(rod);
        var result = ThreadValidator.Validate(rod.Thread);

        CheckPositive(result, "TotalLengthMm", rod.TotalLengthMm, "طول میله");
        CheckNonNegative(result, "EndChamferMm", rod.EndChamferMm, "پخ انتها");
        if (result.HasErrors) return result;

        if (rod.SmoothCenterLengthMm is { } smooth)
        {
            if (smooth < 0)
                result.AddError("SmoothCenterLengthMm", "طول بخش صاف نمی‌تواند منفی باشد.");
            else if (smooth >= rod.TotalLengthMm)
                result.AddError("SmoothCenterLengthMm",
                    $"طول بخش صاف ({smooth:0.###}mm) باید از طول کل ({rod.TotalLengthMm:0.###}mm) کمتر باشد؛ جای رزوه باقی نمی‌ماند.");
        }

        if (rod.EndChamferMm * 2.0 >= rod.Thread.NominalDiameterMm)
            result.AddError("EndChamferMm", "پخ انتها بزرگ‌تر از نصف قطر است.");

        return result;
    }

    /// <summary>اعتبارسنجی مجموعهٔ جفت شامل سازگاری رزوه.</summary>
    public static ValidationResult Validate(AssemblySpec assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var result = Validate(assembly.Bolt);

        if (assembly.Nut is not null)
        {
            result.Merge(Validate(assembly.Nut));

            var compatibility = assembly.CheckThreadCompatibility();
            foreach (var issue in compatibility.Issues)
                result.AddError("ThreadCompatibility", issue);
        }

        if (assembly.Washer is not null)
            result.Merge(Validate(assembly.Washer));

        if (assembly.DisplayGapMm < 0)
            result.AddError("DisplayGapMm", "فاصلهٔ نمایشی مجموعه نمی‌تواند منفی باشد.");

        return result;
    }

    /// <summary>بررسی مثبت‌بودن مقدار عددی پیش از ورود به موتور هندسه.</summary>
    private static void CheckPositive(ValidationResult result, string parameterName, double value, string faName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            result.AddError(parameterName, $"{faName} مقدار عددی معتبر نیست.");
            return;
        }

        if (value <= 0)
            result.AddError(parameterName, $"{faName} باید مثبت باشد (مقدار فعلی: {value:0.###}).");

        if (value > 1e6)
            result.AddError(parameterName, $"{faName} خارج از محدودهٔ معقول است (>۱۰۰۰ متر).");
    }

    private static void CheckNonNegative(ValidationResult result, string parameterName, double value, string faName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            result.AddError(parameterName, $"{faName} مقدار عددی معتبر نیست.");
            return;
        }

        if (value < 0)
            result.AddError(parameterName, $"{faName} نمی‌تواند منفی باشد (مقدار فعلی: {value:0.###}).");
    }
}

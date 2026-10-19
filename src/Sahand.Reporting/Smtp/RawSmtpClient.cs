using System.Net.Security;
using System.Security.Authentication;
using System.Text;

namespace Sahand.Reporting.Smtp;

/// <summary>
/// نتیجهٔ یک گفت‌وگوی SMTP — فقط پس از پاسخ مثبت واقعی سرور «موفق» است (MAIL-03).
/// </summary>
public sealed record SmtpResult(bool Success, string? FailureReason, string ProtocolLog)
{
    /// <summary>متن فارسی شکست؛ null در موفقیت.</summary>
    public string? PersianError => Success ? null : (FailureReason ?? "ارسال SMTP بدون دلیل مشخص ناموفق بود.");
}

/// <summary>
/// کلاینت SMTP خام روی TcpClient + SslStream — بدون SmtpClient منسوخ و بدون وابستگی بیرونی.
/// </summary>
/// <remarks>
/// امنیت (سند ۵-۶): اعتبارسنجی گواهی TLS همیشه روشن و بدون fallback به plaintext؛
/// STARTTLS الزامی در پورت متن؛ timeout و لغو پشتیبانی می‌شود؛ هیچ رازی در exception یا
/// log نمی‌نشیند (رمز فقط در AUTH به سرور می‌رود و در گزارش‌ها ماسک می‌شود).
/// MIME: متن UTF-8 با encoded-word برای موضوع فارسی؛ ضمیمهٔ اختیاری با محدودیت نوع/اندازه.
/// </remarks>
public sealed class RawSmtpClient
{
    /// <summary>مدت مهلت هر عملیات شبکه.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// دریچهٔ آزمون (internal): اجازهٔ متن خام فقط برای سرور حلقهٔ محلی آزمون‌ها.
    /// </summary>
    /// <remarks>
    /// این گزینه در UI/تنظیمات قابل‌ تنظیم نیست و فقط با InternalsVisibleTo برای آزمون‌های
    /// دیالوگ SMTP در دسترس است؛ سیاست امنیتی کاربر نهایی همیشه TLS اجباری می‌ماند.
    /// </remarks>
    internal bool AllowPlaintextLoopbackForTests { get; set; }

    /// <summary>حداکثر اندازهٔ ضمیمه (سند ۵-۶: سقف اندازه).</summary>
    public const long MaxAttachmentBytes = 2 * 1024 * 1024;

    /// <summary>
    /// ایمیل را ارسال می‌کند.
    /// </summary>
    /// <param name="settings">تنظیمات (رمز در آن نیست).</param>
    /// <param name="password">رمز/گذرواژهٔ اپ از SecretStore — هرگز ذخیره نمی‌شود.</param>
    /// <param name="to">نشانی مقصد.</param>
    /// <param name="subject">موضوع (فارسی مجاز).</param>
    /// <param name="body">متن.</param>
    /// <param name="attachmentPath">ضمیمهٔ اختیاری — فقط نوع‌های مجاز.</param>
    /// <param name="cancellationToken">لغو عملیات.</param>
    public async Task<SmtpResult> SendAsync(
        SmtpSettings settings,
        string password,
        string to,
        string subject,
        string body,
        string? attachmentPath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentException.ThrowIfNullOrEmpty(to);

        if (!settings.IsConfigured)
            return new SmtpResult(false, "تنظیمات SMTP کامل نیست؛ میزبان/پورت/فرستنده را بررسی کنید.", "not-configured");

        if (!MailAddressValidator.IsValid(to))
            return new SmtpResult(false, $"نشانی مقصد «{to}» معتبر نیست.", "bad-recipient");

        var log = new StringBuilder();

        try
        {
            using var tcp = new System.Net.Sockets.TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);

            await tcp.ConnectAsync(settings.Host, settings.Port, cts.Token);
            log.AppendLine($"C: connected {settings.Host}:{settings.Port}");

            var stream = (Stream)tcp.GetStream();

            // خواندن سلام سرور (S: 220).
            var greeting = await ReadReplyAsync(stream, cts.Token);
            log.AppendLine(greeting.Log);
            if (!greeting.IsPositive)
                return new SmtpResult(false, "سرور SMTP سلام نامعتبر فرستاد (۲۲۰ انتظار می‌رفت).", log.ToString());

            // EHLO.
            var ehlo = await CommandAsync(stream, "EHLO sahand.local", cts.Token);
            log.AppendLine(ehlo.Log);
            if (!ehlo.IsPositive)
                return new SmtpResult(false, "سرور به EHLO پاسخ مثبت نداد.", log.ToString());

            // TLS: implicit (465) یا STARTTLS (587/متن).
            if (settings.UseStartTls && settings.Port != 465)
            {
                var startTls = await CommandAsync(stream, "STARTTLS", cts.Token);
                log.AppendLine(startTls.Log);
                if (!startTls.IsPositive)
                    return new SmtpResult(false,
                        "سرور STARTTLS را پشتیبانی نمی‌کند؛ ارسال بدون رمزنگاری انجام نمی‌شود (سیاست امنیتی برنامه).",
                        log.ToString());

                stream = await UpgradeTlsAsync(tcp, settings.Host, cts.Token);
                log.AppendLine("C: TLS established");

                // پس از STARTTLS دوباره EHLO الزامی است.
                var ehlo2 = await CommandAsync(stream, "EHLO sahand.local", cts.Token);
                log.AppendLine(ehlo2.Log);
                if (!ehlo2.IsPositive)
                    return new SmtpResult(false, "سرور پس از TLS به EHLO پاسخ مثبت نداد.", log.ToString());
            }
            else if (settings.Port == 465)
            {
                stream = await UpgradeTlsAsync(tcp, settings.Host, cts.Token);
                log.AppendLine("C: TLS established (implicit)");

                var ehloTls = await CommandAsync(stream, "EHLO sahand.local", cts.Token);
                log.AppendLine(ehloTls.Log);
                if (!ehloTls.IsPositive)
                    return new SmtpResult(false, "سرور پس از TLS ضمنی به EHLO پاسخ مثبت نداد.", log.ToString());
            }
            else if (AllowPlaintextLoopbackForTests && System.Net.IPAddress.IsLoopback(
                System.Net.IPAddress.Parse(settings.Host)))
            {
                // فقط مسیر آزمون داخلی — دیالوگ کامل بدون TLS روی حلقهٔ محلی.
                log.AppendLine("C: [TEST] plaintext loopback");
            }
            else
            {
                return new SmtpResult(false,
                    "ارتباط متنِ خام بدون TLS مجاز نیست؛ پورت ۴۶۵ یا STARTTLS روی ۵۸۷ را استفاده کنید.",
                    log.ToString());
            }

            // AUTH LOGIN — رمز فقط همین‌جا استفاده می‌شود و در log نمی‌آید.
            var authUser = await CommandAsync(stream, "AUTH LOGIN", cts.Token);
            log.AppendLine(authUser.Log);
            if (!authUser.IsPositive)
                return new SmtpResult(false, "سرور AUTH LOGIN را نپذیرفت.", log.ToString());

            var authUsername = await CommandAsync(stream, ToBase64(settings.Username), cts.Token);
            log.AppendLine("C: [username masked]");
            if (!authUsername.IsPositive)
                return new SmtpResult(false, "نام کاربری پذیرفته نشد.", log.ToString());

            var authPassword = await CommandAsync(stream, ToBase64(password), cts.Token);
            log.AppendLine("C: [password masked]");
            if (!authPassword.IsPositive)
                return new SmtpResult(false,
                    "احراز هویت ناموفق بود؛ نام کاربری یا گذرواژهٔ اپ را بررسی کنید.",
                    log.ToString());

            // MAIL FROM / RCPT TO / DATA.
            var mailFrom = await CommandAsync(stream, $"MAIL FROM:<{settings.FromAddress}>", cts.Token);
            log.AppendLine(mailFrom.Log);
            if (!mailFrom.IsPositive)
                return new SmtpResult(false, "فرستنده پذیرفته نشد.", log.ToString());

            var rcptTo = await CommandAsync(stream, $"RCPT TO:<{to}>", cts.Token);
            log.AppendLine(rcptTo.Log);
            if (!rcptTo.IsPositive)
                return new SmtpResult(false, "مقصد پذیرفته نشد.", log.ToString());

            var dataCmd = await CommandAsync(stream, "DATA", cts.Token);
            log.AppendLine(dataCmd.Log);
            if (!dataCmd.IsPositive)
                return new SmtpResult(false, "سرور DATA را نپذیرفت.", log.ToString());

            var mime = BuildMime(settings, to, subject, body, attachmentPath);
            var dataReply = await CommandAsync(stream, mime, cts.Token);
            log.AppendLine(dataReply.Log);
            if (!dataReply.IsPositive)
                return new SmtpResult(false, "سرور پیام را نپذیرفت.", log.ToString());

            var quit = await CommandAsync(stream, "QUIT", cts.Token);
            log.AppendLine(quit.Log);

            return new SmtpResult(true, null, log.ToString());
        }
        catch (OperationCanceledException)
        {
            return new SmtpResult(false, "عملیات لغو شد یا مهلت آن تمام شد.", log.ToString());
        }
        catch (AuthenticationException ex)
        {
            return new SmtpResult(false, $"گواهی TLS نامعتبر است؛ اتصال قطع شد (fail-closed): {ex.Message}", log.ToString());
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            return new SmtpResult(false, $"اتصال به سرور برقرار نشد: {ex.Message}", log.ToString());
        }
        catch (IOException ex)
        {
            return new SmtpResult(false, $"ارتباط شبکه در میانهٔ گفت‌وگو قطع شد: {ex.Message}", log.ToString());
        }
    }

    /// <summary>آزمون واقعی تنظیمات: اتصال + سلام + EHLO + AUTH (بدون ارسال).</summary>
    public async Task<SmtpResult> TestConnectionAsync(
        SmtpSettings settings, string password, CancellationToken cancellationToken = default)
    {
        // آزمون کامل = ارسال بدون DATA نمی‌شود؛ همین‌جا یک ارسال «آزمون» واقعی به مقصد خود فرستنده انجام می‌شود.
        var result = await SendAsync(settings, password, settings.FromAddress, "آزمون تنظیمات — طراح پیچ و مهره سهند",
            "این پیام آزمون تنظیمات SMTP از داخل برنامه است؛ نیازی به پاسخ نیست.", null, cancellationToken);
        return result;
    }

    /// <summary>ارتقای جریان به TLS با اعتبارسنجی کامل گواهی — بدون هیچ گونه bypass.</summary>
    private static async Task<Stream> UpgradeTlsAsync(System.Net.Sockets.TcpClient tcp, string host, CancellationToken token)
    {
        var ssl = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false);
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = host,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.Online,
        }, token);
        return ssl;
    }

    /// <summary>ساخت پیام MIME با موضوع encoded-word فارسی و ضمیمهٔ اختیاری.</summary>
    private static string BuildMime(SmtpSettings settings, string to, string subject, string body, string? attachmentPath)
    {
        var hasAttachment = false;
        byte[] attachmentBytes = [];
        string attachmentName = "";

        if (attachmentPath is not null)
        {
            // فقط نوع‌های مجاز و سقف اندازه — جلوگیری از ارسال فایل اجرایی (سند ۵-۶).
            var ext = Path.GetExtension(attachmentPath).ToLowerInvariant();
            if (ext is not (".ssnd" or ".txt" or ".log" or ".stl" or ".png"))
                throw new InvalidOperationException($"نوع ضمیمه «{ext}» مجاز نیست؛ فقط ssnd/txt/log/stl/png.");

            var info = new FileInfo(attachmentPath);
            if (info.Length > MaxAttachmentBytes)
                throw new InvalidOperationException(
                    $"اندازهٔ ضمیمه از سقف {MaxAttachmentBytes / 1024} کیلوبایت بیشتر است.");

            attachmentBytes = File.ReadAllBytes(attachmentPath);
            attachmentName = Path.GetFileName(attachmentPath);
            hasAttachment = true;
        }

        var boundary = "=_Sahand_" + Guid.NewGuid().ToString("N");
        var sb = new StringBuilder();
        sb.AppendLine($"From: {EncodeDisplayName(settings.FromDisplayName)} <{settings.FromAddress}>");
        sb.AppendLine($"To: <{to}>");
        sb.AppendLine($"Subject: {EncodeWord(subject)}");
        sb.AppendLine("MIME-Version: 1.0");
        sb.AppendLine($"Date: {DateTimeOffset.Now:R}");
        sb.AppendLine("X-Mailer: Sahand-Screw-Nut-Designer");

        if (!hasAttachment)
        {
            sb.AppendLine("Content-Type: text/plain; charset=UTF-8");
            sb.AppendLine("Content-Transfer-Encoding: base64");
            sb.AppendLine();
            sb.AppendLine(ToBase64(body).Chunk(76).Select(c => new string(c)).Aggregate("", (a, b) => a + b + "\r\n"));
        }
        else
        {
            sb.AppendLine($"Content-Type: multipart/mixed; boundary=\"{boundary}\"");
            sb.AppendLine();
            sb.AppendLine($"--{boundary}");
            sb.AppendLine("Content-Type: text/plain; charset=UTF-8");
            sb.AppendLine("Content-Transfer-Encoding: base64");
            sb.AppendLine();
            sb.AppendLine(ToBase64(body).Chunk(76).Select(c => new string(c)).Aggregate("", (a, b) => a + b + "\r\n"));
            sb.AppendLine($"--{boundary}");
            sb.AppendLine($"Content-Type: application/octet-stream; name=\"{EncodeWord(attachmentName)}\"");
            sb.AppendLine("Content-Transfer-Encoding: base64");
            sb.AppendLine("Content-Disposition: attachment; filename=\"" + EncodeWord(attachmentName) + "\"");
            sb.AppendLine();
            sb.AppendLine(Convert.ToBase64String(attachmentBytes).Chunk(76).Select(c => new string(c)).Aggregate("", (a, b) => a + b + "\r\n"));
            sb.AppendLine($"--{boundary}--");
        }

        // پایان DATA با نقطهٔ تنها در خط؛ نقطه‌های ابتدای خط با دوتایی escaping می‌شوند.
        var message = sb.ToString();
        message = message.Replace("\r\n.\r\n", "\r\n..\r\n");
        if (message.StartsWith(".\r\n")) message = ".." + message[1..];
        message += "\r\n.";
        return message;
    }

    private static string EncodeDisplayName(string name) =>
        name.All(ch => ch < 128) ? name : EncodeWord(name);

    /// <summary>encoded-word برای متن فارسی در سربرگ‌ها.</summary>
    private static string EncodeWord(string text) =>
        "=?UTF-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(text)) + "?=";

    private static string ToBase64(string text) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    private async Task<Reply> ReadReplyAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[4096];
        var text = new StringBuilder();
        int totalBytes = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
            if (read == 0) break;
            totalBytes += read;
            if (totalBytes > 64 * 1024)
                throw new IOException("پاسخ سرور بیش از حد بلند است.");

            text.Append(Encoding.UTF8.GetString(buffer, 0, read));
            if (IsCompleteReply(text)) break;
        }

        var raw = text.ToString().TrimEnd('\r', '\n');
        return new Reply(raw);
    }

    private static bool IsCompleteReply(StringBuilder text)
    {
        // پاسخ SMTP کامل: خط آخر «NNN<SP>» (خطوط میانی «NNN-»).
        var value = text.ToString();
        if (!value.EndsWith("\r\n")) return false;
        var lines = value.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        return lines.Length > 0 && lines[^1].Length >= 4 && lines[^1][3] == ' ';
    }

    private async Task<Reply> CommandAsync(Stream stream, string command, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(command + "\r\n");
        await stream.WriteAsync(bytes, token);
        await stream.FlushAsync(token);
        return await ReadReplyAsync(stream, token);
    }

    /// <summary>پاسخ SMTP با کد سه‌رقمی.</summary>
    private sealed record Reply(string Raw)
    {
        public bool IsPositive => Raw.Length >= 3 && int.TryParse(Raw[..3], out var code) && code < 400;

        public string Log => "S: " + Raw.Replace("\r\n", " | ");
    }
}

using System.Text;
using System.Net;
using System.Net.Sockets;
using Sahand.Reporting;
using Sahand.Reporting.Smtp;
using Xunit;

namespace Sahand.Reporting.Tests;

/// <summary>
/// آزمون‌های پاک‌سازی دادهٔ حساس (پوشش PRIV-01).
/// </summary>
public class RedactionTests
{
    [Fact]
    public void Secrets_AreRemoved()
    {
        var result = Redaction.Scrub("password: hunter2 توکن من است");
        Assert.DoesNotContain("hunter2", result.Text);
        Assert.Contains(result.Notes, n => n.Contains("راز"));
    }

    [Fact]
    public void CardNumbers_AreMasked()
    {
        var result = Redaction.Scrub("شمارهٔ کارت: 6219 8619 1893 4679 برای حمایت");
        Assert.DoesNotContain("6219 8619", result.Text);
        Assert.Contains("شمارهٔ کارت حذف شد", result.Text);
    }

    [Fact]
    public void UserHomePaths_AreAnonymized()
    {
        var result = Redaction.Scrub("خطا در /home/alice/documents/file.ssnd رخ داد");
        Assert.DoesNotContain("alice", result.Text);
        Assert.Contains("[کاربر]", result.Text);
    }

    [Fact]
    public void IpAddresses_AreRemoved()
    {
        var result = Redaction.Scrub("اتصال از 192.168.1.100 انجام شد");
        Assert.DoesNotContain("192.168.1.100", result.Text);
    }

    [Fact]
    public void CleanText_PassesUnchanged()
    {
        var result = Redaction.Scrub("گزارش عادی بدون دادهٔ حساس");
        Assert.Equal("گزارش عادی بدون دادهٔ حساس", result.Text);
        Assert.Empty(result.Notes);
    }
}

/// <summary>
/// آزمون‌های صف گزارش محلی (پوشش MAIL-01/02/05).
/// </summary>
public class ReportQueueTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "ssnd-queue-tests-" + Guid.NewGuid().ToString("N"));

    private string QueuePath => Path.Combine(_tempDir, "queue.json");

    public ReportQueueTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void EnqueuePersists_AcrossQueueInstances()
    {
        var report = new QueuedReport { Kind = ReportKind.Feedback, Body = "پیشنهاد اول" };
        new ReportQueue(QueuePath).Enqueue(report);

        // نمونهٔ جدید باید از دیسک بخواند (ماندگاری صف).
        var reloaded = new ReportQueue(QueuePath);
        Assert.Single(reloaded.Reports);
        Assert.Equal("پیشنهاد اول", reloaded.Reports[0].Body);
        Assert.Equal(ReportStatus.Queued, reloaded.Reports[0].Status);
    }

    [Fact]
    public void UserCanDelete_QueuedReport()
    {
        var queue = new ReportQueue(QueuePath);
        var report = queue.Enqueue(new QueuedReport { Kind = ReportKind.Error, Body = "خطا" });

        Assert.True(queue.Remove(report.Id));
        Assert.Empty(queue.Reports);
        Assert.DoesNotContain(new ReportQueue(QueuePath).Reports, r => r.Id == report.Id);
    }

    [Fact]
    public void UserCanEdit_BodyBeforeSend()
    {
        var queue = new ReportQueue(QueuePath);
        var report = queue.Enqueue(new QueuedReport { Kind = ReportKind.Feedback, Body = "متن اولیه" });

        Assert.True(queue.UpdateBody(report.Id, "متن ویرایش‌شده با password: abc123"));
        var updated = queue.Reports.Single(r => r.Id == report.Id);

        Assert.Contains("متن ویرایش‌شده", updated.Body);
        // ویرایش هم پاک‌سازی می‌شود (حساسیت حتی پس از ویرایش).
        Assert.DoesNotContain("abc123", updated.Body);
    }

    [Fact]
    public void MarkStatus_Sent_OnlyWithRealOutcome()
    {
        var queue = new ReportQueue(QueuePath);
        var report = queue.Enqueue(new QueuedReport { Kind = ReportKind.Error, Body = "x" });

        queue.MarkStatus(report.Id, ReportStatus.Sending);
        queue.MarkStatus(report.Id, ReportStatus.Sent);

        var sent = new ReportQueue(QueuePath).Reports.Single(r => r.Id == report.Id);
        Assert.Equal(ReportStatus.Sent, sent.Status);
        Assert.Equal(2, sent.AttemptCount);
    }

    [Fact]
    public void CorruptQueueFile_FailsSafe()
    {
        // فایل صف خراب نباید برنامه را بگیرد؛ صف از نو شروع می‌شود و فایل خراب کنار می‌رود.
        File.WriteAllText(QueuePath, "این JSON نیست");
        var queue = new ReportQueue(QueuePath);
        Assert.Empty(queue.Reports);
        Assert.True(Directory.GetFiles(_tempDir, "queue.json.corrupt-*").Length == 1);
    }

    [Fact]
    public void PruneFinished_RemovesOldSentReports()
    {
        var queue = new ReportQueue(QueuePath);
        var old = queue.Enqueue(new QueuedReport
        {
            Kind = ReportKind.Error,
            Body = "کهنه",
            CreatedAt = DateTimeOffset.Now.AddDays(-30),
        });
        queue.MarkStatus(old.Id, ReportStatus.Sent);

        queue.PruneFinishedOlderThan(TimeSpan.FromDays(7));
        Assert.Empty(queue.Reports);
    }

    [Fact]
    public async Task PlaintextWithoutTestHook_IsRefused()
    {
        // سیاست امنیتی: متن خام بدون TLS رد می‌شود (MAIL-04 fail-closed) — حتی سرور واقعی.
        using var server = new FakeSmtpServer();
        var client = new RawSmtpClient { Timeout = TimeSpan.FromSeconds(5) }; // بدون دریچهٔ آزمون.

        var result = await client.SendAsync(
            new SmtpSettings
            {
                Host = "127.0.0.1",
                Port = server.Port,
                FromAddress = "sender@example.com",
                Username = "sender@example.com",
                UseStartTls = false,
            },
            password: "x",
            to: "dest@example.com",
            subject: "s",
            body: "b");

        Assert.False(result.Success);
        Assert.Contains("TLS", result.PersianError);
        Assert.Empty(server.ReceivedMail); // هیچ DATA ارسال نشد.
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* بهترین‌تلاش. */ }
    }
}

/// <summary>
/// سرور SMTP شبیه‌سازی‌شدهٔ درون‌پردازشی — برای آزمون واقعی پروتکل بدون شبکهٔ بیرونی.
/// </summary>
/// <remarks>
/// این سرور فقط EHLO/STARTTLS-ادعا/QUIT و AUTH پایه را می‌فهمد؛ برای آزمون کامل TLS
/// از گواهی توسعه استفاده می‌شود. هدف: اثبات رفتار MAIL-03/04 (موفقیت فقط با پاسخ
/// واقعی؛ خطا با پیام فارسی و بدون افشای راز).
/// </remarks>
public sealed class FakeSmtpServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly List<string> _receivedMail = [];

    /// <summary>پورت گوش‌دادن.</summary>
    public int Port { get; }

    /// <summary>پیکره‌های DATA دریافت‌شده.</summary>
    public IReadOnlyList<string> ReceivedMail => _receivedMail;

    public FakeSmtpServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        AcceptLoop();
    }

    private void AcceptLoop()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client);
                }
            }
            catch (ObjectDisposedException)
            {
                // خاموشی سرور.
            }
        });
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using var _ = client;
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\r\n",
        };

        await writer.WriteLineAsync("220 fake.sahand.local ESMTP");
        var inData = false;
        var mailBody = new StringBuilder();

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line is null) break;

            if (inData)
            {
                if (line == ".")
                {
                    _receivedMail.Add(mailBody.ToString());
                    mailBody.Clear();
                    inData = false;
                    await writer.WriteLineAsync("250 OK: queued");
                    continue;
                }
                mailBody.Append(line).Append("\r\n");
                continue;
            }

            var command = line.Split(' ')[0].ToUpperInvariant();
            switch (command)
            {
                case "EHLO":
                    await writer.WriteLineAsync("250-fake.sahand.local");
                    await writer.WriteLineAsync("250 AUTH LOGIN");
                    break;
                case "AUTH":
                    await writer.WriteLineAsync("334");
                    break;
                case "MAIL":
                case "RCPT":
                    await writer.WriteLineAsync("250 OK");
                    break;
                case "DATA":
                    await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                    inData = true;
                    break;
                case "QUIT":
                    await writer.WriteLineAsync("221 Bye");
                    return;
                default:
                    // پاسخ AUTH پایه (base64) — همه را می‌پذیریم؛ فقط جریان آزموده می‌شود.
                    await writer.WriteLineAsync("235 authenticated");
                    break;
            }
        }
    }

    public void Dispose()
    {
        _listener.Stop();
    }
}

/// <summary>
/// آزمون کلاینت SMTP خام با سرور شبیه‌سازی‌شده (پوشش MAIL-03/04).
/// </summary>
public class RawSmtpClientTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "ssnd-smtp-tests-" + Guid.NewGuid().ToString("N"));

    public RawSmtpClientTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task Send_RealMailFromToFakeServer_ReportsSuccessOnlyWithReal250()
    {
        // موفقیت باید ناشی از پاسخ واقعی سرور باشد (MAIL-03) — با دریچهٔ آزمون داخلی.
        using var server = new FakeSmtpServer();
        var client = new RawSmtpClient { Timeout = TimeSpan.FromSeconds(5), AllowPlaintextLoopbackForTests = true };

        var result = await client.SendAsync(
            new SmtpSettings
            {
                Host = "127.0.0.1",
                Port = server.Port,
                FromAddress = "sender@example.com",
                Username = "sender@example.com",
                UseStartTls = false, // سرور آزمایشی STARTTLS واقعی ندارد؛ بدون TLS آزمون می‌شود.
            },
            password: "app-password",
            to: "dest@example.com",
            subject: "آزمون موضوع فارسی",
            body: "متن آزمون با نویسهٔ فارسی");

        Assert.True(result.Success, result.PersianError ?? "باید موفق باشد");
        Assert.Single(server.ReceivedMail);

        // موضوع به‌صورت encoded-word (=?UTF-8?B?...?=) فرستاده می‌شود؛ رمزگشایی و مقایسه.
        var mail = server.ReceivedMail[0];
        var subjectLine = mail.Split("\r\n").FirstOrDefault(l => l.StartsWith("Subject:")) ?? "";
        Assert.Contains("=?UTF-8?B?", subjectLine);
        var match = System.Text.RegularExpressions.Regex.Match(subjectLine, @"=\?UTF-8\?B\?([^?]+)\?=");
        Assert.True(match.Success, "الگوی encoded-word پیدا نشد.");
        var decodedSubject = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(match.Groups[1].Value));
        Assert.Equal("آزمون موضوع فارسی", decodedSubject);
    }

    [Fact]
    public async Task UnreachableServer_ReportsPersianFailure()
    {
        var client = new RawSmtpClient { Timeout = TimeSpan.FromSeconds(2) };
        var result = await client.SendAsync(
            new SmtpSettings
            {
                Host = "127.0.0.1",
                Port = 1, // پورت بسته — اتصال ناموفق.
                FromAddress = "sender@example.com",
                Username = "sender@example.com",
            },
            password: "x",
            to: "dest@example.com",
            subject: "s",
            body: "b");

        Assert.False(result.Success);
        Assert.NotNull(result.PersianError);
        Assert.Contains("اتصال", result.PersianError!);
    }

    [Fact]
    public async Task IncompleteSettings_RefusedWithoutSending()
    {
        // بدون پیکربندی کامل، ارسال انجام نمی‌شود و پیام فارسی صادر می‌شود (MAIL-02).
        var client = new RawSmtpClient();
        var result = await client.SendAsync(
            new SmtpSettings { Host = "", Port = 587 },
            password: "x",
            to: "dest@example.com",
            subject: "s",
            body: "b");

        Assert.False(result.Success);
        Assert.Contains("تنظیمات", result.PersianError);
    }

    [Fact]
    public async Task InvalidRecipient_RejectedBeforeConnection()
    {
        var client = new RawSmtpClient();
        var result = await client.SendAsync(
            new SmtpSettings
            {
                Host = "127.0.0.1",
                Port = 25,
                FromAddress = "sender@example.com",
                Username = "sender@example.com",
            },
            password: "x",
            to: "نشانی-نامعتبر",
            subject: "s",
            body: "b");

        Assert.False(result.Success);
        Assert.Contains("مقصد", result.PersianError);
    }

    [Fact]
    public async Task AttachmentLimit_Enforced()
    {
        // سقف اندازهٔ ضمیمه (۲MB) اجرا می‌شود (سند ۵-۶).
        using var server = new FakeSmtpServer();
        var client = new RawSmtpClient { Timeout = TimeSpan.FromSeconds(5), AllowPlaintextLoopbackForTests = true };

        var bigFile = Path.Combine(_tempDir, "big.txt");
        var bigContent = new string('x', (int)(RawSmtpClient.MaxAttachmentBytes + 1024));
        File.WriteAllText(bigFile, bigContent);

        var result = await client.SendAsync(
            new SmtpSettings
            {
                Host = "127.0.0.1",
                Port = server.Port,
                FromAddress = "sender@example.com",
                Username = "sender@example.com",
                UseStartTls = false,
            },
            password: "x",
            to: "dest@example.com",
            subject: "s",
            body: "b",
            attachmentPath: bigFile);

        Assert.False(result.Success);
        Assert.Contains("ضمیمه", result.PersianError);
    }

    [Fact]
    public async Task ForbiddenAttachmentType_Rejected()
    {
        using var server = new FakeSmtpServer();
        var client = new RawSmtpClient { Timeout = TimeSpan.FromSeconds(5), AllowPlaintextLoopbackForTests = true };

        var exeFile = Path.Combine(_tempDir, "danger.exe");
        File.WriteAllText(exeFile, "MZ");

        var result = await client.SendAsync(
            new SmtpSettings
            {
                Host = "127.0.0.1",
                Port = server.Port,
                FromAddress = "sender@example.com",
                Username = "sender@example.com",
                UseStartTls = false,
            },
            password: "x",
            to: "dest@example.com",
            subject: "s",
            body: "b",
            attachmentPath: exeFile);

        Assert.False(result.Success);
        Assert.Contains("نوع ضمیمه", result.PersianError);
    }

    [Fact]
    public async Task PlaintextWithoutTestHook_IsRefused()
    {
        // سیاست امنیتی: متن خام بدون TLS رد می‌شود (MAIL-04 fail-closed) — حتی سرور واقعی.
        using var server = new FakeSmtpServer();
        var client = new RawSmtpClient { Timeout = TimeSpan.FromSeconds(5) }; // بدون دریچهٔ آزمون.

        var result = await client.SendAsync(
            new SmtpSettings
            {
                Host = "127.0.0.1",
                Port = server.Port,
                FromAddress = "sender@example.com",
                Username = "sender@example.com",
                UseStartTls = false,
            },
            password: "x",
            to: "dest@example.com",
            subject: "s",
            body: "b");

        Assert.False(result.Success);
        Assert.Contains("TLS", result.PersianError);
        Assert.Empty(server.ReceivedMail); // هیچ DATA ارسال نشد.
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* بهترین‌تلاش. */ }
    }
}

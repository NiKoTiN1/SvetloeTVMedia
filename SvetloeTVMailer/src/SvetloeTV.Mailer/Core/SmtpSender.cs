using System.Security.Authentication;
using System.Text.RegularExpressions;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SvetloeTV.Mailer.Core;

public sealed class SmtpSender
{
    private readonly AppSettings _s;
    public SmtpSender(AppSettings s) => _s=s;

    public async Task<SendOutcome> SendAsync(string to,string subject,string body,string? attachmentPath,CancellationToken ct)
    {
        try
        {
            var msg=new MimeMessage();
            msg.From.Add(new MailboxAddress(string.IsNullOrWhiteSpace(_s.FromName)?_s.FromEmail:_s.FromName,_s.FromEmail));
            msg.To.Add(MailboxAddress.Parse(to));
            msg.Subject=subject;
            var builder=new BodyBuilder{TextBody=body};
            if(!string.IsNullOrWhiteSpace(attachmentPath)&&File.Exists(attachmentPath))builder.Attachments.Add(attachmentPath);
            msg.Body=builder.ToMessageBody();

            using var client=new SmtpClient();
            client.Timeout=60000;
            var secure=_s.SmtpSsl?(_s.SmtpPort==465?SecureSocketOptions.SslOnConnect:SecureSocketOptions.StartTls):SecureSocketOptions.None;
            await client.ConnectAsync(_s.SmtpHost,_s.SmtpPort,secure,ct);
            await client.AuthenticateAsync(_s.SmtpUser,SettingsStore.GetPassword(_s),ct);
            await client.SendAsync(msg,ct);
            await client.DisconnectAsync(true,ct);
            return new(SendOutcomeKind.Sent,"OK");
        }
        catch(AuthenticationException ex){return new(SendOutcomeKind.AuthFailure,ex.Message,535);}
        catch(MailKit.CommandException ex){return new(SendOutcomeKind.ConfigFailure,ex.Message);}
        catch(SmtpCommandException ex){return Classify(ex.Message,(int)ex.StatusCode);}
        catch(SmtpProtocolException ex){return new(SendOutcomeKind.TemporaryFailure,ex.Message);}
        catch(InvalidOperationException ex){return new(SendOutcomeKind.ConfigFailure,ex.Message);}
        catch(Exception ex){return new(SendOutcomeKind.TemporaryFailure,ex.Message);}
    }

    private static SendOutcome Classify(string text,int code)
    {
        var low=(text??"SMTP error").ToLowerInvariant();
        if(code is 530 or 534 or 535 || low.Contains("auth") || low.Contains("authentication"))return new(SendOutcomeKind.AuthFailure,text,code);
        if(code>=400&&code<500)return new(SendOutcomeKind.TemporaryFailure,text,code);
        var hard=code>=500&&code<600&&(Regex.IsMatch(low,@"5\.1\.1")||low.Contains("user unknown")||low.Contains("does not exist")||low.Contains("account is disabled")||low.Contains("recipient rejected")||low.Contains("mailbox unavailable")||code is 550 or 551 or 553);
        if(hard)return new(SendOutcomeKind.PermanentFailure,text,code);
        if(code>=500&&code<600)return new(SendOutcomeKind.TemporaryFailure,text,code);
        return new(SendOutcomeKind.TemporaryFailure,text,code);
    }
}

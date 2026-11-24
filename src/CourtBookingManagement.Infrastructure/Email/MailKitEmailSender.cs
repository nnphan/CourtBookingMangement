using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using CourtBookingManagement.Application.Email;
using MailKit;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace CourtBookingManagement.Infrastructure.Email;

public sealed class MailKitEmailSender(
    IOptions<StaffInvitationOptions> options,
    ILogger<MailKitEmailSender> logger) : IEmailSender
{
    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (!options.Value.Smtp.Enabled)
        {
            logger.LogWarning("SMTP email delivery is disabled.");
            return false;
        }

        if (!MailAddress.TryCreate(message.Recipient, out _) ||
            string.IsNullOrWhiteSpace(message.Subject) ||
            (string.IsNullOrEmpty(message.TextBody) && string.IsNullOrEmpty(message.HtmlBody)))
        {
            throw new ArgumentException("The email message is invalid.", nameof(message));
        }

        var smtpOptions = options.Value.Smtp;
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(smtpOptions.FromName, smtpOptions.Sender));
        mimeMessage.To.Add(MailboxAddress.Parse(message.Recipient));
        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new BodyBuilder
        {
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody
        }.ToMessageBody();

        using var client = new SmtpClient
        {
            Timeout = (int)TimeSpan.FromSeconds(smtpOptions.TimeoutSeconds).TotalMilliseconds
        };

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(smtpOptions.TimeoutSeconds));

            await client.ConnectAsync(
                smtpOptions.Host,
                smtpOptions.Port,
                SecureSocketOptions.StartTls,
                timeout.Token);
            await client.AuthenticateAsync(smtpOptions.Username, smtpOptions.Password, timeout.Token);
            await client.SendAsync(mimeMessage, timeout.Token);
            return true;
        }
        catch (ProtocolException)
        {
            logger.LogWarning("SMTP email delivery failed.");
            return false;
        }
        catch (CommandException)
        {
            logger.LogWarning("SMTP email delivery failed.");
            return false;
        }
        catch (ServiceNotConnectedException)
        {
            logger.LogWarning("SMTP email delivery failed.");
            return false;
        }
        catch (ServiceNotAuthenticatedException)
        {
            logger.LogWarning("SMTP email delivery failed.");
            return false;
        }
        catch (IOException)
        {
            logger.LogWarning("SMTP email delivery failed.");
            return false;
        }
        catch (SocketException)
        {
            logger.LogWarning("SMTP email delivery failed.");
            return false;
        }
        catch (AuthenticationException)
        {
            logger.LogWarning("SMTP email delivery failed.");
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("SMTP email delivery timed out.");
            return false;
        }
        finally
        {
            if (client.IsConnected)
            {
                using var disconnectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try
                {
                    await client.DisconnectAsync(true, disconnectTimeout.Token);
                }
                catch (ProtocolException)
                {
                    logger.LogWarning("SMTP connection could not be closed cleanly.");
                }
                catch (CommandException)
                {
                    logger.LogWarning("SMTP connection could not be closed cleanly.");
                }
                catch (IOException)
                {
                    logger.LogWarning("SMTP connection could not be closed cleanly.");
                }
                catch (SocketException)
                {
                    logger.LogWarning("SMTP connection could not be closed cleanly.");
                }
                catch (OperationCanceledException)
                {
                    logger.LogWarning("SMTP connection cleanup timed out.");
                }
            }
        }
    }
}

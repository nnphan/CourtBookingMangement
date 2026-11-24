namespace CourtBookingManagement.Application.Email;

public interface IEmailSender
{
    Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

namespace CourtBookingManagement.Application.Email;

public sealed record EmailMessage(
    string Recipient,
    string Subject,
    string TextBody,
    string HtmlBody);

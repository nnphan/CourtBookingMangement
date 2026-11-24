using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CourtBookingManagement.Infrastructure.Email;

public sealed class StaffInvitationOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<StaffInvitationOptions>
{
    public ValidateOptionsResult Validate(string? name, StaffInvitationOptions options) =>
        options.IsValidConfiguration(environment)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Staff invitation SMTP settings are invalid.");
}

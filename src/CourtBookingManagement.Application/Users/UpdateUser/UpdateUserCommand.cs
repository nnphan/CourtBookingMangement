using CourtBookingManagement.Application.Abstractions.Clock;
using CourtBookingManagement.Application.Abstractions.Messaging;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Domain.Users;
using FluentValidation;

namespace CourtBookingManagement.Application.Users.UpdateUser;

public sealed record UpdateUserCommand(
    Guid Id,
    string Email,
    string? PhoneNumber = null,
    Guid? UpdatedBy = null) : ICommand;

internal sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();

        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.PhoneNumber)
            .MaximumLength(20)
            .When(command => command.PhoneNumber is not null);
    }
}

internal sealed class UpdateUserCommandHandler(
    IUserRepository userRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateUserCommand>
{
    public async Task<Result> Handle(
        UpdateUserCommand request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(request.Id));
        }

        var updateResult = user.UpdateContactInformation(
            request.Email,
            request.PhoneNumber,
            dateTimeProvider.UtcNow,
            request.UpdatedBy);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
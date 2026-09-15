using CourtBookingManagement.Application.Abstractions.Clock;
using CourtBookingManagement.Application.Abstractions.Messaging;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Domain.Users;
using FluentValidation;

namespace CourtBookingManagement.Application.Users.CreateUser;

public sealed record CreateUserCommand(
    string Email,
    string PasswordHash,
    string? PhoneNumber = null,
    Guid? CreatedBy = null) : ICommand<Guid>;

internal sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.PasswordHash)
            .NotEmpty();

        RuleFor(command => command.PhoneNumber)
            .MaximumLength(20)
            .When(command => command.PhoneNumber is not null);
    }
}

internal sealed class CreateUserCommandHandler(
    IUserRepository userRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        var userResult = User.Create(
            request.Email,
            request.PasswordHash,
            dateTimeProvider.UtcNow,
            request.PhoneNumber,
            request.CreatedBy);

        if (userResult.IsFailure)
        {
            return Result.Failure<Guid>(userResult.Error);
        }

        await userRepository.AddAsync(userResult.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return userResult.Value.Id;
    }
}
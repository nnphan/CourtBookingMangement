using CourtBookingManagement.Application.Abstractions.Clock;
using CourtBookingManagement.Application.Abstractions.Messaging;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Domain.Users;
using FluentValidation;

namespace CourtBookingManagement.Application.Users.DeleteUser;

public sealed record DeleteUserCommand(
    Guid Id,
    Guid? DeletedBy = null) : ICommand;

internal sealed class DeleteUserCommandValidator : AbstractValidator<DeleteUserCommand>
{
    public DeleteUserCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();
    }
}

internal sealed class DeleteUserCommandHandler(
    IUserRepository userRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteUserCommand>
{
    public async Task<Result> Handle(
        DeleteUserCommand request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(request.Id));
        }

        user.Delete(dateTimeProvider.UtcNow, request.DeletedBy);
        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
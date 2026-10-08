using CourtBookingManagement.Application.Abstractions.Messaging;
using CourtBookingManagement.Application.Branches.DTOs.Requests;
using CourtBookingManagement.Application.Branches.DTOs.Responses;
using CourtBookingManagement.Application.Branches.Validators;
using FluentValidation;

namespace CourtBookingManagement.Application.Branches.UpdateBranch;

public sealed record UpdateBranchCommand(
    Guid Id,
    CreateBranchRequest Request,
    Guid UpdatedByUserId) : ICommand<CreateBranchResponse>;

public sealed class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.UpdatedByUserId).NotEmpty();
        RuleFor(command => command.Request)
            .NotNull()
            .SetValidator(new CreateBranchRequestValidator());
    }
}
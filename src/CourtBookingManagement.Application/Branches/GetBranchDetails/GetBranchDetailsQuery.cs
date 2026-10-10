using CourtBookingManagement.Application.Abstractions.Messaging;
using CourtBookingManagement.Application.Branches.DTOs.Admin;
using CourtBookingManagement.Application.Branches.Interfaces;
using CourtBookingManagement.Domain.Abstractions;
using FluentValidation;

namespace CourtBookingManagement.Application.Branches.GetBranchDetails;

public sealed record GetBranchDetailsQuery(Guid Id) : IQuery<BranchAdminResponse>;

public sealed class GetBranchDetailsQueryValidator : AbstractValidator<GetBranchDetailsQuery>
{
    public GetBranchDetailsQueryValidator()
    {
        RuleFor(query => query.Id).NotEmpty();
    }
}

public sealed class GetBranchDetailsQueryHandler(IBranchQueryRepository repository)
    : IQueryHandler<GetBranchDetailsQuery, BranchAdminResponse>
{
    public async Task<Result<BranchAdminResponse>> Handle(
        GetBranchDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var response = await repository.GetBranchDetailsAsync(request.Id, cancellationToken);
        return response is null
            ? Result.Failure<BranchAdminResponse>(BranchErrors.NotFound(request.Id))
            : Result.Success(response);
    }
}

public static class BranchErrors
{
    public static Error NotFound(Guid id) => new("Branch.NotFound", "Branch not found");
}
using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Sales.Models.Requests;
using CourtBookingManagement.Application.Sales.Repositories;
using CourtBookingManagement.Infrastructure.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/business-statistics")]
[Authorize(Roles = "BRANCH_OWNER,BRANCH_MANAGER")]
[Permission("sales.report.view")]
public sealed class BusinessStatisticsController(
    ICurrentUserService currentUser,
    IBusinessStatisticsRepository repository,
    IValidator<SalesReportRequest> validator) : ApiControllerBase
{
    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Unauthorized();

        return Success(await GetRoleBranchesAsync(userId, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] SalesReportRequest request,
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Unauthorized();

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var branches = await GetRoleBranchesAsync(userId, cancellationToken);
        var allowedBranches = branchId.HasValue
            ? branches.Where(branch => branch.Id == branchId)
            : branches.Where(branch => branch.IsOwner);
        var branchIds = allowedBranches.Select(branch => branch.Id).ToArray();
        if (branchIds.Length == 0)
            return Forbid();

        return Success(await repository.GetStatisticsAsync(branchIds, request.From, request.To, cancellationToken));
    }

    private async Task<IReadOnlyList<CourtBookingManagement.Application.Sales.Models.Responses.BusinessStatisticsBranch>>
        GetRoleBranchesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var isOwner = currentUser.Roles.Contains("BRANCH_OWNER", StringComparer.OrdinalIgnoreCase);
        var branches = await repository.GetBranchesAsync(userId, cancellationToken);
        return branches.Where(branch => branch.IsOwner == isOwner).ToArray();
    }
}

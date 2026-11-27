using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Sales.Models.Requests;
using CourtBookingManagement.Application.Sales.Models.Responses;
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
        var branchIds = await GetReportBranchIdsAsync(userId, branchId, cancellationToken);
        if (branchIds.Length == 0)
            return Forbid();

        return Success(await repository.GetStatisticsAsync(branchIds, request.From, request.To, cancellationToken));
    }

    [HttpGet("booking-details")]
    public async Task<IActionResult> GetBookingDetails(
        [FromQuery] DateOnly date, [FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Unauthorized();
        if (date == default)
            return BadRequest();
        var branches = await GetReportBranchIdsAsync(userId, branchId, cancellationToken);
        return branches.Length == 0 ? Forbid() :
            Success(await repository.GetBookingDetailsAsync(branches, date, cancellationToken));
    }

    [HttpGet("sales-details")]
    public async Task<IActionResult> GetSalesDetails(
        [FromQuery] DateOnly date, [FromQuery] Guid productId, [FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Unauthorized();
        if (date == default || productId == Guid.Empty)
            return BadRequest();
        var branches = await GetReportBranchIdsAsync(userId, branchId, cancellationToken);
        return branches.Length == 0 ? Forbid() :
            Success(await repository.GetSalesDetailsAsync(branches, date, productId, cancellationToken));
    }

    [HttpPost("expenses")]
    public Task<IActionResult> CreateExpense(BusinessExpenseRequest request, CancellationToken cancellationToken) =>
        SaveExpense(null, request, cancellationToken);

    [HttpPut("expenses/{id:guid}")]
    public Task<IActionResult> EditExpense(Guid id, BusinessExpenseRequest request, CancellationToken cancellationToken) =>
        SaveExpense(id, request, cancellationToken);

    [HttpDelete("expenses/{id:guid}")]
    public async Task<IActionResult> DeleteExpense(Guid id, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Unauthorized();
        var branches = await GetRoleBranchesAsync(userId, cancellationToken);
        if (branches.Count == 0)
            return Forbid();
        return await repository.DeleteExpenseAsync(id, branches.Select(branch => branch.Id).ToArray(), cancellationToken)
            ? NoContent() : NotFound();
    }

    private async Task<IActionResult> SaveExpense(Guid? id, BusinessExpenseRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Unauthorized();
        if (request.Date == default || request.Date > new DateOnly(9998, 12, 31) || decimal.Round(request.Amount, 2) != request.Amount)
            return BadRequest();
        var branches = await GetRoleBranchesAsync(userId, cancellationToken);
        if (!branches.Any(branch => branch.Id == request.BranchId))
            return Forbid();
        var saved = await repository.SaveExpenseAsync(
            id, branches.Select(branch => branch.Id).ToArray(), userId, request, cancellationToken);
        return saved.HasValue ? Success(saved.Value) : NotFound();
    }

    private async Task<Guid[]> GetReportBranchIdsAsync(Guid userId, Guid? branchId, CancellationToken cancellationToken)
    {
        var branches = await GetRoleBranchesAsync(userId, cancellationToken);
        return branches.Where(branch => branchId.HasValue ? branch.Id == branchId : branch.IsOwner)
            .Select(branch => branch.Id).ToArray();
    }

    private async Task<IReadOnlyList<BusinessStatisticsBranch>>
        GetRoleBranchesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var isOwner = currentUser.Roles.Contains("BRANCH_OWNER", StringComparer.OrdinalIgnoreCase);
        var branches = await repository.GetBranchesAsync(userId, cancellationToken);
        return branches.Where(branch => branch.IsOwner == isOwner).ToArray();
    }
}

using System.Security.Cryptography;
using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Api.Common.Constants;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.CourtStatus.DTOs;
using CourtBookingManagement.Application.CourtStatus.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/court-status")]
public sealed class CourtStatusController(ICourtStatusService service, ICurrentUserService currentUser,
    IBranchAuthorizationService branchAuthorization) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBoard([FromQuery] GetCourtStatusRequest request,
        [FromQuery] bool includeCustomerData, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (includeCustomerData)
        {
            if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid user) return Unauthorized();
            await branchAuthorization.EnsureCanAccessAsync(user, request.BranchId, Permissions.BookingView, ct);
        }

        var board = await service.GetBoardAsync(request.BranchId, request.Date, ct);
        if (includeCustomerData || !board.IsSuccess) return FromResult(board);

        return Success(board.Value with
        {
            ScheduleItems = board.Value.ScheduleItems.Select(item => item with
            {
                BookingId = new Guid(SHA256.HashData(item.BookingId.ToByteArray()).AsSpan(0, 16)),
                CustomerName = null,
                PhoneNumber = null,
                PaymentStatus = null,
                Title = "Đã đặt"
            }).ToList()
        });
    }
}

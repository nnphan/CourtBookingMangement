using CourtBookingManagement.Application.Auth.DTOs;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Api.Common.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, ICurrentUserService currentUserService) : ApiControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RefreshTokenAsync(request, cancellationToken);
        return FromResult(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LogoutAsync(request.RefreshToken, currentUserService.UserId, cancellationToken);
        return FromResult(result);
    }

    [Authorize]
    [HttpPost("logout-all-devices")]
    public async Task<IActionResult> LogoutAllDevices(CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;
        if (userId == Guid.Empty)
        {
            return Error(new Domain.Abstractions.Error("Auth.Unauthorized", "Authentication is required."));
        }

        var result = await authService.LogoutAllDevicesAsync(userId, cancellationToken);
        return FromResult(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
        {
            return Error(new Domain.Abstractions.Error("Auth.Unauthorized", "Authentication is required."));
        }

        var result = await authService.GetCurrentUserAsync(userId.Value, cancellationToken);
        return FromResult(result);
    }
}

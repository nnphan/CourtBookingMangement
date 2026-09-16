using CourtBookingManagement.Application.Auth.DTOs;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Domain.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, ICurrentUserService currentUserService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error);
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RefreshTokenAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LogoutAsync(request.RefreshToken, currentUserService.UserId, cancellationToken);
        return result.IsSuccess ? Ok() : ToErrorResponse(result.Error);
    }

    [Authorize]
    [HttpPost("logout-all-devices")]
    public async Task<IActionResult> LogoutAllDevices(CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await authService.LogoutAllDevicesAsync(userId, cancellationToken);
        return result.IsSuccess ? Ok() : ToErrorResponse(result.Error);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await authService.GetCurrentUserAsync(userId.Value, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error);
    }

    private ObjectResult ToErrorResponse(Error error)
    {
        var statusCode = error.Code switch
        {
            "Auth.InvalidRequest" => StatusCodes.Status400BadRequest,
            "Auth.InvalidCredentials" => StatusCodes.Status401Unauthorized,
            "Auth.UserDisabled" => StatusCodes.Status403Forbidden,
            "Auth.EmailNotVerified" => StatusCodes.Status403Forbidden,
            "Auth.RefreshTokenInvalid" => StatusCodes.Status401Unauthorized,
            "Auth.RefreshTokenRevoked" => StatusCodes.Status401Unauthorized,
            "Auth.RefreshTokenExpired" => StatusCodes.Status401Unauthorized,
            "Auth.Unauthorized" => StatusCodes.Status403Forbidden,
            "User.NotFound" => StatusCodes.Status404NotFound,
            "User.EmailAlreadyExists" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return Problem(statusCode: statusCode, title: error.Code, detail: error.Description);
    }
}

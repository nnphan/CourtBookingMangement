using System.Security.Claims;
using CourtBookingManagement.Application.Auth.Interfaces;
using Microsoft.AspNetCore.Http;

namespace CourtBookingManagement.Infrastructure.Auth;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public Guid? UserId
    {
        get
        {
            var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("sub");

            return Guid.TryParse(userId, out var parsed) ? parsed : null;
        }
    }

    public string? Email => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email)
        ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("email");

    public IReadOnlyCollection<string> Roles => _httpContextAccessor.HttpContext?.User.Claims
        .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
        .Select(c => c.Value)
        .Distinct()
        .ToArray() ?? Array.Empty<string>();

    public IReadOnlyCollection<string> Permissions => _httpContextAccessor.HttpContext?.User.Claims
        .Where(c => c.Type == "permission")
        .Select(c => c.Value)
        .Distinct()
        .ToArray() ?? Array.Empty<string>();

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
}

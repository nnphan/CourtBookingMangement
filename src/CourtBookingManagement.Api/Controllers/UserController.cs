using CourtBookingManagement.Application.Users.DTOs;
using CourtBookingManagement.Application.Users.Services;
using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UserController(IUserService userService) : ApiControllerBase
{
    [HttpPost]
    [Permission("user.create")]
    public async Task<IActionResult> Create(
        CreateUserRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await userService.CreateUserAsync(request, cancellationToken);
        return result.IsSuccess
            ? Created(result.Value, nameof(GetById), new { id = result.Value.Id })
            : Error(result.Error);
    }

    [HttpGet]
    [Permission("user.view")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await userService.GetAllUsersAsync(cancellationToken);
        return FromResult(result);
    }

    [HttpGet("{id:guid}")]
    [Permission("user.view")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await userService.GetUserByIdAsync(id, cancellationToken);
        return FromResult(result);
    }

    [HttpPut("{id:guid}")]
    [Permission("user.update")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateUserRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await userService.UpdateUserAsync(id, request, cancellationToken);
        return FromResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Permission("user.delete")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await userService.DeleteUserAsync(id, cancellationToken: cancellationToken);
        return FromResult(result);
    }
}
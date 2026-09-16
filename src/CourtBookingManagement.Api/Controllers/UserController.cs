using CourtBookingManagement.Application.Users.DTOs;
using CourtBookingManagement.Application.Users.Services;
using CourtBookingManagement.Domain.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UserController(IUserService userService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateUserRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await userService.CreateUserAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : ToErrorResponse(result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await userService.GetAllUsersAsync(cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ToErrorResponse(result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await userService.GetUserByIdAsync(id, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ToErrorResponse(result.Error);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateUserRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await userService.UpdateUserAsync(id, request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ToErrorResponse(result.Error);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await userService.DeleteUserAsync(id, cancellationToken: cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : ToErrorResponse(result.Error);
    }

    private ObjectResult ToErrorResponse(Error error)
    {
        var statusCode = error.Code switch
        {
            "User.NotFound" => StatusCodes.Status404NotFound,
            "User.EmailAlreadyExists" => StatusCodes.Status409Conflict,
            "Error.Validation" => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };

        return Problem(
            statusCode: statusCode,
            title: error.Code,
            detail: error.Description);
    }
}
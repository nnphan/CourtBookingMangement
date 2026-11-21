using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Api.Files;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Infrastructure.Auth;
using CourtBookingManagement.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

/// <summary>Uploads images to the configured file storage provider.</summary>
[ApiController]
[Route("api/files")]
public sealed class FilesController(
    IFileStorageService fileStorageService,
    ICurrentUserService currentUserService) : ApiControllerBase
{
    /// <summary>Uploads a JPEG, PNG, or WEBP image to the selected media category.</summary>
    /// <remarks>Example multipart fields: file=arena.jpg, category=branch.</remarks>
    [HttpPost("upload")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<UploadFileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Upload(
        [FromForm] UploadFileRequest request,
        CancellationToken ct)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("Auth.Unauthorized", "Authentication is required."));
        }

        var result = await fileStorageService.UploadAsync(
            request.File,
            request.Category,
            userId,
            ct);
        return Success(result, "File uploaded successfully");
    }
}
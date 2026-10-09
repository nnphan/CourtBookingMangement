using CourtBookingManagement.Api.Common.Constants;
using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Api.Files;
using CourtBookingManagement.Infrastructure.Auth;
using CourtBookingManagement.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

/// <summary>Uploads images to the configured file storage provider.</summary>
[ApiController]
[Route("api/files")]
public sealed class FilesController(IFileStorageService fileStorageService) : ApiControllerBase
{
    /// <summary>Uploads a JPEG, PNG, or WEBP image to the selected media category.</summary>
    /// <remarks>Example multipart fields: file=arena.jpg, category=branch.</remarks>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    //[Permission(Permissions.FileUpload)]
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
        var result = await fileStorageService.UploadAsync(request.File, request.Category, ct);
        return Success(result, "File uploaded successfully");
    }
}
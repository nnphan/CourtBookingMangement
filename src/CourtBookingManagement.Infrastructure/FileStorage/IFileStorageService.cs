using Microsoft.AspNetCore.Http;

namespace CourtBookingManagement.Infrastructure.FileStorage;

public interface IFileStorageService
{
    Task<UploadFileResponse> UploadAsync(
        IFormFile file,
        string category,
        Guid uploaderUserId,
        CancellationToken cancellationToken);
}
using Microsoft.AspNetCore.Http;

namespace CourtBookingManagement.Infrastructure.FileStorage;

public interface IFileStorageService
{
    Task<UploadFileResponse> UploadAsync(
        IFormFile file,
        string category,
        CancellationToken cancellationToken);

    Task<UploadFileResponse> UploadAsync(
        IFormFile file,
        CancellationToken cancellationToken);
}
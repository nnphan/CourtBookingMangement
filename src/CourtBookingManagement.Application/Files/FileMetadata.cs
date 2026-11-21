using System.Data;

namespace CourtBookingManagement.Application.Files;

public sealed record CreateFileMetadataRequest(
    Guid UploaderUserId,
    string Category,
    string CloudinaryPublicId,
    string SecureUrl,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Status);

public sealed record FileMetadataRecord(
    Guid Id,
    Guid UploaderUserId,
    string Category,
    string SecureUrl,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Status,
    bool IsDeleted);

public sealed record ValidatedAvatarFile(
    Guid FileId,
    string Url,
    string FileName,
    string ContentType,
    long SizeBytes);

public interface IFileMetadataRepository
{
    Task<Guid> CreateAsync(
        CreateFileMetadataRequest request,
        CancellationToken cancellationToken);

    Task<FileMetadataRecord?> GetByIdAsync(
        Guid fileId,
        IDbTransaction? transaction,
        CancellationToken cancellationToken);
}

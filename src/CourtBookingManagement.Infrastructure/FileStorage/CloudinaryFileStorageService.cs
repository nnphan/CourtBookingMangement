 using System.Diagnostics;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using CourtBookingManagement.Application.Files;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CourtBookingManagement.Infrastructure.FileStorage;

public static class FileStorageFolders
{
    public const string Branches = "badminton-booking/branches";
}

public interface ICloudinaryFileUploadClient
{
    Task<CloudinaryUploadResult> UploadAsync(
        IFormFile file,
        string generatedFileName,
        string folder,
        CancellationToken cancellationToken);
}

public sealed record CloudinaryUploadResult(string? PublicId, Uri? SecureUrl, string? Error);

public sealed class CloudinaryFileUploadClient(Cloudinary cloudinary) : ICloudinaryFileUploadClient
{
    public async Task<CloudinaryUploadResult> UploadAsync(
        IFormFile file,
        string generatedFileName,
        string folder,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var uploadParameters = new ImageUploadParams
        {
            File = new FileDescription(generatedFileName, stream),
            Folder = folder,
            PublicId = Path.GetFileNameWithoutExtension(generatedFileName)
        };

        var result = await cloudinary.UploadAsync(uploadParameters, cancellationToken);
        return new CloudinaryUploadResult(result.PublicId, result.SecureUrl, result.Error?.Message);
    }
}

public sealed class CloudinaryFileStorageService(
    ICloudinaryFileUploadClient cloudinaryClient,
    IMediaFolderResolver mediaFolderResolver,
    IFileMetadataRepository fileMetadataRepository,
    ILogger<CloudinaryFileStorageService> logger) : IFileStorageService
{
    private const long MaximumFileSize = 5 * 1024 * 1024;

    public async Task<UploadFileResponse> UploadAsync(
        IFormFile file,
        string category,
        Guid uploaderUserId,
        CancellationToken cancellationToken)
    {
        var categoryValue = category ?? string.Empty;
        var normalizedCategory = categoryValue.Trim().ToLowerInvariant();
        if (uploaderUserId == Guid.Empty)
        {
            throw ValidationError("An authenticated uploader is required");
        }

        try
        {
            var folder = mediaFolderResolver.Resolve(categoryValue, uploaderUserId);
            return await UploadToFolderAsync(
                file,
                normalizedCategory,
                folder,
                uploaderUserId,
                cancellationToken);
        }
        catch (ValidationException exception)
        {
            logger.LogWarning(
                exception,
                "File upload validation failed. Category: {Category}, Message: {Message}",
                normalizedCategory,
                exception.Message);
            throw;
        }
    }

    private async Task<UploadFileResponse> UploadToFolderAsync(
        IFormFile file,
        string category,
        string folder,
        Guid uploaderUserId,
        CancellationToken cancellationToken)
    {
        var contentType = await ValidateFileAsync(file, cancellationToken);

        var extension = GetExtension(contentType);
        var generatedFileName = $"{Guid.NewGuid()}{extension}";
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation(
            "Cloudinary upload started. UploaderUserId: {UploaderUserId}, ContentType: {ContentType}, Size: {Size}, Category: {Category}",
            uploaderUserId,
            contentType,
            file.Length,
            category);

        CloudinaryUploadResult uploadResult;
        try
        {
            uploadResult = await cloudinaryClient.UploadAsync(
                file,
                generatedFileName,
                folder.Trim('/'),
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(uploadResult.Error)
                || uploadResult.SecureUrl is null
                || !uploadResult.SecureUrl.IsAbsoluteUri
                || uploadResult.SecureUrl.Scheme != Uri.UriSchemeHttps
                || string.IsNullOrWhiteSpace(uploadResult.PublicId))
            {
                throw new InvalidOperationException(
                    uploadResult.Error ?? "Cloudinary did not return valid secure file metadata.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Cloudinary upload failed. Category: {Category}, Message: {Message}",
                category,
                exception.Message);
            throw new FileStorageException("Failed to upload file", exception);
        }

        var secureUrl = uploadResult.SecureUrl!.ToString();
        Guid fileId;
        try
        {
            fileId = await fileMetadataRepository.CreateAsync(
                new CreateFileMetadataRequest(
                    uploaderUserId,
                    category,
                    uploadResult.PublicId!,
                    secureUrl,
                    generatedFileName,
                    contentType,
                    file.Length,
                    string.Equals(category, "avatar", StringComparison.Ordinal)
                        ? "TEMPORARY"
                        : "ATTACHED"),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "File metadata persistence was cancelled after Cloudinary upload. UploaderUserId: {UploaderUserId}, Category: {Category}, PublicId: {PublicId}",
                uploaderUserId,
                category,
                uploadResult.PublicId);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "File metadata persistence failed after Cloudinary upload. UploaderUserId: {UploaderUserId}, Category: {Category}, PublicId: {PublicId}",
                uploaderUserId,
                category,
                uploadResult.PublicId);
            throw;
        }

        stopwatch.Stop();
        logger.LogInformation(
            "Cloudinary upload and metadata persistence succeeded. FileId: {FileId}, UploaderUserId: {UploaderUserId}, Category: {Category}, ElapsedMilliseconds: {ElapsedMilliseconds}",
            fileId,
            uploaderUserId,
            category,
            stopwatch.ElapsedMilliseconds);

        return new UploadFileResponse
        {
            FileId = fileId,
            FileName = generatedFileName,
            Url = secureUrl,
            ContentType = contentType,
            Size = file.Length,
            Category = category
        };
    }

    private static async Task<string> ValidateFileAsync(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw ValidationError("File is required");
        }

        if (file.Length > MaximumFileSize)
        {
            throw ValidationError("Maximum file size is 5 MB");
        }

        var contentType = file.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
        var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
        var expectedHeaderLength = contentType switch
        {
            "image/jpeg" or "image/jpg" => 3,
            "image/png" => 8,
            "image/webp" => 12,
            _ => 0
        };

        var extensionMatches = contentType switch
        {
            "image/jpeg" or "image/jpg" => extension is ".jpg" or ".jpeg",
            "image/png" => extension == ".png",
            "image/webp" => extension == ".webp",
            _ => false
        };

        if (expectedHeaderLength == 0 || !extensionMatches)
        {
            throw ValidationError("Only JPG, JPEG, PNG, WEBP are allowed");
        }

        var header = new byte[expectedHeaderLength];
        await using var stream = file.OpenReadStream();
        var bytesRead = 0;
        while (bytesRead < header.Length)
        {
            var read = await stream.ReadAsync(
                header.AsMemory(bytesRead, header.Length - bytesRead),
                cancellationToken);
            if (read == 0)
            {
                break;
            }

            bytesRead += read;
        }

        if (bytesRead != header.Length || !HasExpectedSignature(header, contentType))
        {
            throw ValidationError("The file content does not match a supported image format");
        }

        return contentType;
    }

    private static bool HasExpectedSignature(byte[] header, string contentType) =>
        contentType switch
        {
            "image/jpeg" or "image/jpg" =>
                header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            "image/png" =>
                header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A,
            "image/webp" =>
                header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,
            _ => false
        };

    private static string GetExtension(string contentType) =>
        contentType.Trim().ToLowerInvariant() switch
    {
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => throw ValidationError("Only JPG, JPEG, PNG, WEBP are allowed")
    };

    private static ValidationException ValidationError(string message) =>
        new([new ValidationFailure("file", message)]);
}

public sealed class FileStorageException(string message, Exception innerException)
    : Exception(message, innerException);
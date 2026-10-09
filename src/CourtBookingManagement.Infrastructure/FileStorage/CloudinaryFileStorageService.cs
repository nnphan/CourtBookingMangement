 using System.Diagnostics;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
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
    ILogger<CloudinaryFileStorageService> logger) : IFileStorageService
{
    private const long MaximumFileSize = 5 * 1024 * 1024;

    public CloudinaryFileStorageService(
        ICloudinaryFileUploadClient cloudinaryClient,
        ILogger<CloudinaryFileStorageService> logger)
        : this(cloudinaryClient, new MediaFolderResolver(), logger)
    {
    }

    public Task<UploadFileResponse> UploadAsync(
        IFormFile file,
        CancellationToken cancellationToken) =>
        UploadAsync(file, "branch", cancellationToken);

    public Task<UploadFileResponse> UploadAsync(
        IFormFile file,
        string category,
        CancellationToken cancellationToken)
    {
        var categoryValue = category ?? string.Empty;
        var normalizedCategory = categoryValue.Trim().ToLowerInvariant();
        try
        {
            var folder = mediaFolderResolver.Resolve(categoryValue);
            return UploadToFolderAsync(file, normalizedCategory, folder, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        ValidateFile(file);

        var extension = GetExtension(file.ContentType);
        var generatedFileName = $"{Guid.NewGuid()}{extension}";
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation(
            "Cloudinary upload started. FileName: {FileName}, ContentType: {ContentType}, Size: {Size}, Category: {Category}",
            file.FileName,
            file.ContentType,
            file.Length,
            category);

        try
        {
            var result = await cloudinaryClient.UploadAsync(
                file,
                generatedFileName,
                folder.Trim('/'),
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(result.Error) || result.SecureUrl is null)
            {
                throw new InvalidOperationException(result.Error ?? "Cloudinary did not return a secure URL.");
            }

            stopwatch.Stop();
            logger.LogInformation(
                "Cloudinary upload succeeded. Folder: {Folder}, Url: {Url}, ElapsedMilliseconds: {ElapsedMilliseconds}",
                folder,
                result.SecureUrl,
                stopwatch.ElapsedMilliseconds);

            return new UploadFileResponse
            {
                FileName = generatedFileName,
                Url = result.SecureUrl.ToString(),
                ContentType = file.ContentType,
                Size = file.Length,
                Category = category
            };
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
    }

    private static void ValidateFile(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            throw ValidationError("File is required");
        }

        if (file.Length > MaximumFileSize)
        {
            throw ValidationError("Maximum file size is 5 MB");
        }

        var contentType = file.ContentType.Trim().ToLowerInvariant();
        var isAllowedImage = contentType switch
        {
            "image/jpeg" or "image/jpg" or "image/png" or "image/webp" => true,
            _ => false
        };

        if (!isAllowedImage)
        {
            throw ValidationError("Only JPG, JPEG, PNG, WEBP are allowed");
        }
    }

    private static string GetExtension(string contentType) => contentType.Trim().ToLowerInvariant() switch
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
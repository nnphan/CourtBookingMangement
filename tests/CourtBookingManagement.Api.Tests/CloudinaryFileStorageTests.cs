using CourtBookingManagement.Infrastructure.FileStorage;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class CloudinaryFileStorageTests
{
    [Theory]
    [InlineData("branch.jpg", "image/jpeg")]
    [InlineData("branch.png", "image/png")]
    [InlineData("branch.webp", "image/webp")]
    public async Task Upload_returns_cloudinary_url_for_supported_image(string fileName, string contentType)
    {
        var uploader = new StubCloudinaryFileUploadClient
        {
            Result = new CloudinaryUploadResult("badminton-booking/branches/image", new Uri("https://res.cloudinary.com/demo/image.jpg"), null)
        };
        var service = CreateService(uploader);

        var response = await service.UploadAsync(CreateFile(fileName, contentType, [1, 2, 3]), CancellationToken.None);

        Assert.Equal("https://res.cloudinary.com/demo/image.jpg", response.Url);
        Assert.Equal(contentType, response.ContentType);
        Assert.Equal(3, response.Size);
        Assert.EndsWith(Path.GetExtension(fileName), response.FileName);
        Assert.Equal(FileStorageFolders.Branches, uploader.Folder);
    }

    [Fact]
    public async Task Upload_rejects_empty_file()
    {
        var service = CreateService(new StubCloudinaryFileUploadClient());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UploadAsync(CreateFile("empty.jpg", "image/jpeg", []), CancellationToken.None));

        Assert.Contains(exception.Errors, error => error.ErrorMessage == "File is required");
    }

    [Fact]
    public async Task Upload_rejects_invalid_extension()
    {
        var service = CreateService(new StubCloudinaryFileUploadClient());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UploadAsync(CreateFile("branch.gif", "image/gif", [1]), CancellationToken.None));

        Assert.Contains(exception.Errors, error => error.ErrorMessage == "Only JPG, PNG and WEBP are allowed");
    }

    [Fact]
    public async Task Upload_rejects_files_larger_than_five_megabytes()
    {
        var service = CreateService(new StubCloudinaryFileUploadClient());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UploadAsync(CreateFile("large.jpg", "image/jpeg", new byte[5 * 1024 * 1024 + 1]), CancellationToken.None));

        Assert.Contains(exception.Errors, error => error.ErrorMessage == "Maximum allowed size is 5 MB");
    }

    [Fact]
    public async Task Upload_converts_cloudinary_failure_to_file_storage_exception()
    {
        var uploader = new StubCloudinaryFileUploadClient
        {
            Result = new CloudinaryUploadResult(null, null, "Provider unavailable")
        };
        var service = CreateService(uploader);

        var exception = await Assert.ThrowsAsync<FileStorageException>(() =>
            service.UploadAsync(CreateFile("branch.jpg", "image/jpeg", [1]), CancellationToken.None));

        Assert.Equal("Failed to upload image", exception.Message);
    }

    private static CloudinaryFileStorageService CreateService(ICloudinaryFileUploadClient uploader) =>
        new(uploader, NullLogger<CloudinaryFileStorageService>.Instance);

    private static IFormFile CreateFile(string fileName, string contentType, byte[] contents)
    {
        var file = new FormFile(new MemoryStream(contents), 0, contents.Length, "file", fileName)
        {
            Headers = new HeaderDictionary
            {
                ["Content-Type"] = contentType
            }
        };
        return file;
    }

    private sealed class StubCloudinaryFileUploadClient : ICloudinaryFileUploadClient
    {
        public CloudinaryUploadResult Result { get; init; } = new(null, null, null);

        public string? Folder { get; private set; }

        public Task<CloudinaryUploadResult> UploadAsync(
            IFormFile file,
            string generatedFileName,
            string folder,
            CancellationToken cancellationToken)
        {
            Folder = folder;
            return Task.FromResult(Result);
        }
    }
}
using CourtBookingManagement.Application.Files;
using CourtBookingManagement.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class FileUploadValidationTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<ICloudinaryFileUploadClient> _cloudinaryClient = new();
    private readonly Mock<IMediaFolderResolver> _folderResolver = new();
    private readonly Mock<IFileMetadataRepository> _metadataRepository = new();

    [Fact]
    public async Task MismatchedImageSignatureIsRejectedBeforeStorage()
    {
        var file = CreateFile([0x00, 0x00, 0x00, 0x00], "image/jpeg", "avatar.jpg");
        var service = CreateService();

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            service.UploadAsync(file, "avatar", _userId, CancellationToken.None));

        _cloudinaryClient.Verify(
            client => client.UploadAsync(
                It.IsAny<IFormFile>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UnsupportedExtensionIsRejectedBeforeStorage()
    {
        var file = CreateFile([0xFF, 0xD8, 0xFF, 0x00], "image/jpeg", "avatar.svg");
        var service = CreateService();

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            service.UploadAsync(file, "avatar", _userId, CancellationToken.None));

        _cloudinaryClient.Verify(
            client => client.UploadAsync(
                It.IsAny<IFormFile>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidAvatarPersistsUploaderMetadataAndReturnsFileId()
    {
        var fileId = Guid.NewGuid();
        var file = CreateFile([0xFF, 0xD8, 0xFF, 0x00], "image/jpeg", "avatar.jpg");
        _folderResolver.Setup(resolver => resolver.Resolve("avatar", _userId))
            .Returns($"badminton-booking/avatars/{_userId:N}");
        _cloudinaryClient.Setup(client => client.UploadAsync(
                file,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CloudinaryUploadResult(
                $"badminton-booking/avatars/{_userId:N}/generated",
                new Uri("https://res.cloudinary.com/example/avatar.jpg"),
                null));
        _metadataRepository.Setup(repository => repository.CreateAsync(
                It.IsAny<CreateFileMetadataRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileId);
        var service = CreateService();

        var response = await service.UploadAsync(file, "avatar", _userId, CancellationToken.None);

        Assert.Equal(fileId, response.FileId);
        Assert.Equal("image/jpeg", response.ContentType);
        _metadataRepository.Verify(repository => repository.CreateAsync(
            It.Is<CreateFileMetadataRequest>(request =>
                request.UploaderUserId == _userId
                && request.Category == "avatar"
                && request.Status == "TEMPORARY"
                && request.ContentType == "image/jpeg"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private CloudinaryFileStorageService CreateService() =>
        new(
            _cloudinaryClient.Object,
            _folderResolver.Object,
            _metadataRepository.Object,
            NullLogger<CloudinaryFileStorageService>.Instance);

    private static FormFile CreateFile(byte[] bytes, string contentType, string fileName)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary
            {
                ["Content-Type"] = contentType
            }
        };
    }
}

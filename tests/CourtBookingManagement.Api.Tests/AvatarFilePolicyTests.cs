using System.Data;
using CourtBookingManagement.Application.Files;
using Moq;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class AvatarFilePolicyTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _fileId = Guid.NewGuid();
    private readonly Mock<IFileMetadataRepository> _repository = new();
    private readonly Mock<IDbTransaction> _transaction = new();

    [Fact]
    public async Task ValidTemporaryAvatarUploadedByCurrentUserIsAttachable()
    {
        _repository.Setup(repository => repository.GetByIdAsync(
                _fileId,
                It.IsAny<IDbTransaction?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFile());
        var policy = new AvatarFilePolicy(_repository.Object);

        var result = await policy.ValidateForUserAsync(
            _fileId,
            _userId,
            null,
            _transaction.Object,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(_fileId, result.Value.FileId);
        Assert.Equal("https://res.cloudinary.com/example/avatar.jpg", result.Value.Url);
    }

    [Fact]
    public async Task FileUploadedByAnotherUserIsRejected()
    {
        _repository.Setup(repository => repository.GetByIdAsync(
                _fileId,
                It.IsAny<IDbTransaction?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFile(uploaderUserId: Guid.NewGuid()));
        var policy = new AvatarFilePolicy(_repository.Object);

        var result = await policy.ValidateForUserAsync(
            _fileId,
            _userId,
            null,
            _transaction.Object,
            CancellationToken.None);

        Assert.Equal("AVATAR_FILE_NOT_OWNED", result.Error.Code);
    }

    [Fact]
    public async Task AttachedFileIsEligibleOnlyWhenItIsAlreadyTheUsersCurrentAvatar()
    {
        _repository.Setup(repository => repository.GetByIdAsync(
                _fileId,
                It.IsAny<IDbTransaction?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFile(status: "ATTACHED"));
        var policy = new AvatarFilePolicy(_repository.Object);

        var currentFile = await policy.ValidateForUserAsync(
            _fileId,
            _userId,
            _fileId,
            _transaction.Object,
            CancellationToken.None);
        var unrelatedFile = await policy.ValidateForUserAsync(
            _fileId,
            _userId,
            Guid.NewGuid(),
            _transaction.Object,
            CancellationToken.None);

        Assert.True(currentFile.IsSuccess);
        Assert.Equal("AVATAR_FILE_NOT_ATTACHABLE", unrelatedFile.Error.Code);
    }

    private FileMetadataRecord CreateFile(
        Guid? uploaderUserId = null,
        string category = "avatar",
        string contentType = "image/jpeg",
        long sizeBytes = 1024,
        string status = "TEMPORARY",
        bool isDeleted = false) =>
        new(
            _fileId,
            uploaderUserId ?? _userId,
            category,
            "https://res.cloudinary.com/example/avatar.jpg",
            "avatar.jpg",
            contentType,
            sizeBytes,
            status,
            isDeleted);
}

namespace CourtBookingManagement.Infrastructure.FileStorage;

public sealed class UploadFileResponse
{
    public required string FileName { get; init; }

    public required string Url { get; init; }

    public long Size { get; init; }

    public required string ContentType { get; init; }

    public required string Category { get; init; }
}
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CourtBookingManagement.Api.Files;

/// <summary>Multipart form data for uploading an image to a media category.</summary>
public sealed class UploadFileRequest
{
    /// <summary>JPEG, PNG, or WEBP image up to 5 MB.</summary>
    [Required(ErrorMessage = "File is required")]
    public IFormFile File { get; set; } = default!;

    /// <summary>One of: branch, court, avatar, tournament, promotion.</summary>
    [Required(ErrorMessage = "Unsupported media category")]
    public string Category { get; set; } = default!;
}
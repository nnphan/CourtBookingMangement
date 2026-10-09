namespace CourtBookingManagement.Infrastructure.FileStorage;

public interface IMediaFolderResolver
{
    string Resolve(string category);
}
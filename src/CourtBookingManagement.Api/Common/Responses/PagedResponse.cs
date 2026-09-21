namespace CourtBookingManagement.Api.Common.Responses;

public sealed record PagedResponse<T>(
    IReadOnlyCollection<T> Items,
    int PageNumber,
    int PageSize,
    long TotalRecords,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage)
{
    public static PagedResponse<T> Create(
        IReadOnlyCollection<T> items,
        int pageNumber,
        int pageSize,
        long totalRecords)
    {
        var totalPages = pageSize <= 0
            ? 0
            : (int)Math.Ceiling(totalRecords / (double)pageSize);

        return new(
            items,
            pageNumber,
            pageSize,
            totalRecords,
            totalPages,
            pageNumber < totalPages,
            pageNumber > 1);
    }
}
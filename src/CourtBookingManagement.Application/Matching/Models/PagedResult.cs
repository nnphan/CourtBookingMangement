namespace CourtBookingManagement.Application.Matching.Models;

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public long TotalCount { get; init; }

    public int TotalPages { get; init; }

    public bool HasPreviousPage { get; init; }

    public bool HasNextPage { get; init; }

    public PagedResultMetadata Metadata => new(Page, PageSize, TotalCount, TotalPages, HasPreviousPage, HasNextPage);

    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, long totalCount)
    {
        var totalPages = pageSize > 0 && totalCount > 0
            ? (int)Math.Ceiling(totalCount / (double)pageSize)
            : 0;

        return new PagedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasPreviousPage = page > 1 && totalPages > 0,
            HasNextPage = page < totalPages
        };
    }
}

public sealed record PagedResultMetadata(
    int PageNumber,
    int PageSize,
    long TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);

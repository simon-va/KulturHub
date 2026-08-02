namespace KulturHub.Application.Abstractions.Pagination;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Skip,
    int Take);

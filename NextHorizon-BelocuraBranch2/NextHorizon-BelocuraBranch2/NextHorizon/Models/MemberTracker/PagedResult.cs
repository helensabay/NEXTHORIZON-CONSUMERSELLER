namespace NextHorizon.Models;

public class PagedResult<T>
{
    public int PageNumber { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public IReadOnlyList<T> Items { get; init; } = [];
}
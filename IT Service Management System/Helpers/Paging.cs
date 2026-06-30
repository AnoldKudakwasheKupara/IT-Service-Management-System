using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Helpers
{
    /// <summary>Pagination metadata passed to views via ViewBag.Paging.</summary>
    public class PagingInfo
    {
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPrevious => Page > 1;
        public bool HasNext => Page < TotalPages;
        public int FirstItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
        public int LastItem => Math.Min(Page * PageSize, TotalCount);
    }

    public static class PagingExtensions
    {
        public const int DefaultPageSize = 15;

        /// <summary>Materializes one page of a query plus its paging metadata.</summary>
        public static async Task<(List<T> Items, PagingInfo Info)> PageAsync<T>(
            this IQueryable<T> query, int page, int pageSize = DefaultPageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = DefaultPageSize;

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, new PagingInfo { Page = page, PageSize = pageSize, TotalCount = total });
        }
    }
}

using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// T represents the generic type, in your case, it will be 'LoanApplication'
public class PaginatedList<T> : List<T>
{
    // The current page number being displayed.
    public int PageIndex { get; private set; }

    // The total number of pages in the dataset.
    public int TotalPages { get; private set; }

    /// <summary>
    /// Constructor for the PaginatedList.
    /// It's private because you'll use the CreateAsync factory method to instantiate it.
    /// </summary>
    /// <param name="items">The list of items for the current page.</param>
    /// <param name="count">The total count of items across all pages.</param>
    /// <param name="pageIndex">The current page index.</param>
    /// <param name="pageSize">The number of items per page.</param>
    private PaginatedList(List<T> items, int count, int pageIndex, int pageSize)
    {
        PageIndex = pageIndex;
        // Calculate the total number of pages needed.
        // For example, 10 items with a page size of 4 needs (10 / 4) + 1 = 3 pages.
        TotalPages = (int)Math.Ceiling(count / (double)pageSize);

        // Add the items for the current page to this list instance.
        this.AddRange(items);
    }

    // A property to determine if a "Previous" page link should be shown.
    public bool HasPreviousPage => PageIndex > 1;

    // A property to determine if a "Next" page link should be shown.
    public bool HasNextPage => PageIndex < TotalPages;

    /// <summary>
    /// Asynchronously creates a paginated list from an IQueryable source.
    /// This is the factory method you will call from your controller.
    /// </summary>
    /// <param name="source">The IQueryable source, e.g., from an Entity Framework DbSet.</param>
    /// <param name="pageIndex">The desired page number (1-based).</param>
    /// <param name="pageSize">The number of items to include on a page.</param>
    /// <returns>A Task that results in a new PaginatedList<T> instance.</returns>
    public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int pageIndex, int pageSize)
    {
        // First, get the total count of items in the source query.
        // This is done BEFORE skipping and taking to get the overall total.
        var count = await source.CountAsync();

        // Calculate the number of items to skip based on the page number and size.
        // For page 1, skip 0. For page 2, skip 'pageSize' items, and so on.
        var items = await source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();

        // Create and return the new PaginatedList instance.
        return new PaginatedList<T>(items, count, pageIndex, pageSize);
    }
}   
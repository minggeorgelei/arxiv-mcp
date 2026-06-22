namespace ArxivMcp.Models;

/// <summary>
/// Result of a search_papers call: the page of papers plus the OpenSearch
/// pagination totals (&lt;opensearch:totalResults&gt; etc.) from the Atom feed.
/// </summary>
public class ArxivSearchResult
{
    /// <summary>Total number of papers matching the query across all pages.</summary>
    public int TotalResults { get; set; }

    /// <summary>Zero-based offset of the first returned paper into the full result set.</summary>
    public int StartIndex { get; set; }

    /// <summary>Number of papers requested per page.</summary>
    public int ItemsPerPage { get; set; }

    /// <summary>The papers in this page.</summary>
    public IReadOnlyList<ArxivPaper> Papers { get; set; } = [];
}

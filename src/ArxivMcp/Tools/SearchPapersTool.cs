using System.ComponentModel;
using ArxivMcp.Models;
using ArxivMcp.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ArxivMcp.Tools;

/// <summary>
/// MCP tool: search arXiv papers.
/// </summary>
[McpServerToolType]
public static class SearchPapersTool
{
    [McpServerTool(Name = "search_papers")]
    [Description(
        "Search arXiv papers and return a page of metadata (title, authors, abstract, id, etc.) " +
        "together with the total number of matches. The query supports arXiv field prefixes: " +
        "ti: (title), au: (author), abs: (abstract), cat: (category), all: (any field); combine " +
        "terms with AND / OR / ANDNOT and quote phrases, e.g. 'au:vaswani AND ti:attention'. " +
        "Use 'start' with 'maxResults' to page through results beyond the first page.")]
    public static async Task<ArxivSearchResult> SearchPapers(
        ArxivClient client,
        [Description("arXiv search query. Supports field prefixes (ti:/au:/abs:/cat:/all:) and AND/OR/ANDNOT.")]
        string query,
        [Description("Optional category filter, e.g. 'cs.CL'. Appended as 'AND cat:{category}'.")]
        string? category = null,
        [Description("Maximum number of results to return (1-50). Default 10.")]
        int maxResults = 10,
        [Description("Zero-based offset of the first result, for paging. Default 0.")]
        int start = 0,
        [Description("Sort field: 'relevance', 'submitted', or 'updated'. Default 'relevance'.")]
        string sortBy = "relevance",
        [Description("Sort order: 'ascending' or 'descending'. Default 'descending'.")]
        string sortOrder = "descending",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.SearchAsync(query, category, maxResults, start, sortBy, sortOrder, cancellationToken);
        }
        catch (ArxivUnavailableException ex)
        {
            // McpException messages are surfaced to the client; other exceptions are masked by the SDK.
            throw new McpException(ex.Message);
        }
    }
}

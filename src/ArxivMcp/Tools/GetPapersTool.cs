using System.ComponentModel;
using ArxivMcp.Models;
using ArxivMcp.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ArxivMcp.Tools;

/// <summary>
/// MCP tool: fetch a single arXiv paper's full metadata by id.
/// </summary>
[McpServerToolType]
public static class GetPapersTool
{
    [McpServerTool(Name = "get_papers")]
    [Description(
        "Fetch full metadata for one or more arXiv papers by id (title, authors, abstract, " +
        "categories, dates, PDF/abstract URLs, and optional comment/journal-ref/DOI). Each id may " +
        "include a version suffix (e.g. '2101.00001v2') and an optional 'arXiv:' prefix. Ids that " +
        "don't resolve to a paper are omitted from the result.")]
    public static async Task<IReadOnlyList<ArxivPaper>> GetPapers(
        ArxivClient client,
        [Description("arXiv paper ids, e.g. ['1706.03762', '2101.00001v2'] (an 'arXiv:' prefix is also accepted).")]
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var papers = await client.GetPapersAsync(ids, cancellationToken);
            if (papers.Count == 0)
                throw new McpException($"No arXiv papers found for ids: {string.Join(", ", ids ?? [])}.");

            return papers;
        }
        catch (ArxivUnavailableException ex)
        {
            // McpException messages are surfaced to the client; other exceptions are masked by the SDK.
            throw new McpException(ex.Message);
        }
    }
}

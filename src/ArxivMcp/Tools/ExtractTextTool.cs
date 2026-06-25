using System.ComponentModel;
using ArxivMcp.Models;
using ArxivMcp.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ArxivMcp.Tools;

/// <summary>
/// MCP tool: extract the body text of an arXiv paper from its HTML version.
/// </summary>
[McpServerToolType]
public static class ExtractTextTool
{
    [McpServerTool(Name = "extract_text")]
    [Description(
        "Extract the body of an arXiv paper from its HTML version as cleaned HTML (article markup is " +
        "kept; math is collapsed to LaTeX and LaTeXML class/id noise is stripped), trying arxiv.org/html " +
        "first and falling back to ar5iv. The id may include a version suffix (e.g. '2101.00001v2') and " +
        "an optional 'arXiv:' prefix. The body is paged: each call returns a slice of at most 'maxChars' " +
        "characters starting at 'start', plus 'source' ('arxiv-html', 'ar5iv', or 'none'), 'charCount' " +
        "(full length), 'start', 'truncated', and 'nextStart' (the 'start' to pass for the next page, or " +
        "null at the end). When no HTML version exists, the PDF is NOT parsed: 'source' is 'none' and a " +
        "link to the PDF is returned instead.")]
    public static async Task<ExtractedText> ExtractText(
        ArxivClient client,
        [Description("arXiv paper id, e.g. '1706.03762' or '2101.00001v2' (an 'arXiv:' prefix is also accepted).")]
        string id,
        [Description("Character offset to start from; pass the previous response's 'nextStart' to page. Default 0.")]
        int start = 0,
        [Description("Max characters to return in this page. Default 40000, capped at 50000.")]
        int? maxChars = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new McpException("An arXiv id is required.");

        try
        {
            return await client.ExtractTextAsync(id, start, maxChars, cancellationToken);
        }
        catch (ArxivUnavailableException ex)
        {
            // McpException messages are surfaced to the client; other exceptions are masked by the SDK.
            throw new McpException(ex.Message);
        }
    }
}

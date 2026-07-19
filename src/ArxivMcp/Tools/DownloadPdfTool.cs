using System.ComponentModel;
using ArxivMcp.Models;
using ArxivMcp.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ArxivMcp.Tools;

/// <summary>
/// MCP tool: download the PDF of an arXiv paper to disk.
/// </summary>
[McpServerToolType]
public static class DownloadPdfTool
{
    [McpServerTool(Name = "download_pdf")]
    [Description(
        "Download the PDF of an arXiv paper by id. The id may include a version suffix (e.g. " +
        "'2101.00001v2') and an optional 'arXiv:' prefix. The PDF is fetched from arxiv.org/pdf, " +
        "streamed to disk, and the absolute file path, size in bytes, and source URL are returned.")]
    public static async Task<DownloadedPdf> DownloadPdf(
        ArxivClient client,
        [Description("arXiv paper id, e.g. '1706.03762' or '2101.00001v2' (an 'arXiv:' prefix is also accepted).")]
        string id,
        [Description("Directory to write the PDF into. Defaults to a 'downloads' folder under the server's working directory.")]
        string? outputDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new McpException("An arXiv id is required.");

        try
        {
            return await client.DownloadPdfAsync(id, outputDirectory, cancellationToken);
        }
        catch (ArxivUnavailableException ex)
        {
            throw new McpException(ex.Message);
        }
    }
}

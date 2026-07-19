namespace ArxivMcp.Models;

/// <summary>
/// Result of downloading the PDF of an arXiv paper to disk.
/// </summary>
public class DownloadedPdf
{
    /// <summary>Normalized arXiv id, e.g. "1706.03762" or "2101.00001v2".</summary>
    public string Id { get; set; } = "";

    /// <summary>The URL the PDF was fetched from (https://arxiv.org/pdf/{id}).</summary>
    public string Url { get; set; } = "";

    /// <summary>Absolute path of the file on disk.</summary>
    public string FilePath { get; set; } = "";

    /// <summary>Size of the saved PDF in bytes.</summary>
    public long SizeBytes { get; set; }
}

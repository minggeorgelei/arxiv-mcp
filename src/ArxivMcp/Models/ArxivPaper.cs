namespace ArxivMcp.Models;

/// <summary>
/// Bibliographic metadata for an arXiv paper, corresponding to a single &lt;entry&gt;
/// in the arXiv API Atom feed.
/// </summary>
public class ArxivPaper
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public IReadOnlyList<string> Authors { get; set; } = [];
    public string PrimaryCategory { get; set; } = "";
    public IReadOnlyList<string> Categories { get; set; } = [];
    public DateTimeOffset Published { get; set; }
    public DateTimeOffset Updated { get; set; }
    public string PdfUrl { get; set; } = "";
    public string AbsUrl { get; set; } = "";
    public string? Comment { get; set; }
    public string? JournalRef { get; set; }
    public string? Doi { get; set; }
}

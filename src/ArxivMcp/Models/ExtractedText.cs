namespace ArxivMcp.Models;

/// <summary>
/// Result of extracting the body of an arXiv paper from its HTML version, as a cleaned HTML fragment.
/// When no HTML version exists, <see cref="Source"/> is "none", <see cref="Text"/> is empty,
/// and <see cref="PdfUrl"/>/<see cref="Message"/> point at the PDF (which is not parsed).
/// </summary>
public class ExtractedText
{
    public string Id { get; set; } = "";

    /// <summary>Where the body came from: "arxiv-html", "ar5iv", or "none".</summary>
    public string Source { get; set; } = "none";

    /// <summary>
    /// The cleaned HTML body for the requested page (see <see cref="Start"/>): article markup with
    /// math collapsed to LaTeX and LaTeXML class/id noise removed.
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>Length of the full cleaned HTML in characters, across all pages.</summary>
    public int CharCount { get; set; }

    /// <summary>Character offset into the full text at which <see cref="Text"/> begins.</summary>
    public int Start { get; set; }

    /// <summary>True when more text remains past this page (i.e. <see cref="NextStart"/> is set).</summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// The <c>start</c> value to pass to fetch the next page, or null when this page reaches the end.
    /// </summary>
    public int? NextStart { get; set; }

    /// <summary>Link to the PDF; set when no HTML version was found.</summary>
    public string? PdfUrl { get; set; }

    /// <summary>Human-readable note, set when no HTML version was found.</summary>
    public string? Message { get; set; }
}

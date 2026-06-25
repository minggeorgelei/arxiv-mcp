using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace ArxivMcp.Services;

/// <summary>
/// Cleans an arXiv HTML page (arxiv.org/html or ar5iv) down to its article body.
/// Both render LaTeX through LaTeXML, so the article lives in &lt;article class="ltx_document"&gt;
/// with formulas as &lt;math&gt; elements that carry their LaTeX source in an <c>alttext</c> attribute
/// (and a matching x-tex &lt;annotation&gt;). We keep the body markup (headings, paragraphs, lists,
/// links survive), but collapse each formula to its LaTeX and drop the LaTeXML bookkeeping noise so
/// the output is compact.
/// </summary>
internal static class HtmlExtractor
{
    private static readonly HtmlParser Parser = new();

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    // An unescaped '$' (i.e. not a literal \$) inside a formula's LaTeX source. LaTeXML sometimes emits
    // these — e.g. alttext "\leq$70\,000\,000$" — which would prematurely close the delimiters we add.
    private static readonly Regex StrayDollar = new(@"(?<!\\)\$", RegexOptions.Compiled);

    // Chrome and non-prose payloads that aren't part of the readable paper body.
    // Footnotes (ltx_note_mark/ltx_note_outer) are dropped: LaTeXML renders the footnote body inline at
    // the marker, which otherwise splices the note into the middle of a sentence (and duplicates the
    // marker). Images/SVG carry no text value once extracted — relative src, "Refer to caption" alt —
    // while their figure captions (ltx_caption) survive. Note: .ltx_tag is deliberately *kept* so that
    // equation numbers "(1.1)" and section numbers "1." remain, since the prose references them.
    private const string NoiseSelector =
        "script, style, nav, header, footer, img, svg, " +
        ".ltx_page_navbar, .ltx_page_footer, .ltx_pagination, .ar5iv-footer, " +
        ".ltx_note_mark, .ltx_note_outer";

    // Attributes that only add LaTeXML bookkeeping bulk and carry no meaning for a reader. 'title' alone
    // shows up hundreds of times per paper (cross-ref tooltips); width/height ride on graphics/tables.
    private static readonly string[] NoiseAttributes = ["class", "id", "style", "title", "width", "height"];

    /// <summary>
    /// Parses <paramref name="html"/> and returns the cleaned article body as an HTML fragment, with
    /// math collapsed to <c>$...$</c>/<c>$$...$$</c> LaTeX. Returns an empty string when no body is found.
    /// </summary>
    public static string ExtractMainHtml(string html)
    {
        var doc = Parser.ParseDocument(html);

        foreach (var noise in doc.QuerySelectorAll(NoiseSelector))
            noise.Remove();

        var root = doc.QuerySelector("article.ltx_document")
            ?? doc.QuerySelector("article")
            ?? doc.QuerySelector(".ltx_page_main")
            ?? doc.QuerySelector("main")
            ?? doc.Body;

        if (root is null)
            return "";

        CollapseMath(root);
        UnwrapDeadAnchors(root);
        StripNoiseAttributes(root);
        CollapseWhitespace(root);

        return root.InnerHtml.Trim();
    }

    // LaTeXML emits each formula as a <math> element whose subtree holds the fully rendered presentation
    // MathML *and* a hidden LaTeX <annotation>; serializing that would dump both (a single "k" becomes
    // the garbled "k𝑘k\\k") and bloats the output. We replace each <math> with a plain text node holding
    // just its LaTeX source. We prefer the x-tex <annotation> over the alttext attribute: the two are
    // normally identical, but alttext occasionally carries a stray '$' that would break the delimiters.
    private static void CollapseMath(IElement root)
    {
        foreach (var math in root.QuerySelectorAll("math").ToArray())
        {
            var tex = math.QuerySelector("annotation[encoding='application/x-tex']")?.TextContent;
            if (string.IsNullOrWhiteSpace(tex))
                tex = math.GetAttribute("alttext");

            tex = Whitespace.Replace(tex?.Trim() ?? "", " ");
            tex = StrayDollar.Replace(tex, "");
            var display = math.GetAttribute("display") == "block";
            var replacement = tex.Length > 0 ? (display ? $"$${tex}$$" : $"${tex}$") : "";

            // A text node keeps the LaTeX literal: any '<' or '&' is escaped on serialization, never reparsed.
            math.Replace(root.Owner!.CreateTextNode(replacement));
        }
    }

    // Internal cross-references (<a href="#...">) point at ids we strip below, so the link is dead.
    // Keep just the visible text (e.g. "1.3", "[5]"); leave real http/mailto links intact.
    private static void UnwrapDeadAnchors(IElement root)
    {
        foreach (var anchor in root.QuerySelectorAll("a").ToArray())
        {
            var href = anchor.GetAttribute("href");
            if (href is null || href.StartsWith('#'))
                anchor.Replace(root.Owner!.CreateTextNode(anchor.TextContent));
        }
    }

    private static void StripNoiseAttributes(IElement root)
    {
        foreach (var el in root.QuerySelectorAll("*"))
        {
            foreach (var name in NoiseAttributes)
                el.RemoveAttribute(name);

            foreach (var attr in el.Attributes.Where(a => a.Name.StartsWith("data-")).ToArray())
                el.RemoveAttribute(attr.Name);
        }
    }

    // LaTeXML indents the markup heavily; squeeze whitespace runs (HTML treats them as one anyway).
    // We normalize per text node rather than over the whole serialized string so the indentation and
    // line breaks inside <pre> verbatim/listing blocks — the one place whitespace is significant —
    // survive intact.
    private static void CollapseWhitespace(IElement root)
    {
        foreach (var el in root.QuerySelectorAll("*").Prepend(root).ToArray())
        {
            if (el.Closest("pre") is not null)
                continue;

            foreach (var text in el.ChildNodes.OfType<IText>().ToArray())
                text.TextContent = Whitespace.Replace(text.TextContent, " ");
        }
    }
}

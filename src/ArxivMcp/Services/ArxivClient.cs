using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using ArxivMcp.Models;

namespace ArxivMcp.Services;

/// <summary>
/// Wraps calls to the arXiv API (http://export.arxiv.org/api/query) and Atom XML parsing.
/// All requests go through <see cref="ArxivGate"/> and retry with backoff on 429/503/connection errors.
/// </summary>
public class ArxivClient(HttpClient http, ArxivGate gate, ILogger<ArxivClient> logger)
{
    private static readonly TimeSpan[] BackoffDelays =
        [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(45)];

    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(120);

    // Namespaces used by the Atom feed.
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Arxiv = "http://arxiv.org/schemas/atom";
    private static readonly XNamespace OpenSearch = "http://a9.com/-/spec/opensearch/1.1/";

    /// <summary>
    /// Searches papers. The query supports arXiv field prefixes (ti:/au:/abs:/cat:/all: ...)
    /// and AND/OR/ANDNOT; when category is provided, "AND cat:{category}" is appended.
    /// </summary>
    public async Task<ArxivSearchResult> SearchAsync(
        string query,
        string? category = null,
        int maxResults = 10,
        int start = 0,
        string sortBy = "relevance",
        string sortOrder = "descending",
        CancellationToken cancellationToken = default)
    {
        var searchQuery = string.IsNullOrWhiteSpace(category)
            ? query
            : $"{query} AND cat:{category}";

        var url = "api/query"
            + $"?search_query={HttpUtility.UrlEncode(searchQuery)}"
            + $"&start={Math.Max(start, 0)}"
            + $"&max_results={Math.Clamp(maxResults, 1, 50)}"
            + $"&sortBy={MapSortBy(sortBy)}"
            + $"&sortOrder={MapSortOrder(sortOrder)}";

        logger.LogInformation("arXiv search: query={Query} category={Category} start={Start} max={Max}",
            query, category, start, maxResults);

        var xml = await GetStringThrottledAsync(url, cancellationToken);
        return ParseFeed(xml);
    }

    /// <summary>
    /// Fetches full metadata for one or more papers by arXiv id (via id_list). Each id may include
    /// a version suffix (e.g. 2101.00001v2) and an optional "arXiv:" prefix; both are accepted.
    /// Ids that don't resolve to a paper are simply omitted from the result.
    /// </summary>
    public async Task<IReadOnlyList<ArxivPaper>> GetPapersAsync(
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default)
    {
        var normalized = (ids ?? [])
            .Select(NormalizeId)
            .Where(s => s.Length > 0)
            .ToList();

        if (normalized.Count == 0)
            return [];

        var idList = string.Join(",", normalized);
        var url = $"api/query?id_list={HttpUtility.UrlEncode(idList)}&max_results={normalized.Count}";

        logger.LogInformation("arXiv get_paper: ids={Ids}", idList);

        var xml = await GetStringThrottledAsync(url, cancellationToken);

        // A malformed id yields a placeholder "error" entry rather than a real paper; drop those.
        return [..ParseFeed(xml).Papers.Where(p => !p.AbsUrl.Contains("/api/errors", StringComparison.OrdinalIgnoreCase))];
    }

    // A single MCP response can't hold a whole long paper, so the body is paged: each call returns at
    // most this many characters starting at the requested offset, with NextStart pointing at the rest.
    // The page is HTML, and JSON-escaping (tags -> \" , LaTeX backslashes -> \\) inflates it by ~1.4x,
    // so these are kept well below the response token limit; callers page through via 'start'.
    private const int DefaultMaxChars = 15_000;
    private const int HardMaxChars = 20_000;

    /// <summary>
    /// Fetches the HTML version of a paper and extracts its body text, trying arxiv.org/html first
    /// and falling back to ar5iv. These hosts are not the API gateway, so the fetches bypass
    /// <see cref="ArxivGate"/>; each gets its own short retry on 5xx/connection errors. When neither
    /// host has an HTML version, returns <c>Source = "none"</c> with a link to the (unparsed) PDF.
    /// </summary>
    public async Task<ExtractedText> ExtractTextAsync(
        string id, int start = 0, int? maxChars = null, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeId(id);
        if (normalized.Length == 0)
            throw new ArxivUnavailableException("No arXiv id was provided.");

        var pageSize = Math.Clamp(maxChars ?? DefaultMaxChars, 1, HardMaxChars);

        (string Source, string Url)[] attempts =
        [
            ("arxiv-html", $"https://arxiv.org/html/{normalized}"),
            ("ar5iv", $"https://ar5iv.labs.arxiv.org/html/{normalized}"),
        ];

        foreach (var (source, url) in attempts)
        {
            var html = await TryFetchHtmlAsync(url, cancellationToken);
            if (html is null)
                continue;

            var text = HtmlExtractor.ExtractMainHtml(html);
            if (text.Length == 0)
                continue;

            var from = Math.Clamp(start, 0, text.Length);
            var to = Math.Min(from + pageSize, text.Length);
            var hasMore = to < text.Length;

            logger.LogInformation(
                "extract_text: id={Id} source={Source} chars={Chars} start={Start} returned={Returned}",
                normalized, source, text.Length, from, to - from);

            return new ExtractedText
            {
                Id = normalized,
                Source = source,
                Text = text[from..to],
                CharCount = text.Length,
                Start = from,
                Truncated = hasMore,
                NextStart = hasMore ? to : null,
            };
        }

        var pdfUrl = $"https://arxiv.org/pdf/{normalized}";
        logger.LogInformation("extract_text: id={Id} source=none (no HTML version)", normalized);

        return new ExtractedText
        {
            Id = normalized,
            Source = "none",
            PdfUrl = pdfUrl,
            Message = $"No HTML version of {normalized} was found. The PDF is not parsed; see {pdfUrl}.",
        };
    }

    /// <summary>
    /// Fetches an HTML page outside the API gate. Returns null when the page doesn't exist (404/410)
    /// or stays unavailable after one retry on 5xx/connection errors — the caller treats null as
    /// "this source has no HTML" and moves on.
    /// </summary>
    private async Task<string?> TryFetchHtmlAsync(string url, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var response = await http.GetAsync(url, cancellationToken);

                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                    return null;

                if ((int)response.StatusCode >= 500)
                {
                    logger.LogWarning("HTML fetch {Url} returned {Status} (attempt {Attempt}/2)",
                        url, (int)response.StatusCode, attempt + 1);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning("HTML fetch {Url} failed (attempt {Attempt}/2): {Reason}", url, attempt + 1, ex.Message);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("HTML fetch {Url} timed out (attempt {Attempt}/2)", url, attempt + 1);
            }
        }

        return null;
    }

    /// <summary>Strips an optional "arXiv:" prefix and surrounding whitespace from an id.</summary>
    private static string NormalizeId(string id)
    {
        var trimmed = (id ?? "").Trim();
        return trimmed.StartsWith("arxiv:", StringComparison.OrdinalIgnoreCase)
            ? trimmed["arxiv:".Length..].Trim()
            : trimmed;
    }

    /// <summary>
    /// Fetches a URL through the global gate. Retries 429/503/connection failures with
    /// exponential backoff (5s/15s/45s); 429 prefers the Retry-After header. The backoff is
    /// written into the gate's schedule, so re-entering the gate is what enforces the wait —
    /// for this caller and everyone else.
    /// </summary>
    private async Task<string> GetStringThrottledAsync(string url, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var backoff = BackoffDelays[Math.Min(attempt, BackoffDelays.Length - 1)];
            try
            {
                return await gate.ExecuteAsync(async ct =>
                {
                    HttpResponseMessage response;
                    try
                    {
                        response = await http.GetAsync(url, ct);
                    }
                    catch (HttpRequestException ex)
                    {
                        throw new ArxivRetryableException($"connection to arXiv failed ({ex.Message})", backoff, ex);
                    }
                    catch (TaskCanceledException ex)
                    {
                        // Caller cancelled: propagate as-is. Otherwise it's the HttpClient timeout.
                        if (ct.IsCancellationRequested)
                            throw;
                        throw new ArxivRetryableException("request to arXiv timed out", backoff, ex);
                    }

                    using (response)
                    {
                        if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
                        {
                            var penalty = RetryAfter(response) ?? backoff;
                            throw new ArxivRetryableException(
                                $"arXiv replied {(int)response.StatusCode} {response.StatusCode}", penalty);
                        }

                        response.EnsureSuccessStatusCode();
                        return await response.Content.ReadAsStringAsync(ct);
                    }
                }, cancellationToken);
            }
            catch (ArxivRetryableException ex)
            {
                if (attempt >= BackoffDelays.Length)
                    throw new ArxivUnavailableException(
                        $"arXiv is unavailable: {ex.Message} after {BackoffDelays.Length + 1} attempts. " +
                        "Please try again in a minute or two.", ex);

                logger.LogWarning("arXiv request failed (attempt {Attempt}/{Total}): {Reason}; retrying in {Penalty}s",
                    attempt + 1, BackoffDelays.Length + 1, ex.Message, ex.Penalty.TotalSeconds);
            }
        }
    }

    /// <summary>Reads Retry-After (delta or absolute date), clamped to <see cref="MaxRetryAfter"/>.</summary>
    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        var value = header?.Delta ?? (header?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
        if (value is not { } v || v <= TimeSpan.Zero)
            return null;
        return v <= MaxRetryAfter ? v : MaxRetryAfter;
    }

    private static string MapSortBy(string sortBy) => sortBy.ToLowerInvariant() switch
    {
        "submitted" => "submittedDate",
        "updated" => "lastUpdatedDate",
        _ => "relevance",
    };

    private static string MapSortOrder(string sortOrder) =>
        sortOrder.Equals("ascending", StringComparison.OrdinalIgnoreCase) ? "ascending" : "descending";

    private static ArxivSearchResult ParseFeed(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root;

        var papers = root?
            .Elements(Atom + "entry")
            .Select(ParseEntry)
            .ToList() ?? [];

        return new ArxivSearchResult
        {
            TotalResults = ParseInt(root?.Element(OpenSearch + "totalResults")),
            StartIndex = ParseInt(root?.Element(OpenSearch + "startIndex")),
            ItemsPerPage = ParseInt(root?.Element(OpenSearch + "itemsPerPage")),
            Papers = papers,
        };
    }

    private static int ParseInt(XElement? element) =>
        int.TryParse((string?)element, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static ArxivPaper ParseEntry(XElement entry)
    {
        var absUrl = (string?)entry.Element(Atom + "id") ?? "";
        // The Atom id looks like http://arxiv.org/abs/1706.03762v7 — take the tail as the arXiv id.
        var id = absUrl.Split("/abs/").LastOrDefault() ?? absUrl;

        var authors = entry.Elements(Atom + "author")
            .Select(a => (string?)a.Element(Atom + "name") ?? "")
            .Where(n => n.Length > 0)
            .ToList();

        var categories = entry.Elements(Atom + "category")
            .Select(c => (string?)c.Attribute("term") ?? "")
            .Where(t => t.Length > 0)
            .ToList();

        var pdfUrl = entry.Elements(Atom + "link")
            .FirstOrDefault(l => (string?)l.Attribute("title") == "pdf")?
            .Attribute("href")?.Value
            ?? $"https://arxiv.org/pdf/{id}";

        return new ArxivPaper
        {
            Id = id,
            Title = Normalize((string?)entry.Element(Atom + "title")),
            Summary = Normalize((string?)entry.Element(Atom + "summary")),
            Authors = authors,
            PrimaryCategory = (string?)entry.Element(Arxiv + "primary_category")?.Attribute("term") ?? "",
            Categories = categories,
            Published = ParseDate((string?)entry.Element(Atom + "published")),
            Updated = ParseDate((string?)entry.Element(Atom + "updated")),
            PdfUrl = pdfUrl,
            AbsUrl = absUrl,
            Comment = (string?)entry.Element(Arxiv + "comment"),
            JournalRef = (string?)entry.Element(Arxiv + "journal_ref"),
            Doi = (string?)entry.Element(Arxiv + "doi"),
        };
    }

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    // Collapse internal whitespace/newlines that arXiv inserts into title/summary text.
    private static string Normalize(string? s) =>
        string.IsNullOrWhiteSpace(s) ? "" : Whitespace.Replace(s.Trim(), " ");

    private static DateTimeOffset ParseDate(string? s) =>
        DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d)
            ? d
            : default;
}

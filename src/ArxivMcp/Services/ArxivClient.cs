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

    /// <summary>
    /// Searches papers. The query supports arXiv field prefixes (ti:/au:/abs:/cat:/all: ...)
    /// and AND/OR/ANDNOT; when category is provided, "AND cat:{category}" is appended.
    /// </summary>
    public async Task<IReadOnlyList<ArxivPaper>> SearchAsync(
        string query,
        string? category = null,
        int maxResults = 10,
        string sortBy = "relevance",
        string sortOrder = "descending",
        CancellationToken cancellationToken = default)
    {
        var searchQuery = string.IsNullOrWhiteSpace(category)
            ? query
            : $"{query} AND cat:{category}";

        var url = "api/query"
            + $"?search_query={HttpUtility.UrlEncode(searchQuery)}"
            + $"&max_results={Math.Clamp(maxResults, 1, 50)}"
            + $"&sortBy={MapSortBy(sortBy)}"
            + $"&sortOrder={MapSortOrder(sortOrder)}";

        logger.LogInformation("arXiv search: query={Query} category={Category} max={Max}",
            query, category, maxResults);

        var xml = await GetStringThrottledAsync(url, cancellationToken);
        return ParseFeed(xml);
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

    private static List<ArxivPaper> ParseFeed(string xml)
    {
        var doc = XDocument.Parse(xml);
        return doc.Root?
            .Elements(Atom + "entry")
            .Select(ParseEntry)
            .ToList() ?? [];
    }

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

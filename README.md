# arxiv-mcp

An [MCP](https://modelcontextprotocol.io) server for [arXiv](https://arxiv.org), built on
ASP.NET Core (.NET 8). It exposes a set of tools for searching, fetching, extracting, and
downloading arXiv papers, while respecting arXiv's rate-limit policy across all connected
clients.

## Features

- **`search_papers` tool** — search arXiv and get back structured metadata (title, authors,
  abstract, categories, PDF/abstract links, DOI, journal reference, publish/update dates)
  together with the total number of matches and pagination support.
- **`get_papers` tool** — fetch full metadata for one or more papers by id in a single call.
- **`list_categories` tool** — list arXiv subject categories from a built-in offline table,
  optionally filtered by archive group (e.g. `cs`, `math`, `physics`).
- **`extract_text` tool** — extract the body of an arXiv paper from its HTML version as cleaned
  HTML, with character-based paging for large papers. Falls back across `arxiv.org/html` →
  `ar5iv` and returns a PDF link when no HTML version is available.
- **`download_pdf` tool** — stream an arXiv paper's PDF to disk and return the file path,
  size, and source URL.
- **arXiv query syntax** — supports field prefixes (`ti:`, `au:`, `abs:`, `cat:`, `all:`) and
  boolean operators (`AND` / `OR` / `ANDNOT`), e.g. `au:vaswani AND ti:attention`.
- **Polite rate limiting** — a global gate enforces a single in-flight request and ≥ 3s between
  requests, shared process-wide as required by the arXiv API terms of use.
- **Resilient** — retries `429` / `503` / connection failures with exponential backoff
  (5s / 15s / 45s), honoring `Retry-After`, and surfaces clear errors to the client.
- **Streamable HTTP transport** — served at `/`, with legacy SSE at `/sse` + `/message`.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)

## Configuration

arXiv asks API clients to include a contact address in the `User-Agent`. Set it via the
`Arxiv:ContactEmail` configuration key (in `appsettings.json`, an environment variable, or
user secrets):

```json
{
  "Arxiv": {
    "ContactEmail": "you@example.com"
  }
}
```

If it is not set, the server logs a warning and falls back to a generic `User-Agent`.

## Running

```bash
dotnet run --project src/ArxivMcp
```

The MCP endpoint is then available at the server's base address (e.g. `http://localhost:5000/`).

## Tools

### `search_papers`

Search arXiv and return a page of metadata together with the total number of matches.

| Parameter    | Type     | Default       | Description                                                              |
|--------------|----------|---------------|--------------------------------------------------------------------------|
| `query`      | string   | *(required)*  | arXiv search query. Supports `ti:`/`au:`/`abs:`/`cat:`/`all:` and `AND`/`OR`/`ANDNOT`. |
| `category`   | string?  | `null`        | Optional category filter, e.g. `cs.CL`. Appended as `AND cat:{category}`. |
| `maxResults` | int      | `10`          | Maximum number of results to return (clamped to 1–50).                   |
| `start`      | int      | `0`           | Zero-based offset of the first result, for paging through large result sets. |
| `sortBy`     | string   | `relevance`   | Sort field: `relevance`, `submitted`, or `updated`.                      |
| `sortOrder`  | string   | `descending`  | Sort order: `ascending` or `descending`.                                |

Each result is an `ArxivPaper` with: `id`, `title`, `summary`, `authors`, `primaryCategory`,
`categories`, `published`, `updated`, `pdfUrl`, `absUrl`, `comment`, `journalRef`, `doi`.

The response also carries `totalResults` (the total number of matches reported by arXiv) and
`startIndex`, so callers can compute the next page from `start + maxResults` until
`startIndex + len(results) >= totalResults`.

### `get_papers`

Fetch full metadata for one or more arXiv papers in a single call.

| Parameter | Type                  | Default      | Description                                                              |
|-----------|-----------------------|--------------|--------------------------------------------------------------------------|
| `ids`     | string[]              | *(required)* | arXiv paper ids, e.g. `["1706.03762", "2101.00001v2"]`. An `arXiv:` prefix is also accepted. |

Each id may include a version suffix (e.g. `2101.00001v2`) and an optional `arXiv:` prefix.
Ids that don't resolve to a paper are omitted from the result; if none resolve, the call
returns an error. Returns an `ArxivPaper` for each id.

### `list_categories`

List arXiv subject categories from a built-in offline table.

| Parameter | Type    | Default | Description                                                              |
|-----------|---------|---------|--------------------------------------------------------------------------|
| `group`   | string? | `null`  | Optional archive group to filter by, e.g. `cs`, `math`, `physics`. Omit to return all categories. |

Returns `categories` (an array of `ArxivCategory` with `code`, `name`, `group`) and
`totalCount`.

### `extract_text`

Extract the body of an arXiv paper from its HTML version as cleaned HTML (article markup is
kept; math is collapsed to LaTeX and LaTeXML class/id noise is stripped), trying
`arxiv.org/html` first and falling back to `ar5iv`.

| Parameter   | Type    | Default | Description                                                              |
|-------------|---------|---------|--------------------------------------------------------------------------|
| `id`        | string  | *(required)* | arXiv paper id, e.g. `1706.03762` or `2101.00001v2`. An `arXiv:` prefix is also accepted. |
| `start`     | int     | `0`     | Character offset to start from. Pass the previous response's `nextStart` to page. |
| `maxChars`  | int?    | `40000` | Max characters to return in this page. Capped at 50000.                  |

Returns an `ExtractedText` with:

- `id` — the normalized arXiv id.
- `source` — `"arxiv-html"`, `"ar5iv"`, or `"none"`.
- `text` — the cleaned HTML body for the requested page.
- `charCount` — full length in characters, across all pages.
- `start` — the offset at which `text` begins.
- `truncated` — true when more text remains past this page.
- `nextStart` — the `start` to pass for the next page, or `null` at the end.
- `pdfUrl` / `message` — set when no HTML version is found; the PDF is not parsed in that case.

### `download_pdf`

Download the PDF of an arXiv paper and write it to disk.

| Parameter        | Type    | Default     | Description                                                              |
|------------------|---------|-------------|--------------------------------------------------------------------------|
| `id`             | string  | *(required)* | arXiv paper id, e.g. `1706.03762` or `2101.00001v2`. An `arXiv:` prefix is also accepted. |
| `outputDirectory`| string? | `null`      | Directory to write the PDF into. Defaults to a `downloads` folder under the server's working directory. |

The PDF is fetched from `https://arxiv.org/pdf/{id}` and streamed to disk. Returns a
`DownloadedPdf` with:

- `id` — the normalized arXiv id.
- `url` — the URL the PDF was fetched from.
- `filePath` — absolute path of the file on disk.
- `sizeBytes` — size of the saved PDF in bytes.

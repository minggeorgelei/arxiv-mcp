# arxiv-mcp

An [MCP](https://modelcontextprotocol.io) server for [arXiv](https://arxiv.org), built on
ASP.NET Core (.NET 8). It exposes a single `search_papers` tool that queries the arXiv API
and returns structured paper metadata, while respecting arXiv's rate-limit policy across all
connected clients.

## Features

- **`search_papers` tool** — search arXiv and get back structured metadata (title, authors,
  abstract, categories, PDF/abstract links, DOI, journal reference, publish/update dates).
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

## The `search_papers` tool

| Parameter    | Type     | Default       | Description                                                              |
|--------------|----------|---------------|--------------------------------------------------------------------------|
| `query`      | string   | *(required)*  | arXiv search query. Supports `ti:`/`au:`/`abs:`/`cat:`/`all:` and `AND`/`OR`/`ANDNOT`. |
| `category`   | string?  | `null`        | Optional category filter, e.g. `cs.CL`. Appended as `AND cat:{category}`. |
| `maxResults` | int      | `10`          | Maximum number of results to return (clamped to 1–50).                   |
| `sortBy`     | string   | `relevance`   | Sort field: `relevance`, `submitted`, or `updated`.                      |
| `sortOrder`  | string   | `descending`  | Sort order: `ascending` or `descending`.                                |

Each result is an `ArxivPaper` with: `id`, `title`, `summary`, `authors`, `primaryCategory`,
`categories`, `published`, `updated`, `pdfUrl`, `absUrl`, `comment`, `journalRef`, `doi`.

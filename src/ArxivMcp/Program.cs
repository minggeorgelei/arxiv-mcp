using ArxivMcp.Services;

var builder = WebApplication.CreateBuilder(args);

// Register the MCP server with HTTP/SSE transport and auto-discover [McpServerToolType] tools.
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Global throttle for arXiv: single connection, >= 3s between requests. Must be a
// singleton — the limit applies to the whole process, not per MCP client/session.
builder.Services.AddSingleton<ArxivGate>();

// arXiv asks for a contact address in the User-Agent; configured via Arxiv:ContactEmail.
var contactEmail = builder.Configuration["Arxiv:ContactEmail"];

// Typed HttpClient for the arXiv API.
builder.Services.AddHttpClient<ArxivClient>(c =>
{
    c.BaseAddress = new Uri("https://export.arxiv.org/");
    // A hung request holds the gate and stalls every other tool call, but the timeout must
    // still cover arXiv's real latency: sorted queries (sortBy=submittedDate) routinely take
    // 60-70s server-side.
    c.Timeout = TimeSpan.FromSeconds(100);
    c.DefaultRequestHeaders.UserAgent.ParseAdd(
        string.IsNullOrWhiteSpace(contactEmail)
            ? "arxiv-mcp/1.0"
            : $"arxiv-mcp/1.0 (mailto:{contactEmail})");
});

var app = builder.Build();

if (string.IsNullOrWhiteSpace(contactEmail))
    app.Logger.LogWarning(
        "Arxiv:ContactEmail is not configured; arXiv recommends a contact address in the User-Agent.");

// Exposes Streamable HTTP at "/" and legacy SSE at "/sse" + "/message".
app.MapMcp();

app.Run();

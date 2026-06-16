using System.ComponentModel;
using ArxivMcp.Models;
using ModelContextProtocol.Server;

namespace ArxivMcp.Tools;

/// <summary>
/// MCP tool: list arXiv categories from a static, offline table.
/// </summary>
[McpServerToolType]
public static class ListCategoriesTool
{
    [McpServerTool(Name = "list_categories")]
    [Description(
        "List arXiv subject categories (code, name, group) from a built-in offline table. " +
        "Optionally filter by archive group such as 'cs', 'math', or 'physics'.")]
    public static ListCategoriesResult ListCategories(
        [Description("Optional archive group to filter by (e.g. 'cs', 'math', 'physics'). Omit to return all categories.")]
        string? group = null)
    {
        var categories = ArxivCategories.ByGroup(group);
        return new ListCategoriesResult
        {
            Categories = categories,
            TotalCount = categories.Count,
        };
    }
}

/// <summary>
/// Result of the list_categories tool.
/// </summary>
public class ListCategoriesResult
{
    public IReadOnlyList<ArxivCategory> Categories { get; set; } = [];
    public int TotalCount { get; set; }
}

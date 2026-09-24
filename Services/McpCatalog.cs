using LlamaLauncher.Models;

namespace LlamaLauncher.Services;

/// <summary>
/// Curated MCP servers for C#/.NET agentic development, roughly in order of value.
/// Commands run wherever OpenCode runs (for example inside WSL), so requirements
/// refer to that environment, not the machine running LlamaLauncher.
/// </summary>
public static class McpCatalog
{
    /// <summary>All servers offered in the UI.</summary>
    public static IReadOnlyList<McpServerDefinition> All { get; } =
    [
        new McpServerDefinition
        {
            Id = "roslyn",
            DisplayName = "Roslyn (C# semantics)",
            Description = "Compiler-accurate diagnostics, symbol search, find references, and safe renames across the solution.",
            Requirement = "roslyn-mcp .NET global tool (RoslynMcpServer) on PATH.",
            Transport = McpTransport.Local,
            Command = ["roslyn-mcp"],
            TimeoutMs = 60000,
            Signature = "roslyn",
            RecommendedDefault = true
        },
        new McpServerDefinition
        {
            Id = "microsoft-learn",
            DisplayName = "Microsoft Learn docs",
            Description = "Searches official Microsoft, .NET, and Azure documentation and code samples. Free, no key.",
            Requirement = "Internet access.",
            Transport = McpTransport.Remote,
            Url = "https://learn.microsoft.com/api/mcp",
            Signature = "learn.microsoft.com/api/mcp",
            RecommendedDefault = true
        },
        new McpServerDefinition
        {
            Id = "context7",
            DisplayName = "Context7 library docs",
            Description = "Up-to-date, version-specific docs for libraries (Avalonia, EF Core, Serilog, and more).",
            Requirement = "Internet access. Optional CONTEXT7_API_KEY for higher rate limits.",
            Transport = McpTransport.Remote,
            Url = "https://mcp.context7.com/mcp",
            Signature = "mcp.context7.com",
            RecommendedDefault = true
        },
        new McpServerDefinition
        {
            Id = "nuget",
            DisplayName = "NuGet (Microsoft)",
            Description = "Live package info, latest compatible versions, and fixes for vulnerable packages, including transitive ones.",
            Requirement = ".NET 10 SDK or later (provides the dnx command).",
            Transport = McpTransport.Local,
            Command = ["dnx", "NuGet.Mcp.Server", "--yes"],
            TimeoutMs = 60000,
            Signature = "NuGet.Mcp.Server"
        },
        new McpServerDefinition
        {
            Id = "gh_grep",
            DisplayName = "Grep GitHub code",
            Description = "Searches real-world code on GitHub for usage examples of an API or pattern.",
            Requirement = "Internet access.",
            Transport = McpTransport.Remote,
            Url = "https://mcp.grep.app",
            Signature = "mcp.grep.app"
        },
        new McpServerDefinition
        {
            Id = "github",
            DisplayName = "GitHub (issues, PRs, repos)",
            Description = "Reads and manages issues, pull requests, branches, and Actions runs in your repositories.",
            Requirement = "GITHUB_PERSONAL_ACCESS_TOKEN environment variable set where OpenCode runs.",
            Transport = McpTransport.Remote,
            Url = "https://api.githubcopilot.com/mcp/",
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer {env:GITHUB_PERSONAL_ACCESS_TOKEN}"
            },
            DisableOAuth = true,
            Signature = "api.githubcopilot.com/mcp"
        },
        new McpServerDefinition
        {
            Id = "sequential-thinking",
            DisplayName = "Sequential thinking",
            Description = "Structured step-by-step planning tool; helps smaller local models on multi-step tasks.",
            Requirement = "Node.js (npx).",
            Transport = McpTransport.Local,
            Command = ["npx", "-y", "@modelcontextprotocol/server-sequential-thinking"],
            Signature = "server-sequential-thinking"
        },
        new McpServerDefinition
        {
            Id = "playwright",
            DisplayName = "Playwright browser",
            Description = "Drives a real browser to test ASP.NET/Blazor UIs, click through pages, and read the DOM.",
            Requirement = "Node.js (npx); the first run downloads a browser.",
            Transport = McpTransport.Local,
            Command = ["npx", "-y", "@playwright/mcp@latest"],
            TimeoutMs = 60000,
            Signature = "@playwright/mcp"
        },
        new McpServerDefinition
        {
            Id = "azure",
            DisplayName = "Azure (Microsoft)",
            Description = "Queries and manages Azure resources: App Service, storage, SQL, Key Vault, and logs.",
            Requirement = "Node.js (npx) and a signed-in Azure CLI (az login).",
            Transport = McpTransport.Local,
            Command = ["npx", "-y", "@azure/mcp@latest", "server", "start"],
            TimeoutMs = 60000,
            Signature = "@azure/mcp"
        },
        new McpServerDefinition
        {
            Id = "memory",
            DisplayName = "Memory (knowledge graph)",
            Description = "Persistent notes the agent can store and recall across sessions (decisions, conventions).",
            Requirement = "Node.js (npx).",
            Transport = McpTransport.Local,
            Command = ["npx", "-y", "@modelcontextprotocol/server-memory"],
            Signature = "server-memory"
        }
    ];

    /// <summary>Ids checked on a fresh install.</summary>
    public static List<string> DefaultEnabledIds()
        => All.Where(s => s.RecommendedDefault).Select(s => s.Id).ToList();

    /// <summary>Display names for the given ids, in catalog order.</summary>
    public static IEnumerable<string> DisplayNames(IEnumerable<string> ids)
    {
        var set = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        return All.Where(s => set.Contains(s.Id)).Select(s => s.DisplayName);
    }
}

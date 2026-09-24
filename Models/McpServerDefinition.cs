namespace LlamaLauncher.Models;

/// <summary>How OpenCode connects to an MCP server.</summary>
public enum McpTransport
{
    /// <summary>OpenCode starts a process and talks MCP over stdio.</summary>
    Local,

    /// <summary>OpenCode connects to a streamable-HTTP MCP endpoint.</summary>
    Remote
}

/// <summary>
/// A curated MCP server that LlamaLauncher can add to, enable, or disable in
/// opencode.json. Definitions are data only; the sync service turns them into
/// config entries.
/// </summary>
public sealed record McpServerDefinition
{
    /// <summary>Key used for the server entry in opencode.json (also its tool-name prefix).</summary>
    public required string Id { get; init; }

    /// <summary>Short name shown on the checkbox.</summary>
    public required string DisplayName { get; init; }

    /// <summary>One-line description of what the server gives the agent.</summary>
    public required string Description { get; init; }

    /// <summary>What must be installed where OpenCode runs for the server to start.</summary>
    public required string Requirement { get; init; }

    /// <summary>Local (process) or remote (URL) server.</summary>
    public required McpTransport Transport { get; init; }

    /// <summary>Executable and arguments for a local server.</summary>
    public IReadOnlyList<string> Command { get; init; } = [];

    /// <summary>Endpoint URL for a remote server.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Environment variables for a local server (values may use OpenCode's {env:NAME}).</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

    /// <summary>HTTP headers for a remote server (values may use OpenCode's {env:NAME}).</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <summary>Request timeout in milliseconds; null keeps OpenCode's default (5000 ms).</summary>
    public int? TimeoutMs { get; init; }

    /// <summary>Writes <c>oauth: false</c> for remote servers authenticated by a header token.</summary>
    public bool DisableOAuth { get; init; }

    /// <summary>
    /// Text that identifies this server inside an existing entry's command or URL, so an
    /// entry the user already configured under a different key is recognized, not duplicated.
    /// </summary>
    public required string Signature { get; init; }

    /// <summary>Checked on a fresh install. Kept small: every server adds tool definitions to the prompt.</summary>
    public bool RecommendedDefault { get; init; }
}

using LlamaLauncher.Services;

namespace LlamaLauncher.Models;

/// <summary>
/// Root of the JSON config file: global paths plus a map of
/// gguf file name -> its <see cref="ModelConfig"/>.
/// </summary>
public sealed class AppConfig
{
    public string LlamaServerPath { get; set; } = @"C:\llama\llama-server.exe";
    public string ModelsFolder { get; set; } = @"C:\llama\models";

    /// <summary>Path to the OpenCode config to keep in sync. Empty disables syncing.</summary>
    public string OpenCodePath { get; set; } = string.Empty;

    /// <summary>When true, loading a model rewrites the matching model's limit.context in opencode.json.</summary>
    public bool SyncOpenCodeOnLoad { get; set; } = true;

    /// <summary>
    /// Where to write the native llama.cpp INI preset (the <c>--models-preset</c> router file).
    /// Empty falls back to <c>config.ini</c> next to the executable.
    /// </summary>
    public string PresetIniPath { get; set; } = string.Empty;

    /// <summary>When true, every config save also rewrites the llama-server INI preset.</summary>
    public bool WritePresetIni { get; set; } = true;

    /// <summary>
    /// Ids from <see cref="McpCatalog"/> to enable in opencode.json. Catalog servers not
    /// listed are disabled (never deleted) when agent tools are applied.
    /// </summary>
    public List<string> EnabledMcpServers { get; set; } = McpCatalog.DefaultEnabledIds();

    /// <summary>
    /// Turns on OpenCode's built-in LSP servers, including C#. OpenCode disables every
    /// LSP server when its config has no "lsp" key.
    /// </summary>
    public bool OpenCodeEnableLsp { get; set; } = true;

    public Dictionary<string, ModelConfig> Models { get; set; } = new();
}

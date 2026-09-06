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

    public Dictionary<string, ModelConfig> Models { get; set; } = new();
}

using System.Diagnostics;
using System.IO;
using System.Text.Json;
using LlamaLauncher.Models;

namespace LlamaLauncher.Services;

/// <summary>
/// Reads and writes the application's JSON config file.
///
/// Storage location is chosen so the app works both as a portable build and as an
/// installed one:
///   * Portable / dev: if a <c>llama-models.json</c> sits next to the executable
///     and that folder is writable (a zip drop or <c>dotnet run</c>), it is used
///     in place, matching the original behaviour.
///   * Installed: otherwise config lives under a per-user data directory
///     (<c>%APPDATA%\LlamaLauncher</c> on Windows, <c>~/.config/LlamaLauncher</c>
///     on Linux/macOS), because an install under Program Files is not writable.
///     On first run it is seeded from the bundled default next to the executable.
/// </summary>
public sealed class ConfigService
{
    private const string ConfigFileName = "llama-models.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Full path to the active config file.</summary>
    public string ConfigPath { get; }

    /// <summary>Directory that holds the active config (and the default preset location).</summary>
    public string DataDirectory { get; }

    public ConfigService(string? configPath = null)
    {
        if (configPath is not null)
        {
            ConfigPath = configPath;
            DataDirectory = Path.GetDirectoryName(configPath) ?? AppContext.BaseDirectory;
            return;
        }

        var baseDir = AppContext.BaseDirectory;
        var localConfig = Path.Combine(baseDir, ConfigFileName);

        if (File.Exists(localConfig) && IsWritable(baseDir))
        {
            // Portable / dev: keep everything next to the executable.
            DataDirectory = baseDir;
            ConfigPath = localConfig;
        }
        else
        {
            // Installed: use a per-user data directory that is always writable.
            DataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LlamaLauncher");
            Directory.CreateDirectory(DataDirectory);
            ConfigPath = Path.Combine(DataDirectory, ConfigFileName);

            // Seed the profiles that ship next to the exe on first run.
            if (!File.Exists(ConfigPath) && File.Exists(localConfig))
            {
                try { File.Copy(localConfig, ConfigPath); }
                catch (Exception ex) { Debug.WriteLine($"Config seed failed: {ex.Message}"); }
            }
        }
    }

    public AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, Options);
                if (cfg is not null)
                {
                    cfg.Models ??= new Dictionary<string, ModelConfig>();
                    return cfg;
                }
            }
        }
        catch (Exception ex)
        {
            // A corrupt or unreadable file should not crash the app; start clean.
            Debug.WriteLine($"Config load failed: {ex.Message}");
        }

        return new AppConfig();
    }

    public void Save(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, Options);
        var dir = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(ConfigPath, json);
    }

    private static bool IsWritable(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, ".write-test-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

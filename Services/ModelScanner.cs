using System.IO;

namespace LlamaLauncher.Services;

/// <summary>Enumerates loadable .gguf models in a folder.</summary>
public sealed class ModelScanner
{
    public IReadOnlyList<string> Scan(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return Array.Empty<string>();

        return Directory
            .EnumerateFiles(folder, "*.gguf", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            // Skip vision projectors; they are not loadable base models.
            .Where(name => !name.StartsWith("mmproj", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

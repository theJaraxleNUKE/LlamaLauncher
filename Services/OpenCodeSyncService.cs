using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LlamaLauncher.Services;

/// <summary>
/// Keeps an OpenCode config (opencode.json) in step with the loaded model by
/// rewriting <c>provider.*.models.&lt;modelKey&gt;.limit.context</c>.
/// </summary>
public sealed class OpenCodeSyncService
{
    public readonly record struct Result(bool Changed, string Message);

    /// <summary>
    /// Sets limit.context (and optionally limit.output) for every provider model
    /// whose key equals <paramref name="modelKey"/>. Returns a human-readable result.
    /// </summary>
    public Result UpdateContext(string openCodePath, string modelKey, int context, int? output = null)
    {
        if (string.IsNullOrWhiteSpace(openCodePath))
            return new Result(false, "OpenCode path not set; skipped.");
        if (!File.Exists(openCodePath))
            return new Result(false, $"opencode.json not found at: {openCodePath}");
        if (string.IsNullOrWhiteSpace(modelKey))
            return new Result(false, "Model has no alias; cannot match an OpenCode model entry.");

        JsonNode? root;
        try
        {
            var text = File.ReadAllText(openCodePath);
            var docOptions = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };
            root = JsonNode.Parse(text, documentOptions: docOptions);
        }
        catch (Exception ex)
        {
            return new Result(false, $"Failed to parse opencode.json: {ex.Message}");
        }

        if (root is null)
            return new Result(false, "opencode.json is empty.");

        if (root is not JsonObject rootObj)
            return new Result(false, "opencode.json root is not a JSON object.");

        if (rootObj["provider"] is not JsonObject providers)
            return new Result(false, "No \"provider\" section in opencode.json.");

        var updated = 0;
        foreach (var provider in providers)
        {
            if (provider.Value is not JsonObject providerObj) continue;
            if (providerObj["models"] is not JsonObject models) continue;
            if (models[modelKey] is not JsonObject model) continue;

            if (model["limit"] is not JsonObject limit)
            {
                limit = new JsonObject();
                model["limit"] = limit;
            }

            limit["context"] = context;
            if (output is int o)
                limit["output"] = o;

            updated++;
        }

        if (updated == 0)
            return new Result(false, $"No model \"{modelKey}\" found under any provider in opencode.json.");

        try
        {
            var writeOptions = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(openCodePath, root.ToJsonString(writeOptions) + Environment.NewLine);
        }
        catch (Exception ex)
        {
            return new Result(false, $"Failed to write opencode.json: {ex.Message}");
        }

        var entries = updated == 1 ? "entry" : "entries";
        return new Result(true, $"Set limit.context = {context} for \"{modelKey}\" ({updated} {entries}).");
    }
}

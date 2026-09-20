using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LlamaLauncher.Services;

/// <summary>
/// Keeps an OpenCode config (opencode.json) configured for the loaded model.
/// Updates the matching provider model's limit.context (preserving limit.output),
/// creates the provider/model entry when it does not exist yet, and repoints the
/// active model/small_model at it — so launching a profile leaves OpenCode ready
/// to talk to whatever llama-server the app just started.
/// </summary>
public sealed class OpenCodeSyncService
{
    public readonly record struct Result(bool Changed, string Message);

    private const string ProviderId = "llama-local";

    /// <summary>Sensible max output tokens for a freshly created model entry.</summary>
    private static int DefaultOutput(int context) => Math.Clamp(context / 4, 4096, 32768);

    /// <summary>
    /// Ensures opencode.json describes <paramref name="alias"/> (creating the file,
    /// provider, and model entry if needed), sets its context, and — when
    /// <paramref name="switchActiveModel"/> is true — makes it the active model.
    /// </summary>
    public Result Sync(string openCodePath, string alias, string displayName, int context,
                       string host, int port, bool switchActiveModel = true)
    {
        if (string.IsNullOrWhiteSpace(openCodePath))
            return new Result(false, "OpenCode path not set; skipped.");
        if (string.IsNullOrWhiteSpace(alias))
            return new Result(false, "Model has no alias; cannot configure an OpenCode model entry.");

        // Load existing config, or start a fresh one if the file is missing/empty.
        JsonObject rootObj;
        var existed = File.Exists(openCodePath);
        if (existed)
        {
            try
            {
                var text = File.ReadAllText(openCodePath);
                var docOptions = new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                };
                var parsed = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text, documentOptions: docOptions);
                rootObj = parsed as JsonObject ?? new JsonObject();
            }
            catch (Exception ex)
            {
                return new Result(false, $"Failed to parse opencode.json: {ex.Message}");
            }
        }
        else
        {
            rootObj = new JsonObject { ["$schema"] = "https://opencode.ai/config.json" };
        }

        if (rootObj["provider"] is not JsonObject providers)
        {
            providers = new JsonObject();
            rootObj["provider"] = providers;
        }

        // 1) Update every existing entry that already matches the alias.
        var updated = 0;
        string? foundProvider = null;
        foreach (var provider in providers)
        {
            if (provider.Value is not JsonObject providerObj) continue;
            if (providerObj["models"] is not JsonObject models) continue;
            if (models[alias] is not JsonObject model) continue;

            if (model["limit"] is not JsonObject limit)
            {
                limit = new JsonObject();
                model["limit"] = limit;
            }
            limit["context"] = context;
            if (limit["output"] is null)
                limit["output"] = DefaultOutput(context);

            foundProvider ??= provider.Key;
            updated++;
        }

        // 2) If nothing matched, create the entry under a target provider.
        var created = false;
        if (updated == 0)
        {
            var targetKey = ResolveTargetProvider(providers);
            if (providers[targetKey] is not JsonObject targetProvider)
            {
                targetProvider = NewProvider(host, port);
                providers[targetKey] = targetProvider;
            }
            if (targetProvider["models"] is not JsonObject targetModels)
            {
                targetModels = new JsonObject();
                targetProvider["models"] = targetModels;
            }

            targetModels[alias] = new JsonObject
            {
                ["name"] = string.IsNullOrWhiteSpace(displayName) ? alias : displayName,
                ["supportsToolCalls"] = true,
                ["limit"] = new JsonObject { ["context"] = context, ["output"] = DefaultOutput(context) }
            };

            foundProvider = targetKey;
            created = true;
        }

        // 3) Point OpenCode at this model.
        var switchedActive = false;
        if (switchActiveModel && foundProvider is not null)
        {
            var qualified = $"{foundProvider}/{alias}";
            rootObj["model"] = qualified;
            rootObj["small_model"] = qualified;
            switchedActive = true;
        }

        // 4) Persist.
        try
        {
            var dir = Path.GetDirectoryName(openCodePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var writeOptions = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(openCodePath, rootObj.ToJsonString(writeOptions) + Environment.NewLine);
        }
        catch (Exception ex)
        {
            return new Result(false, $"Failed to write opencode.json: {ex.Message}");
        }

        var verb = created ? (existed ? "Added" : "Created opencode.json and added") : "Updated";
        var activeNote = switchedActive ? $"; active model -> {foundProvider}/{alias}" : "";
        return new Result(true, $"{verb} \"{alias}\" (context {context}){activeNote}.");
    }

    /// <summary>Prefer the app's own provider, else a single existing provider, else create ours.</summary>
    private static string ResolveTargetProvider(JsonObject providers)
    {
        if (providers[ProviderId] is JsonObject)
            return ProviderId;

        string? only = null;
        var count = 0;
        foreach (var p in providers)
        {
            if (p.Value is JsonObject) { only = p.Key; count++; }
        }
        return count == 1 && only is not null ? only : ProviderId;
    }

    private static JsonObject NewProvider(string host, int port)
    {
        var clientHost = string.IsNullOrWhiteSpace(host) || host == "0.0.0.0" ? "127.0.0.1" : host;
        return new JsonObject
        {
            ["npm"] = "@ai-sdk/openai-compatible",
            ["name"] = "llama.cpp (LlamaLauncher)",
            ["options"] = new JsonObject
            {
                ["baseURL"] = $"http://{clientHost}:{port}/v1",
                ["apiKey"] = "local"
            },
            ["models"] = new JsonObject()
        };
    }
}
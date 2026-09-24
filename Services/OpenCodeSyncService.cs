using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using LlamaLauncher.Models;

namespace LlamaLauncher.Services;

/// <summary>What LlamaLauncher turns on in OpenCode besides the model itself.</summary>
/// <param name="Catalog">Every MCP server the launcher manages.</param>
/// <param name="EnabledIds">Catalog ids to enable; the rest are disabled (never deleted).</param>
/// <param name="EnableLsp">Turn on OpenCode's built-in LSP servers (C# included).</param>
public sealed record OpenCodeFeatures(
    IReadOnlyList<McpServerDefinition> Catalog,
    IReadOnlySet<string> EnabledIds,
    bool EnableLsp);

/// <summary>
/// Keeps an OpenCode config (opencode.json) configured for the loaded model and the
/// selected agent tools. Model sync updates or creates the provider model entry and makes
/// it active. Feature sync enables or disables curated MCP servers and writes the LSP
/// block. Entries the user already configured are only toggled, never rewritten.
/// </summary>
public sealed class OpenCodeSyncService
{
    public readonly record struct Result(bool Changed, string Message);

    private const string ProviderId = "llama-local";
    private const string SchemaUrl = "https://opencode.ai/config.json";

    /// <summary>Sensible max output tokens for a freshly created model entry.</summary>
    private static int DefaultOutput(int context) => Math.Clamp(context / 4, 4096, 32768);

    /// <summary>
    /// Ensures opencode.json describes <paramref name="alias"/> (creating the file,
    /// provider, and model entry if needed), sets its context, makes it the active
    /// model when <paramref name="switchActiveModel"/> is true, and applies
    /// <paramref name="features"/> when given. One read, one write.
    /// </summary>
    public Result Sync(string openCodePath, string alias, string displayName, int context,
                       string host, int port, OpenCodeFeatures? features = null,
                       bool switchActiveModel = true)
    {
        if (string.IsNullOrWhiteSpace(openCodePath))
            return new Result(false, "OpenCode path not set; skipped.");
        if (string.IsNullOrWhiteSpace(alias))
            return new Result(false, "Model has no alias; cannot configure an OpenCode model entry.");

        if (!TryLoad(openCodePath, out var rootObj, out var existed, out var error))
            return new Result(false, error);

        var note = ApplyModel(rootObj, alias, displayName, context, host, port, existed, switchActiveModel);
        if (features is not null)
            note += "; " + ApplyFeatures(rootObj, features);

        return TrySave(openCodePath, rootObj, out error)
            ? new Result(true, note + ".")
            : new Result(false, error);
    }

    /// <summary>Applies only the MCP and LSP selections, leaving model settings untouched.</summary>
    public Result ApplyFeatures(string openCodePath, OpenCodeFeatures features)
    {
        if (string.IsNullOrWhiteSpace(openCodePath))
            return new Result(false, "OpenCode path not set; skipped.");

        if (!TryLoad(openCodePath, out var rootObj, out _, out var error))
            return new Result(false, error);

        var note = ApplyFeatures(rootObj, features);

        return TrySave(openCodePath, rootObj, out error)
            ? new Result(true, note + ".")
            : new Result(false, error);
    }

    // ---- Model -------------------------------------------------------------

    private static string ApplyModel(JsonObject rootObj, string alias, string displayName, int context,
                                     string host, int port, bool fileExisted, bool switchActiveModel)
    {
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
        var activeNote = string.Empty;
        if (switchActiveModel && foundProvider is not null)
        {
            var qualified = $"{foundProvider}/{alias}";
            rootObj["model"] = qualified;
            rootObj["small_model"] = qualified;
            activeNote = $"; active model -> {qualified}";
        }

        var verb = created ? (fileExisted ? "Added" : "Created opencode.json and added") : "Updated";
        return $"{verb} \"{alias}\" (context {context}){activeNote}";
    }

    // ---- Features (MCP + LSP) ----------------------------------------------

    private static string ApplyFeatures(JsonObject rootObj, OpenCodeFeatures features)
    {
        var mcpNote = ApplyMcp(rootObj, features);
        var lspNote = ApplyLsp(rootObj, features.EnableLsp);
        return $"{mcpNote}; {lspNote}";
    }

    /// <summary>
    /// OpenCode disables every LSP server when "lsp" is absent. Enabling writes an object
    /// (keeps all built-ins on, including csharp) and clears a csharp "disabled" flag while
    /// preserving other per-server overrides. Disabling writes false.
    /// </summary>
    private static string ApplyLsp(JsonObject rootObj, bool enable)
    {
        if (!enable)
        {
            rootObj["lsp"] = false;
            return "LSP off";
        }

        if (rootObj["lsp"] is JsonObject lsp)
        {
            if (lsp["csharp"] is JsonObject csharp && csharp.ContainsKey("disabled"))
            {
                csharp.Remove("disabled");
                // An override with neither a command nor a disabled flag is not a valid
                // entry, so drop it when nothing else remains.
                if (csharp.Count == 0)
                    lsp.Remove("csharp");
            }
        }
        else
        {
            // Covers missing, false, and true: an empty object keeps every built-in on.
            rootObj["lsp"] = new JsonObject();
        }

        return "LSP on (C# via the built-in csharp server)";
    }

    private static string ApplyMcp(JsonObject rootObj, OpenCodeFeatures features)
    {
        if (rootObj["mcp"] is not JsonObject mcp)
        {
            mcp = new JsonObject();
            rootObj["mcp"] = mcp;
        }

        // Newer OpenCode nests servers under mcp.servers and uses "disabled" instead of
        // "enabled". Follow whichever layout the file already uses; default to the flat one.
        var isV2 = mcp["servers"] is JsonObject;
        var container = isV2 ? mcp["servers"]!.AsObject() : mcp;

        int added = 0, enabled = 0, disabled = 0;
        foreach (var def in features.Catalog)
        {
            var want = features.EnabledIds.Contains(def.Id);
            var existingKey = FindExisting(container, def);

            if (existingKey is null)
            {
                if (!want) continue; // don't add clutter for servers the user didn't pick
                container[def.Id] = BuildEntry(def, isV2);
                added++;
                continue;
            }

            if (container[existingKey] is not JsonObject entry) continue;

            if (isV2)
            {
                if (want) entry.Remove("disabled");
                else entry["disabled"] = true;
            }
            else
            {
                entry["enabled"] = want;
            }

            if (want) enabled++;
            else disabled++;
        }

        return $"MCP: {added} added, {enabled} enabled, {disabled} disabled";
    }

    /// <summary>Finds the entry for <paramref name="def"/> by key, or by its signature in command/url.</summary>
    private static string? FindExisting(JsonObject container, McpServerDefinition def)
    {
        if (container[def.Id] is JsonObject)
            return def.Id;

        foreach (var kv in container)
        {
            if (kv.Value is not JsonObject entry) continue;

            if (entry["url"] is JsonValue url &&
                url.TryGetValue<string>(out var u) &&
                u.Contains(def.Signature, StringComparison.OrdinalIgnoreCase))
                return kv.Key;

            if (entry["command"] is JsonArray command)
            {
                foreach (var part in command)
                {
                    if (part is JsonValue v &&
                        v.TryGetValue<string>(out var s) &&
                        s.Contains(def.Signature, StringComparison.OrdinalIgnoreCase))
                        return kv.Key;
                }
            }
        }

        return null;
    }

    /// <summary>Creates a schema-valid entry. The flat schema is strict, so only known keys are written.</summary>
    private static JsonObject BuildEntry(McpServerDefinition def, bool isV2)
    {
        var entry = new JsonObject();

        if (def.Transport == McpTransport.Local)
        {
            entry["type"] = "local";
            // Cast to JsonNode so the non-generic Add is used. JsonArray.Add<string>
            // wraps the value in a serializer-backed node that throws when written with
            // custom JsonSerializerOptions ("must specify a TypeInfoResolver").
            var command = new JsonArray();
            foreach (var part in def.Command) command.Add((JsonNode?)part);
            entry["command"] = command;

            if (def.Environment.Count > 0)
            {
                var env = new JsonObject();
                foreach (var kv in def.Environment) env[kv.Key] = kv.Value;
                entry["environment"] = env;
            }
        }
        else
        {
            entry["type"] = "remote";
            entry["url"] = def.Url;

            if (def.Headers.Count > 0)
            {
                var headers = new JsonObject();
                foreach (var kv in def.Headers) headers[kv.Key] = kv.Value;
                entry["headers"] = headers;
            }

            if (def.DisableOAuth)
                entry["oauth"] = false;
        }

        // The flat layout uses "enabled" and supports "timeout"; the V2 layout has
        // neither (servers connect unless "disabled"), so only write them for flat.
        if (!isV2)
        {
            entry["enabled"] = true;
            if (def.TimeoutMs is int timeout)
                entry["timeout"] = timeout;
        }

        return entry;
    }

    // ---- File I/O ----------------------------------------------------------

    private static bool TryLoad(string path, out JsonObject rootObj, out bool existed, out string error)
    {
        error = string.Empty;
        existed = File.Exists(path);

        if (!existed)
        {
            rootObj = new JsonObject { ["$schema"] = SchemaUrl };
            return true;
        }

        try
        {
            var text = File.ReadAllText(path);
            var docOptions = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };
            var parsed = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text, documentOptions: docOptions);
            rootObj = parsed as JsonObject ?? new JsonObject { ["$schema"] = SchemaUrl };
            return true;
        }
        catch (Exception ex)
        {
            rootObj = new JsonObject();
            error = $"Failed to parse opencode.json: {ex.Message}";
            return false;
        }
    }

    private static bool TrySave(string path, JsonObject rootObj, out string error)
    {
        error = string.Empty;
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var writeOptions = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, rootObj.ToJsonString(writeOptions) + System.Environment.NewLine);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to write opencode.json: {ex.Message}";
            return false;
        }
    }

    // ---- Provider helpers --------------------------------------------------

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

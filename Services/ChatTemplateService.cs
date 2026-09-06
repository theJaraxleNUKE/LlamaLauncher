using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace LlamaLauncher.Services;

/// <summary>
/// Reads a model's embedded Jinja chat template out of its GGUF metadata and
/// writes a patched copy with the strict message-order guards removed, so
/// tool-calling clients (Claude Code, opencode, Codex, LM Studio) stop getting
/// "Unable to generate parser for this template ... System message must be at
/// the beginning." The guards are the only thing removed; normal rendering is
/// unchanged.
/// </summary>
public sealed class ChatTemplateService
{
    private const uint GgufMagic = 0x46554747; // "GGUF" little-endian
    private const string TemplateKey = "tokenizer.chat_template";

    // Matches {{- raise_exception('System message must be at the beginning.') }}
    // and the 'No user query found ...' variant (either quote style, optional
    // whitespace-control dashes).
    private static readonly Regex RaiseGuard = new(
        @"\{\{-?\s*raise_exception\(\s*['""]" +
        @"(?:System message must be at the beginning\.|No user query found[^'""]*)" +
        @"['""]\s*\)\s*-?\}\}",
        RegexOptions.Compiled);

    public sealed record Result(bool Ok, string Message, string? OutputPath = null);

    /// <summary>
    /// Extracts the template from <paramref name="ggufPath"/>, removes the guards,
    /// and writes it to <paramref name="outputPath"/>.
    /// </summary>
    public Result PatchModelTemplate(string ggufPath, string outputPath)
    {
        if (!File.Exists(ggufPath))
            return new Result(false, $"Model file not found: {ggufPath}");

        if (!TryExtractTemplate(ggufPath, out var template, out var error))
            return new Result(false, error);

        var found = RaiseGuard.Matches(template).Count;
        var patched = RaiseGuard.Replace(template, string.Empty);

        try
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, patched);
        }
        catch (Exception ex)
        {
            return new Result(false, $"Could not write template: {ex.Message}");
        }

        return new Result(true,
            found == 0
                ? "No message-order guards found; wrote template unchanged (the 400 may have another cause)."
                : $"Removed {found} guard(s); wrote patched template.",
            outputPath);
    }

    /// <summary>Reads the chat template string from a GGUF file's metadata header.</summary>
    public bool TryExtractTemplate(string ggufPath, out string template, out string error)
    {
        template = string.Empty;
        error = string.Empty;
        try
        {
            using var fs = File.OpenRead(ggufPath);
            using var br = new BinaryReader(fs, Encoding.UTF8);

            if (br.ReadUInt32() != GgufMagic)
            {
                error = "Not a GGUF file (bad magic).";
                return false;
            }

            var version = br.ReadUInt32();
            if (version < 2)
            {
                error = $"Unsupported GGUF version {version} (need 2+).";
                return false;
            }

            _ = br.ReadUInt64();               // tensor count (unused)
            var kvCount = br.ReadUInt64();      // metadata kv count

            for (ulong i = 0; i < kvCount; i++)
            {
                var key = ReadGgufString(br);
                var valueType = br.ReadUInt32();

                if (key == TemplateKey)
                {
                    if (valueType != 8) // 8 = STRING
                    {
                        error = "chat_template metadata is not a string.";
                        return false;
                    }
                    template = ReadGgufString(br);
                    if (string.IsNullOrEmpty(template))
                    {
                        error = "Embedded chat template is empty.";
                        return false;
                    }
                    return true;
                }

                SkipValue(br, valueType);
            }

            error = "This model has no embedded chat_template.";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Could not read GGUF metadata: {ex.Message}";
            return false;
        }
    }

    private static string ReadGgufString(BinaryReader br)
    {
        var len = br.ReadUInt64();
        var bytes = br.ReadBytes(checked((int)len));
        return Encoding.UTF8.GetString(bytes);
    }

    private static void SkipValue(BinaryReader br, uint valueType)
    {
        switch (valueType)
        {
            case 0: case 1: case 7:                       // uint8 / int8 / bool
                br.BaseStream.Seek(1, SeekOrigin.Current); break;
            case 2: case 3:                               // uint16 / int16
                br.BaseStream.Seek(2, SeekOrigin.Current); break;
            case 4: case 5: case 6:                       // uint32 / int32 / float32
                br.BaseStream.Seek(4, SeekOrigin.Current); break;
            case 10: case 11: case 12:                    // uint64 / int64 / float64
                br.BaseStream.Seek(8, SeekOrigin.Current); break;
            case 8:                                        // string
                var len = br.ReadUInt64();
                br.BaseStream.Seek(checked((long)len), SeekOrigin.Current); break;
            case 9:                                        // array
                var elemType = br.ReadUInt32();
                var count = br.ReadUInt64();
                for (ulong i = 0; i < count; i++) SkipValue(br, elemType);
                break;
            default:
                throw new InvalidDataException($"Unknown GGUF value type {valueType}.");
        }
    }
}

namespace LlamaLauncher.Models;

/// <summary>
/// The full set of llama-server launch parameters for one .gguf model.
/// Serialized to / from the JSON config file. Defaults mirror a sane
/// starting point for a 16 GB card; the user tunes per model.
/// </summary>
public sealed class ModelConfig
{
    // ---- Identity ----------------------------------------------------------
    /// <summary>Unique display name for this profile — the dictionary key in AppConfig.
    /// Multiple profiles may target the same .gguf file with different settings.</summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>The .gguf file this profile launches.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>False for a model just discovered on disk that has not been set up yet.</summary>
    public bool IsConfigured { get; set; }

    // ---- Core --------------------------------------------------------------
    public string Alias { get; set; } = string.Empty;
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8080;
    public int GpuLayers { get; set; } = 99;          // -ngl
    public bool FlashAttention { get; set; } = true;  // -fa 1
    public int ContextSize { get; set; } = 131072;    // -c

    // ---- Speculative decoding ---------------------------------------------
    public bool SpeculativeEnabled { get; set; } = true;
    public string SpecType { get; set; } = "draft-mtp";
    public int SpecDraftNMax { get; set; } = 2;

    // ---- KV cache ----------------------------------------------------------
    public string CacheTypeK { get; set; } = "q4_0";
    public string CacheTypeV { get; set; } = "q4_0";
    public bool KvUnified { get; set; } = true;

    // ---- Performance / threading ------------------------------------------
    public int Parallel { get; set; } = 1;            // -np
    public int BatchSize { get; set; } = 512;         // -b
    public int UBatchSize { get; set; } = 256;        // -ub
    public int Threads { get; set; } = 12;            // -t
    public int ThreadsBatch { get; set; } = 12;       // -tb
    public int ThreadsHttp { get; set; } = 8;         // --threads-http

    // ---- MoE offload / runtime ---------------------------------------------
    /// <summary>Expert (MoE) layers to keep on CPU (--n-cpu-moe). 0 = omit. MoE models only.</summary>
    public int NCpuMoe { get; set; }                  // --n-cpu-moe
    /// <summary>Prompt-cache reuse window in tokens (--cache-reuse). 0 = omit.</summary>
    public int CacheReuse { get; set; }               // --cache-reuse

    // ---- Boolean flags -----------------------------------------------------
    public bool NoMmap { get; set; } = true;          // --no-mmap
    public bool Mlock { get; set; } = true;           // --mlock
    public bool Jinja { get; set; } = true;           // --jinja
    public bool Perf { get; set; } = true;            // --perf

    /// <summary>JSON passed to --chat-template-kwargs, e.g. {"reasoning_effort":"high"}. Empty = omit.</summary>
    public string ChatTemplateKwargs { get; set; } = string.Empty;

    /// <summary>
    /// Path to a Jinja chat template that overrides the model's embedded one
    /// (--chat-template-file). Used to supply a template with the strict
    /// message-order guards removed so tool-calling clients don't 400. Empty = omit.
    /// </summary>
    public string ChatTemplateFile { get; set; } = string.Empty;

    // ---- Sampling ----------------------------------------------------------
    /// <summary>When false, no --temp/--top-p/--top-k/--min-p/--presence-penalty flags are emitted.</summary>
    public bool SamplingEnabled { get; set; } = true;
    public double Temperature { get; set; } = 1.0;
    public double TopP { get; set; } = 0.95;
    public int TopK { get; set; } = 20;
    public double MinP { get; set; }                  // 0.0
    public double PresencePenalty { get; set; }       // 0.0

    /// <summary>Any additional flags appended verbatim to the command line.</summary>
    public string ExtraArgs { get; set; } = string.Empty;

    /// <summary>All fields are value types or immutable strings, so a shallow clone is a full copy.</summary>
    public ModelConfig Clone() => (ModelConfig)MemberwiseClone();
}

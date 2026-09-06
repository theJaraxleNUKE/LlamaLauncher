using System.Runtime.CompilerServices;
using LlamaLauncher.Models;
using LlamaLauncher.Mvvm;

namespace LlamaLauncher.ViewModels;

/// <summary>
/// Editable surface over a <see cref="ModelConfig"/>. Every field edit flips
/// <see cref="IsDirty"/> so the UI can enable Save/Cancel and gate Load.
/// </summary>
public sealed class ModelConfigViewModel : ObservableObject
{
    public ModelConfigViewModel(ModelConfig source)
    {
        FileName = source.FileName;
        LoadFrom(source);
        IsDirty = false; // LoadFrom set fields directly; start clean
    }

    public string FileName { get; }

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        set => SetProperty(ref _isDirty, value);
    }

    private bool _isConfigured;
    public bool IsConfigured
    {
        get => _isConfigured;
        set => SetProperty(ref _isConfigured, value);
    }

    private string _profileName = string.Empty;
    public string ProfileName { get => _profileName; set => SetEdit(ref _profileName, value); }

    // ---- Backing fields ----------------------------------------------------
    private string _alias = string.Empty;
    private string _host = "0.0.0.0";
    private int _port = 8080;
    private int _gpuLayers = 99;
    private bool _flashAttention = true;
    private int _contextSize = 131072;

    private bool _speculativeEnabled = true;
    private string _specType = "draft-mtp";
    private int _specDraftNMax = 2;

    private string _cacheTypeK = "q4_0";
    private string _cacheTypeV = "q4_0";
    private bool _kvUnified = true;

    private int _parallel = 1;
    private int _batchSize = 512;
    private int _uBatchSize = 256;
    private int _threads = 12;
    private int _threadsBatch = 12;
    private int _threadsHttp = 8;

    private int _nCpuMoe;
    private int _cacheReuse;
    private string _chatTemplateKwargs = string.Empty;
    private string _chatTemplateFile = string.Empty;

    private bool _noMmap = true;
    private bool _mlock = true;
    private bool _jinja = true;
    private bool _perf = true;

    private double _temperature = 1.0;
    private double _topP = 0.95;
    private int _topK = 20;
    private double _minP;
    private double _presencePenalty;
    private bool _samplingEnabled = true;

    private string _extraArgs = string.Empty;

    // ---- Public properties (edits mark dirty) ------------------------------
    public string Alias { get => _alias; set => SetEdit(ref _alias, value); }
    public string Host { get => _host; set => SetEdit(ref _host, value); }
    public int Port { get => _port; set => SetEdit(ref _port, value); }
    public int GpuLayers { get => _gpuLayers; set => SetEdit(ref _gpuLayers, value); }
    public bool FlashAttention { get => _flashAttention; set => SetEdit(ref _flashAttention, value); }
    public int ContextSize { get => _contextSize; set => SetEdit(ref _contextSize, value); }

    public bool SpeculativeEnabled { get => _speculativeEnabled; set => SetEdit(ref _speculativeEnabled, value); }
    public string SpecType { get => _specType; set => SetEdit(ref _specType, value); }
    public int SpecDraftNMax { get => _specDraftNMax; set => SetEdit(ref _specDraftNMax, value); }

    public string CacheTypeK { get => _cacheTypeK; set => SetEdit(ref _cacheTypeK, value); }
    public string CacheTypeV { get => _cacheTypeV; set => SetEdit(ref _cacheTypeV, value); }
    public bool KvUnified { get => _kvUnified; set => SetEdit(ref _kvUnified, value); }

    public int Parallel { get => _parallel; set => SetEdit(ref _parallel, value); }
    public int BatchSize { get => _batchSize; set => SetEdit(ref _batchSize, value); }
    public int UBatchSize { get => _uBatchSize; set => SetEdit(ref _uBatchSize, value); }
    public int Threads { get => _threads; set => SetEdit(ref _threads, value); }
    public int ThreadsBatch { get => _threadsBatch; set => SetEdit(ref _threadsBatch, value); }
    public int ThreadsHttp { get => _threadsHttp; set => SetEdit(ref _threadsHttp, value); }

    public int NCpuMoe { get => _nCpuMoe; set => SetEdit(ref _nCpuMoe, value); }
    public int CacheReuse { get => _cacheReuse; set => SetEdit(ref _cacheReuse, value); }
    public string ChatTemplateKwargs { get => _chatTemplateKwargs; set => SetEdit(ref _chatTemplateKwargs, value); }
    public string ChatTemplateFile { get => _chatTemplateFile; set => SetEdit(ref _chatTemplateFile, value); }

    public bool NoMmap { get => _noMmap; set => SetEdit(ref _noMmap, value); }
    public bool Mlock { get => _mlock; set => SetEdit(ref _mlock, value); }
    public bool Jinja { get => _jinja; set => SetEdit(ref _jinja, value); }
    public bool Perf { get => _perf; set => SetEdit(ref _perf, value); }

    public bool SamplingEnabled { get => _samplingEnabled; set => SetEdit(ref _samplingEnabled, value); }
    public double Temperature { get => _temperature; set => SetEdit(ref _temperature, value); }
    public double TopP { get => _topP; set => SetEdit(ref _topP, value); }
    public int TopK { get => _topK; set => SetEdit(ref _topK, value); }
    public double MinP { get => _minP; set => SetEdit(ref _minP, value); }
    public double PresencePenalty { get => _presencePenalty; set => SetEdit(ref _presencePenalty, value); }

    public string ExtraArgs { get => _extraArgs; set => SetEdit(ref _extraArgs, value); }

    // ---- Load / commit -----------------------------------------------------
    public void LoadFrom(ModelConfig c)
    {
        IsConfigured = c.IsConfigured;
        ProfileName = c.ProfileName;
        Alias = c.Alias;
        Host = c.Host;
        Port = c.Port;
        GpuLayers = c.GpuLayers;
        FlashAttention = c.FlashAttention;
        ContextSize = c.ContextSize;

        SpeculativeEnabled = c.SpeculativeEnabled;
        SpecType = c.SpecType;
        SpecDraftNMax = c.SpecDraftNMax;

        CacheTypeK = c.CacheTypeK;
        CacheTypeV = c.CacheTypeV;
        KvUnified = c.KvUnified;

        Parallel = c.Parallel;
        BatchSize = c.BatchSize;
        UBatchSize = c.UBatchSize;
        Threads = c.Threads;
        ThreadsBatch = c.ThreadsBatch;
        ThreadsHttp = c.ThreadsHttp;

        NCpuMoe = c.NCpuMoe;
        CacheReuse = c.CacheReuse;
        ChatTemplateKwargs = c.ChatTemplateKwargs;
        ChatTemplateFile = c.ChatTemplateFile;

        NoMmap = c.NoMmap;
        Mlock = c.Mlock;
        Jinja = c.Jinja;
        Perf = c.Perf;

        SamplingEnabled = c.SamplingEnabled;
        Temperature = c.Temperature;
        TopP = c.TopP;
        TopK = c.TopK;
        MinP = c.MinP;
        PresencePenalty = c.PresencePenalty;

        ExtraArgs = c.ExtraArgs;

        IsDirty = false;
    }

    public ModelConfig ToModel() => new()
    {
        FileName = FileName,
        ProfileName = ProfileName,
        IsConfigured = IsConfigured,
        Alias = Alias,
        Host = Host,
        Port = Port,
        GpuLayers = GpuLayers,
        FlashAttention = FlashAttention,
        ContextSize = ContextSize,
        SpeculativeEnabled = SpeculativeEnabled,
        SpecType = SpecType,
        SpecDraftNMax = SpecDraftNMax,
        CacheTypeK = CacheTypeK,
        CacheTypeV = CacheTypeV,
        KvUnified = KvUnified,
        Parallel = Parallel,
        BatchSize = BatchSize,
        UBatchSize = UBatchSize,
        Threads = Threads,
        ThreadsBatch = ThreadsBatch,
        ThreadsHttp = ThreadsHttp,
        NCpuMoe = NCpuMoe,
        CacheReuse = CacheReuse,
        ChatTemplateKwargs = ChatTemplateKwargs,
        ChatTemplateFile = ChatTemplateFile,
        NoMmap = NoMmap,
        Mlock = Mlock,
        Jinja = Jinja,
        Perf = Perf,
        SamplingEnabled = SamplingEnabled,
        Temperature = Temperature,
        TopP = TopP,
        TopK = TopK,
        MinP = MinP,
        PresencePenalty = PresencePenalty,
        ExtraArgs = ExtraArgs
    };

    /// <summary>Set a field and, if it changed, flag the VM dirty.</summary>
    private void SetEdit<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (SetProperty(ref field, value, name))
            IsDirty = true;
    }
}

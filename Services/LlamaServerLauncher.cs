using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using LlamaLauncher.Models;

namespace LlamaLauncher.Services;

/// <summary>
/// Turns a <see cref="ModelConfig"/> into a llama-server command line,
/// starts the process, streams its stdout/stderr, and stops it on demand.
/// Only one server is kept alive at a time.
/// </summary>
public sealed class LlamaServerLauncher
{
    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

    private Process? _process;

    public bool IsRunning => _process is { HasExited: false };

    /// <summary>Raised for every line of server output (already off the process thread pool).</summary>
    public event Action<string>? OutputReceived;

    /// <summary>Raised when the server process exits, with its exit code.</summary>
    public event Action<int>? Exited;

    public string BuildArguments(ModelConfig cfg, string modelsFolder)
    {
        var modelPath = Path.Combine(modelsFolder, cfg.FileName);
        var sb = new StringBuilder();

        void Add(string token)
        {
            sb.Append(token);
            sb.Append(' ');
        }

        Add($"--model \"{modelPath}\"");
        Add($"-ngl {cfg.GpuLayers}");
        if (cfg.NCpuMoe > 0) Add($"--n-cpu-moe {cfg.NCpuMoe.ToString(Ci)}");
        if (cfg.FlashAttention) Add("-fa on");
        Add($"-c {cfg.ContextSize}");
        if (!string.IsNullOrWhiteSpace(cfg.Alias)) Add($"--alias {cfg.Alias}");
        Add($"--host {cfg.Host}");
        Add($"--port {cfg.Port.ToString(Ci)}");

        if (cfg.SpeculativeEnabled && !string.IsNullOrWhiteSpace(cfg.SpecType))
        {
            Add($"--spec-type {cfg.SpecType}");
            Add($"--spec-draft-n-max {cfg.SpecDraftNMax.ToString(Ci)}");
        }

        if (!string.IsNullOrWhiteSpace(cfg.CacheTypeK)) Add($"--cache-type-k {cfg.CacheTypeK}");
        if (!string.IsNullOrWhiteSpace(cfg.CacheTypeV)) Add($"--cache-type-v {cfg.CacheTypeV}");
        if (cfg.KvUnified) Add("--kv-unified");

        // Integer perf flags: 0 means "omit — use llama-server's own default".
        if (cfg.Parallel > 0) Add($"-np {cfg.Parallel.ToString(Ci)}");
        if (cfg.BatchSize > 0) Add($"-b {cfg.BatchSize.ToString(Ci)}");
        if (cfg.UBatchSize > 0) Add($"-ub {cfg.UBatchSize.ToString(Ci)}");
        if (cfg.Threads > 0) Add($"-t {cfg.Threads.ToString(Ci)}");
        if (cfg.ThreadsBatch > 0) Add($"-tb {cfg.ThreadsBatch.ToString(Ci)}");
        if (cfg.ThreadsHttp > 0) Add($"--threads-http {cfg.ThreadsHttp.ToString(Ci)}");

        if (cfg.NoMmap) Add("--no-mmap");
        if (cfg.Mlock) Add("--mlock");
        if (cfg.Jinja) Add("--jinja");
        if (!string.IsNullOrWhiteSpace(cfg.ChatTemplateFile))
            Add($"--chat-template-file {QuoteArg(cfg.ChatTemplateFile.Trim())}");
        if (cfg.CacheReuse > 0) Add($"--cache-reuse {cfg.CacheReuse.ToString(Ci)}");
        if (!string.IsNullOrWhiteSpace(cfg.ChatTemplateKwargs))
            Add($"--chat-template-kwargs {QuoteArg(cfg.ChatTemplateKwargs.Trim())}");
        if (cfg.Perf) Add("--perf");

        if (cfg.SamplingEnabled)
        {
            Add($"--temp {cfg.Temperature.ToString(Ci)}");
            Add($"--top-p {cfg.TopP.ToString(Ci)}");
            Add($"--top-k {cfg.TopK.ToString(Ci)}");
            Add($"--min-p {cfg.MinP.ToString(Ci)}");
            Add($"--presence-penalty {cfg.PresencePenalty.ToString(Ci)}");
        }

        if (!string.IsNullOrWhiteSpace(cfg.ExtraArgs)) Add(cfg.ExtraArgs.Trim());

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Quotes a single argument value using the Win32 CommandLineToArgvW rules that
    /// .NET also applies to <see cref="ProcessStartInfo.Arguments"/> on all platforms.
    /// Needed for values with spaces or embedded quotes, e.g. a JSON
    /// --chat-template-kwargs payload like {"reasoning_effort":"high"}.
    /// </summary>
    private static string QuoteArg(string value)
    {
        if (value.Length > 0 && value.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
            return value;

        var sb = new StringBuilder();
        sb.Append('"');
        var backslashes = 0;
        foreach (var c in value)
        {
            if (c == '\\')
            {
                backslashes++;
            }
            else if (c == '"')
            {
                sb.Append('\\', backslashes * 2 + 1);
                sb.Append('"');
                backslashes = 0;
            }
            else
            {
                if (backslashes > 0) { sb.Append('\\', backslashes); backslashes = 0; }
                sb.Append(c);
            }
        }
        sb.Append('\\', backslashes * 2);
        sb.Append('"');
        return sb.ToString();
    }

    public void Start(string serverExePath, ModelConfig cfg, string modelsFolder)
    {
        Stop(); // guarantee a single running server

        if (!File.Exists(serverExePath))
            throw new FileNotFoundException($"llama-server.exe not found at: {serverExePath}");

        var args = BuildArguments(cfg, modelsFolder);

        var psi = new ProcessStartInfo
        {
            FileName = serverExePath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(serverExePath) ?? Environment.CurrentDirectory
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) OutputReceived?.Invoke(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) OutputReceived?.Invoke(e.Data); };
        process.Exited += (_, _) => Exited?.Invoke(TryGetExitCode(process));

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _process = process;

        OutputReceived?.Invoke($"> \"{serverExePath}\" {args}");
    }

    public void Stop()
    {
        if (_process is null) return;

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
        }
        catch
        {
            // Nothing actionable if the kill races the process ending on its own.
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    private static int TryGetExitCode(Process p)
    {
        try { return p.ExitCode; }
        catch { return -1; }
    }
}

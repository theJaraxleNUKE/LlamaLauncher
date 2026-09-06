using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Avalonia.Threading;
using LlamaLauncher.Models;
using LlamaLauncher.Mvvm;
using LlamaLauncher.Services;

namespace LlamaLauncher.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ConfigService _configService = new();
    private readonly ModelScanner _scanner = new();
    private readonly LlamaServerLauncher _launcher = new();
    private readonly OpenCodeSyncService _openCode = new();
    private readonly PresetIniService _presetIni = new();
    private readonly ChatTemplateService _chatTemplate = new();

    private AppConfig _config = new();
    private HashSet<string> _onDisk = new(StringComparer.OrdinalIgnoreCase);

    public MainViewModel()
    {
        SaveCommand = new RelayCommand(_ => Save(), _ => Editor is not null);
        CancelCommand = new RelayCommand(_ => Cancel(), _ => Editor is { IsDirty: true });
        LoadCommand = new RelayCommand(_ => LoadModel(), _ => CanLoad());
        StopCommand = new RelayCommand(_ => StopServer(), _ => _launcher.IsRunning);
        RescanCommand = new RelayCommand(_ => RescanAndSavePaths());
        DuplicateCommand = new RelayCommand(_ => DuplicateProfile(), _ => _selectedModel is not null);
        DeleteProfileCommand = new RelayCommand(_ => DeleteProfile(), _ => _selectedModel is not null);
        ClearLogCommand = new RelayCommand(_ => { _logText = string.Empty; OnPropertyChanged(nameof(LogText)); });
        WriteIniCommand = new RelayCommand(_ => WriteIni(announce: true));
        FixTemplateCommand = new RelayCommand(_ => FixTemplate(),
            _ => Editor is not null && !string.IsNullOrWhiteSpace(Editor.FileName));

        _launcher.OutputReceived += line => Dispatch(() => AppendLog(line));
        _launcher.Exited += code => Dispatch(() =>
        {
            ServerStatus = $"Exited (code {code})";
            AppendLog($"[server exited with code {code}]");
            RaiseCommands();
        });
    }

    // ---- Commands ----------------------------------------------------------
    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand LoadCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand RescanCommand { get; }
    public RelayCommand DuplicateCommand { get; }
    public RelayCommand DeleteProfileCommand { get; }
    public RelayCommand ClearLogCommand { get; }
    public RelayCommand WriteIniCommand { get; }
    public RelayCommand FixTemplateCommand { get; }

    // ---- Bound state -------------------------------------------------------
    public ObservableCollection<ModelListItem> Models { get; } = new();

    private ModelListItem? _selectedModel;
    public ModelListItem? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (SetProperty(ref _selectedModel, value))
            {
                BuildEditor();
                RaiseCommands();
            }
        }
    }

    private ModelConfigViewModel? _editor;
    public ModelConfigViewModel? Editor
    {
        get => _editor;
        private set => SetProperty(ref _editor, value);
    }

    private string _serverPath = @"C:\llama\llama-server.exe";
    public string ServerPath { get => _serverPath; set => SetProperty(ref _serverPath, value); }

    private string _modelsFolder = @"C:\llama\models";
    public string ModelsFolder { get => _modelsFolder; set => SetProperty(ref _modelsFolder, value); }

    private string _openCodePath = string.Empty;
    public string OpenCodePath { get => _openCodePath; set => SetProperty(ref _openCodePath, value); }

    private bool _syncOpenCode = true;
    public bool SyncOpenCode { get => _syncOpenCode; set => SetProperty(ref _syncOpenCode, value); }

    private string _presetIniPath = string.Empty;
    public string PresetIniPath { get => _presetIniPath; set => SetProperty(ref _presetIniPath, value); }

    private bool _writePresetIni = true;
    public bool WritePresetIni { get => _writePresetIni; set => SetProperty(ref _writePresetIni, value); }

    private string _serverStatus = "Idle";
    public string ServerStatus { get => _serverStatus; set => SetProperty(ref _serverStatus, value); }

    private string _logText = string.Empty;
    public string LogText => _logText;

    // ---- Lifecycle ---------------------------------------------------------
    public void Initialize()
    {
        _config = _configService.Load();
        ServerPath = _config.LlamaServerPath;
        ModelsFolder = _config.ModelsFolder;
        OpenCodePath = _config.OpenCodePath;
        SyncOpenCode = _config.SyncOpenCodeOnLoad;
        PresetIniPath = _config.PresetIniPath;
        WritePresetIni = _config.WritePresetIni;

        ScanAndMerge();
        AppendLog($"Config: {_configService.ConfigPath}");
        AppendLog($"Loaded {Models.Count} profile(s); models folder: {ModelsFolder}.");
        WriteIni(announce: true);
    }

    public void Shutdown()
    {
        PersistSettings();
        _launcher.Stop();
    }

    private void PersistSettings()
    {
        _config.LlamaServerPath = ServerPath;
        _config.ModelsFolder = ModelsFolder;
        _config.OpenCodePath = OpenCodePath;
        _config.SyncOpenCodeOnLoad = SyncOpenCode;
        _config.PresetIniPath = PresetIniPath;
        _config.WritePresetIni = WritePresetIni;
        SaveConfig();
    }

    /// <summary>Persists the JSON config and, when enabled, rewrites the native llama-server INI preset.</summary>
    private void SaveConfig()
    {
        _configService.Save(_config);
        WriteIni(announce: false);
    }

    private string ResolveIniPath()
        => string.IsNullOrWhiteSpace(PresetIniPath)
            ? Path.Combine(_configService.DataDirectory, "config.ini")
            : PresetIniPath.Trim();

    private void WriteIni(bool announce)
    {
        if (!WritePresetIni) return;

        var path = ResolveIniPath();
        try
        {
            var count = _presetIni.Write(_config, path);
            if (announce)
                AppendLog($"Wrote llama-server preset ({count} model(s)): {path}");
        }
        catch (Exception ex)
        {
            AppendLog($"Could not write llama-server preset: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts the selected model's embedded chat template, strips the strict
    /// message-order guards that break tool-calling, writes the patched template
    /// into a "templates" folder, and points the profile at it.
    /// </summary>
    private void FixTemplate()
    {
        if (Editor is null || string.IsNullOrWhiteSpace(Editor.FileName))
            return;

        var ggufPath = Path.Combine(ModelsFolder, Editor.FileName);
        var slug = MakeTemplateSlug(Editor.ProfileName, Editor.FileName);
        var outputPath = Path.Combine(_configService.DataDirectory, "templates", slug + ".jinja");

        var result = _chatTemplate.PatchModelTemplate(ggufPath, outputPath);
        if (result.Ok)
        {
            Editor.ChatTemplateFile = result.OutputPath!;
            AppendLog($"Chat template: {result.Message} -> {result.OutputPath}");
            AppendLog("Save the profile to keep this override.");
        }
        else
        {
            AppendLog($"Chat template fix failed: {result.Message}");
        }
    }

    private static string MakeTemplateSlug(string profileName, string fileName)
    {
        var basis = !string.IsNullOrWhiteSpace(profileName)
            ? profileName
            : Path.GetFileNameWithoutExtension(fileName);
        var chars = basis.Select(c => char.IsLetterOrDigit(c) || c is '-' or '.' ? c : '-').ToArray();
        var slug = new string(chars).Trim('-', '.');
        return string.IsNullOrEmpty(slug) ? "template" : slug;
    }


    // ---- Core logic --------------------------------------------------------
    private void ScanAndMerge()
    {
        var files = _scanner.Scan(_config.ModelsFolder);
        _onDisk = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);

        // Which files already have at least one profile pointing at them?
        var referenced = new HashSet<string>(
            _config.Models.Values.Select(c => c.FileName),
            StringComparer.OrdinalIgnoreCase);

        var added = false;
        foreach (var file in files)
        {
            if (!referenced.Contains(file))
            {
                var name = MakeUniqueKey(file);
                _config.Models[name] = new ModelConfig
                {
                    ProfileName = name,
                    FileName = file,
                    Alias = SuggestAlias(file),
                    IsConfigured = false
                };
                referenced.Add(file);
                added = true;
            }
        }

        if (added) SaveConfig();

        RebuildList();
    }

    private void RebuildList(string? selectProfile = null)
    {
        var target = selectProfile ?? _selectedModel?.ProfileName;

        Models.Clear();
        foreach (var cfg in _config.Models.Values
                     .OrderBy(c => c.FileName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(c => c.ProfileName, StringComparer.OrdinalIgnoreCase))
        {
            Models.Add(new ModelListItem(
                cfg.ProfileName,
                cfg.FileName,
                cfg.IsConfigured,
                fileMissing: !_onDisk.Contains(cfg.FileName)));
        }

        if (target is not null)
            SelectedModel = Models.FirstOrDefault(m => m.ProfileName == target);
    }

    private void BuildEditor()
    {
        if (_selectedModel is not null &&
            _config.Models.TryGetValue(_selectedModel.ProfileName, out var cfg))
        {
            var editor = new ModelConfigViewModel(cfg.Clone());
            editor.PropertyChanged += OnEditorPropertyChanged;
            Editor = editor;
        }
        else
        {
            Editor = null;
        }
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ModelConfigViewModel.IsDirty)
            or nameof(ModelConfigViewModel.IsConfigured))
        {
            RaiseCommands();
        }
    }

    private void Save()
    {
        if (Editor is null || _selectedModel is null) return;

        var oldKey = _selectedModel.ProfileName;
        var model = Editor.ToModel();
        model.IsConfigured = true;

        // Resolve the (possibly renamed) profile key, keeping it unique.
        var newKey = string.IsNullOrWhiteSpace(model.ProfileName)
            ? model.FileName
            : model.ProfileName.Trim();
        if (!string.Equals(newKey, oldKey, StringComparison.Ordinal))
            newKey = MakeUniqueKey(newKey, exclude: oldKey);
        model.ProfileName = newKey;

        if (!string.Equals(newKey, oldKey, StringComparison.Ordinal))
            _config.Models.Remove(oldKey);
        _config.Models[newKey] = model;
        SaveConfig();

        RebuildList(selectProfile: newKey);
        AppendLog($"Saved profile \"{newKey}\".");
        RaiseCommands();
    }

    private void Cancel()
    {
        BuildEditor(); // discard edits by reloading from the stored config
        RaiseCommands();
    }

    private bool CanLoad()
        => Editor is { IsConfigured: true, IsDirty: false }
           && _selectedModel is { FileMissing: false };

    private void LoadModel()
    {
        if (Editor is null || _selectedModel is null) return;

        if (!Editor.IsConfigured || Editor.IsDirty)
        {
            AppendLog("Save the profile before loading it.");
            return;
        }

        if (!_config.Models.TryGetValue(_selectedModel.ProfileName, out var cfg))
            return;

        PersistSettings();

        try
        {
            ServerStatus = $"Starting {cfg.ProfileName}...";
            AppendLog($"Launching \"{cfg.ProfileName}\" ({cfg.FileName}) ...");
            _launcher.Start(ServerPath, cfg, ModelsFolder);
            ServerStatus = $"Running: {cfg.ProfileName}";

            if (SyncOpenCode && !string.IsNullOrWhiteSpace(OpenCodePath))
            {
                var result = _openCode.UpdateContext(OpenCodePath, cfg.Alias, cfg.ContextSize);
                AppendLog("OpenCode: " + result.Message);
            }
        }
        catch (Exception ex)
        {
            ServerStatus = "Error";
            AppendLog($"Failed to start server: {ex.Message}");
        }

        RaiseCommands();
    }

    private void StopServer()
    {
        _launcher.Stop();
        ServerStatus = "Stopped";
        AppendLog("[server stopped]");
        RaiseCommands();
    }

    private void DuplicateProfile()
    {
        if (_selectedModel is null) return;
        if (!_config.Models.TryGetValue(_selectedModel.ProfileName, out var src)) return;

        var copy = src.Clone();
        copy.ProfileName = MakeUniqueKey($"{src.ProfileName} (copy)");
        _config.Models[copy.ProfileName] = copy;
        SaveConfig();

        RebuildList(selectProfile: copy.ProfileName);
        AppendLog($"Duplicated profile as \"{copy.ProfileName}\".");
        RaiseCommands();
    }

    private void DeleteProfile()
    {
        if (_selectedModel is null) return;

        var key = _selectedModel.ProfileName;
        if (_config.Models.Remove(key))
        {
            SaveConfig();
            // If the file is still on disk with no remaining profile, scan re-adds a NEW one.
            ScanAndMerge();
            AppendLog($"Deleted profile \"{key}\".");
        }
        RaiseCommands();
    }

    private void RescanAndSavePaths()
    {
        PersistSettings();
        ScanAndMerge();
        AppendLog($"Rescanned. {Models.Count} profile(s).");
    }

    private string MakeUniqueKey(string baseName, string? exclude = null)
    {
        baseName = string.IsNullOrWhiteSpace(baseName) ? "profile" : baseName.Trim();
        if (!_config.Models.ContainsKey(baseName)
            || string.Equals(baseName, exclude, StringComparison.Ordinal))
            return baseName;

        for (var i = 2; ; i++)
        {
            var candidate = $"{baseName} ({i})";
            if (!_config.Models.ContainsKey(candidate)) return candidate;
        }
    }

    private static string SuggestAlias(string fileName)
        => Path.GetFileNameWithoutExtension(fileName);

    private void RaiseCommands()
    {
        SaveCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        LoadCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        DuplicateCommand.RaiseCanExecuteChanged();
        DeleteProfileCommand.RaiseCanExecuteChanged();
        FixTemplateCommand.RaiseCanExecuteChanged();
    }

    private void AppendLog(string line)
    {
        _logText = _logText.Length == 0 ? line : _logText + "\n" + line;
        OnPropertyChanged(nameof(LogText));
    }

    private static void Dispatch(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }
}

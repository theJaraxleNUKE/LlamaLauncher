using LlamaLauncher.Mvvm;

namespace LlamaLauncher.ViewModels;

/// <summary>Row shown in the profile list on the left.</summary>
public sealed class ModelListItem : ObservableObject
{
    private bool _isConfigured;
    private bool _fileMissing;

    public ModelListItem(string profileName, string fileName, bool isConfigured, bool fileMissing = false)
    {
        ProfileName = profileName;
        FileName = fileName;
        _isConfigured = isConfigured;
        _fileMissing = fileMissing;
    }

    /// <summary>The profile's unique name (dictionary key).</summary>
    public string ProfileName { get; }

    /// <summary>The .gguf file this profile launches.</summary>
    public string FileName { get; }

    public bool IsConfigured
    {
        get => _isConfigured;
        set => SetProperty(ref _isConfigured, value);
    }

    /// <summary>True when the profile points at a file no longer on disk.</summary>
    public bool FileMissing
    {
        get => _fileMissing;
        set => SetProperty(ref _fileMissing, value);
    }
}

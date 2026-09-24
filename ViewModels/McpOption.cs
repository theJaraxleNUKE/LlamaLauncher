using LlamaLauncher.Models;
using LlamaLauncher.Mvvm;

namespace LlamaLauncher.ViewModels;

/// <summary>One checkbox in the OpenCode agent-tools list.</summary>
public sealed class McpOption : ObservableObject
{
    private bool _isEnabled;

    public McpOption(McpServerDefinition definition, bool isEnabled)
    {
        Definition = definition;
        _isEnabled = isEnabled;
    }

    public McpServerDefinition Definition { get; }

    public string Id => Definition.Id;

    public string DisplayName => Definition.DisplayName;

    /// <summary>Hover text: what it does, what it needs, and how it connects.</summary>
    public string Tooltip =>
        Definition.Description + System.Environment.NewLine + System.Environment.NewLine +
        "Requires: " + Definition.Requirement + System.Environment.NewLine +
        (Definition.Transport == McpTransport.Remote
            ? "Remote: " + Definition.Url
            : "Command: " + string.Join(' ', Definition.Command));

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}

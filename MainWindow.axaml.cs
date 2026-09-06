using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LlamaLauncher.ViewModels;

namespace LlamaLauncher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private ScrollViewer? _logScroller;

    public MainWindow()
    {
        InitializeComponent();

        _logScroller = this.FindControl<ScrollViewer>("LogScroller");
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.Initialize();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.LogText))
            Dispatcher.UIThread.Post(ScrollLogToEnd, DispatcherPriority.Background);
    }

    private void ScrollLogToEnd()
    {
        if (_logScroller is null) return;
        var maxY = Math.Max(0, _logScroller.Extent.Height - _logScroller.Viewport.Height);
        _logScroller.Offset = new Vector(_logScroller.Offset.X, maxY);
    }

    private async void BrowseOpenCode_Click(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select opencode.json",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("OpenCode config") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } }
            }
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
            _viewModel.OpenCodePath = path;
    }

    private async void BrowseIni_Click(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Choose where to write the llama-server config.ini",
            SuggestedFileName = "config.ini",
            DefaultExtension = "ini",
            ShowOverwritePrompt = false,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("INI preset") { Patterns = new[] { "*.ini" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } }
            }
        });

        if (file?.TryGetLocalPath() is { } path)
            _viewModel.PresetIniPath = path;
    }

    private async void BrowseTemplate_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.Editor is null) return;

        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a chat template (.jinja)",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Jinja template") { Patterns = new[] { "*.jinja", "*.j2", "*.txt" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } }
            }
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path && _viewModel.Editor is not null)
            _viewModel.Editor.ChatTemplateFile = path;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Shutdown();
        base.OnClosed(e);
    }
}

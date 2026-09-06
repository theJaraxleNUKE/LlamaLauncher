# Contributing

Thanks for your interest in improving Llama Launcher.

## Getting set up

- Install the **.NET 8 SDK** (any OS).
- Clone the repo, then:

  ```bash
  cd LlamaLauncher
  dotnet restore
  dotnet run
  ```

The UI is [Avalonia](https://avaloniaui.net/) (`net8.0`), so it builds and runs
on Linux, Windows, and macOS from the same project.

## Project layout

| Path | What |
|------|------|
| `Program.cs`, `App.axaml(.cs)` | Avalonia bootstrap + theme |
| `MainWindow.axaml(.cs)` | Main window UI + code-behind (file pickers, log auto-scroll) |
| `ViewModels/` | MVVM view models (`MainViewModel` orchestrates everything) |
| `Models/` | Plain config POCOs serialized to JSON |
| `Services/` | Scanner, launcher, JSON config, OpenCode sync, **preset INI writer** |
| `Mvvm/` | `ObservableObject` + `RelayCommand` (framework-agnostic) |
| `packaging/`, `installer/` | Linux/Windows packaging inputs |

## Guidelines

- Keep the launcher (`LlamaServerLauncher.BuildArguments`) and the preset writer
  (`PresetIniService`) in sync: a profile must produce the same effective
  configuration whether it's launched through the app or via
  `llama-server --models-preset`. If you add a field, wire it into **both**.
- Prefer small, focused PRs. Describe the change and how you tested it.
- Match the existing C# style (nullable enabled, file-scoped namespaces).
- No secrets, machine-specific paths, or `bin/`/`obj/` in commits.

## Reporting issues

Open a GitHub issue with your OS, .NET version, llama.cpp build/date, and the
relevant log output from the app's server-output panel.

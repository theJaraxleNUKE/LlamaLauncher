# Llama Launcher

An **Avalonia (.NET 8)** desktop app for managing per-model `llama-server` launch
configurations. It scans your models folder, stores a JSON config of named
profiles, lets you view/edit/save/load those settings, starts `llama-server`
with the right flags for whichever profile you pick, and mirrors every profile
into a **native llama.cpp `config.ini` preset** so you can also launch straight
from `llama-server` without the app.

Avalonia replaces the original WPF UI, so the app runs on **Linux, Windows and
macOS** from a single `net8.0` build.

![CI](https://github.com/theJaraxleNUKE/LlamaLauncher/actions/workflows/ci.yml/badge.svg)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)

## Download & install

Grab the latest build from the [Releases](https://github.com/theJaraxleNUKE/LlamaLauncher/releases)
page:

- **Windows** — run `LlamaLauncher-<ver>-setup.exe` (installs to Program
  Files, adds a Start-menu entry), or use the portable `…-win-x64-portable.exe`.
- **Linux** — install the `.deb`
  (`sudo apt install ./llama-launcher_<ver>_amd64.deb`) for a menu entry,
  or extract the `.tar.gz` and run `./LlamaLauncher`.
- **macOS** — build from source for now (see [PACKAGING.md](PACKAGING.md)).

Your settings are **not** stored in the install folder — see
[Where settings are stored](#where-settings-are-stored).

## Build from source

Requires the **.NET 8 SDK** (any OS). The first build pulls the Avalonia
packages from NuGet, so it needs internet access once.

```bash
cd LlamaLauncher
dotnet restore
dotnet run
```

On Windows you can also open `LlamaLauncher.csproj` in Visual Studio 2022+
and press F5. On Linux/macOS `dotnet run` (or your IDE of choice) is all you need.

> The seeded `llama-models.json` uses **Windows paths** (`C:\llama\...`). On
> Linux/macOS, edit the *llama-server*, *models folder*, *opencode.json* and
> *config.ini* paths in the top bar to match your system (and note the server
> binary has no `.exe` there).

## How it works

- On startup it loads `llama-models.json` (next to the built binary, in
  `AppContext.BaseDirectory`) and scans the **models folder** for `*.gguf` files.
- Any `.gguf` on disk with no profile yet is added as an **unconfigured** entry
  (a `NEW` badge). Open it, fill in settings, and **Save** before it can load.
- **Load Model** stops any running server and starts `llama-server` with the
  arguments built from the selected profile. Output streams into the log panel.
- Whenever the config changes, the app also (re)writes the native
  `config.ini` preset — see below.

## Native llama-server `config.ini` preset

The app keeps a llama.cpp **INI preset** in sync with your profiles (the file
llama-server consumes via `--models-preset`, documented in
[`docs/preset.md`](https://github.com/ggml-org/llama.cpp/blob/master/docs/preset.md)).
This means the same managed profiles can be launched **without** the app:

```bash
llama-server --models-preset "config.ini" --host 0.0.0.0 --port 8080
```

Then request a model by its **`[section]` name** (the JSON `"model"` field in
your API call). Details:

- Each **configured** profile becomes one `[section]`; the section name is the
  model id used in router mode. Keys are llama-server long options with the
  dashes removed (`n-gpu-layers`, `ctx-size`, `cache-type-k`, `spec-type`, …),
  booleans are `1`/`0`, and Flash Attention is `flash-attn = on`.
- The emitted keys mirror exactly what the app passes on the command line, so a
  profile behaves the same whether you **Load** it in the app or start
  llama-server against the INI.
- **Host/port are not written per-section.** In router mode llama-server binds a
  single socket, so pass `--host/--port` on the command line (the file header
  shows the suggested line). Set `load-on-startup = true` under a section if you
  want a model to load automatically.
- The path, and whether to auto-write on save, are set in the top bar
  (*llama-server config.ini* row). Blank path = `config.ini` next to the app.
  **Write .ini** writes on demand.

## Preloaded config

`llama-models.json` ships with **seven profiles** (copied next to the binary on
build via `CopyToOutputDirectory=PreserveNewest`, so they're present on first
run and survive rebuilds once you edit the output copy):

- `gemma-4-12B (128K q8_0)` — safe/recommended Gemma config for 16 GB
- `gemma-4-12B (256K q4_0)` — full native window, lighter KV to fit
- `Qwen3.8-27B IQ3_XXS (160K)`
- `Qwen3.8-27B Q3_K_XL (64K)`
- `Qwen3.8-27B IQ4_XS pure (32K)` — speculation off
- `Qwen3.8-27B MTP IQ4_XS pure (32K)` — MTP draft speculation
- `Qwen3.8-27B Ridge 3.7bpw (96K)` — MTP draft speculation, `spec-draft-n-max 6`

Profiles that point at the **same** `.gguf` with different context/KV settings
(like the two Gemma entries) are exactly what profiles are for.

## Requirement mapping

| # | Requirement | Where |
|---|---|---|
| 1 | C#/.NET 8 | `LlamaLauncher.csproj` (`net8.0`) |
| 2 | Cross-platform (Avalonia) UI | `App.axaml`, `MainWindow.axaml` |
| 3 | List models in the models folder | `Services/ModelScanner.cs` + model list |
| 4 | Local JSON of per-model settings | `Services/ConfigService.cs`, `Models/*` |
| 5 | View/edit/save/cancel/load | `ViewModels/MainViewModel.cs`, `ViewModels/ModelConfigViewModel.cs` |
| 6 | Launch server from a profile | `Services/LlamaServerLauncher.cs` |
| 7 | Native llama-server `config.ini` | `Services/PresetIniService.cs` |
| 8 | Modern look + scan-for-new + gate load until configured | dark theme + `NEW` badge + `CanLoad()` |

## Notes

- **OpenCode context sync:** set the *opencode.json* path in the top bar and keep
  *Sync context on load* checked. Loading a profile rewrites
  `provider.*.models.<alias>.limit.context` in that file to the profile's context
  size (matched by the profile's **Alias**, e.g. `qwen38-27b`). The file is
  re-serialized as plain JSON — comments are not preserved.
- **Profiles, not just files:** each entry is a named profile keyed by *Profile
  name*; several profiles can target one `.gguf`. **Duplicate** spins up a
  variant, **Delete** removes one.
- **Sampling toggle:** clear *Emit sampling overrides* to leave the
  `--temp/--top-p/--top-k/--min-p/--presence-penalty` flags (and their INI keys)
  off and let the server/client decide.
- **`0` = server default:** on the integer performance fields (`-np`, `-b`,
  `-ub`, `-t`, `-tb`, `--threads-http`), `0` omits the flag/key so llama-server
  uses its own default.
- The **Extra Arguments** box is appended verbatim on the command line; in the
  INI, long-form `--key value` / `--flag` args are translated to preset keys and
  the raw string is also kept as a comment.
- Only one server runs at a time; loading a new model kills the previous one.
- Vision projector files (`mmproj*.gguf`) are skipped by the scanner.

## Where settings are stored

The app never writes into its install folder, so a normal Program Files install
works without admin rights:

- **Portable / dev** (zip or `dotnet run`, folder writable): `llama-models.json`
  and the generated `config.ini` stay **next to the binary**.
- **Installed**: they live under a per-user data directory —
  `%APPDATA%\LlamaLauncher` (Windows) or `~/.config/LlamaLauncher`
  (Linux/macOS) — seeded on first run from the bundled profiles.

You can always point the *config.ini* path in the top bar somewhere else (e.g.
your llama.cpp working directory) so `llama-server --models-preset` finds it.

## Packaging & releases

Building installers (Windows Inno Setup, Linux `.deb`/`.tar.gz`) and the GitHub
Actions release flow are documented in **[PACKAGING.md](PACKAGING.md)**. The
`.exe` file icon is embedded via `<ApplicationIcon>` in the csproj.

## Contributing

See **[CONTRIBUTING.md](CONTRIBUTING.md)**. In short: keep
`LlamaServerLauncher.BuildArguments` and `PresetIniService` in sync when adding
fields, and open focused PRs.

## License

[MIT](LICENSE) © AptusWorks LLC. Update the copyright holder in `LICENSE` and the
metadata in `LlamaLauncher.csproj` if you fork or re-publish.

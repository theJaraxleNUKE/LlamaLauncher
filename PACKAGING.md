# Packaging & releasing

This app is Avalonia / `net8.0`, so it publishes for Windows, Linux, and macOS
from the same project. Everything below assumes the repo root as the working
directory.

## Where config lives (important for installers)

The app **does not** write into its install folder. It picks storage like this:

- **Portable / dev:** if `llama-models.json` sits next to the binary and that
  folder is writable (a zip drop or `dotnet run`), it's used in place.
- **Installed:** otherwise config and the generated `config.ini` live under a
  per-user data dir — `%APPDATA%\LlamaLauncher` on Windows,
  `~/.config/LlamaLauncher` on Linux/macOS — seeded on first run from the
  `llama-models.json` bundled next to the binary.

That's why a normal *Program Files* install works without admin-write issues.

## The .exe icon

The Windows executable icon comes from `<ApplicationIcon>app.ico</ApplicationIcon>`
in `LlamaLauncher.csproj` (embedded into the PE header at build time). The
Avalonia `Window Icon="/app.ico"` only sets the runtime window/taskbar icon, so
both are needed. Linux/macOS have no per-exe icon; the `.desktop` file / app
bundle carries it there (icons are generated under `packaging/linux/`).

## Windows installer (Inno Setup)

1. Install the **.NET 8 SDK** and **[Inno Setup 6+](https://jrsoftware.org/isdl.php)**.
2. From the repo root:

   ```powershell
   ./packaging/windows/build-windows.ps1 -Version 1.0.0
   ```

   This publishes a self-contained `win-x64` build into
   `installer/windows/publish`, then compiles `installer/windows/LlamaLauncher.iss`.
   The setup lands in `installer/windows/Output/LlamaLauncher-1.0.0-setup.exe`.

   Add `-SingleFile` to also emit a portable single-file `dist/LlamaLauncher.exe`,
   or `-SkipInstaller` to publish only.

Keep the `AppId` GUID in the `.iss` **stable** across versions so upgrades
replace the previous install instead of stacking.

## Linux (.tar.gz + .deb)

Needs the **.NET 8 SDK**; `dpkg-deb` (from `dpkg`) for the `.deb`.

```bash
./packaging/linux/build-linux.sh 1.0.0 linux-x64      # or linux-arm64
```

Outputs to `dist/`:
- `LlamaLauncher-1.0.0-linux-x64.tar.gz` — portable; extract and run
  `./LlamaLauncher`.
- `llama-launcher_1.0.0_amd64.deb` — installs to `/opt/llama-launcher`,
  adds a menu entry + icons, and a `/usr/bin/llama-launcher` launcher.

> Self-contained .NET apps may still need a couple of system libraries on
> minimal images (commonly `libicu` and `openssl`). Most desktop distros already
> have them. The `.deb` declares only `libc6` to avoid blocking installs on
> naming differences between distros.

### AppImage (optional)

With [`appimagetool`](https://github.com/AppImage/AppImageKit) on PATH you can
wrap the published folder: put it in an `AppDir` with an `AppRun` that execs
`LlamaLauncher`, copy `packaging/linux/llama-launcher.desktop` and
`packaging/linux/app.png` to the `AppDir` root, then run
`appimagetool AppDir`.

## macOS (.app / .dmg, optional)

```bash
dotnet publish LlamaLauncher.csproj -c Release -r osx-x64 --self-contained true -o build/osx
```

Wrap `build/osx` in a `LlamaLauncher.app` bundle (`Contents/MacOS`,
`Contents/Info.plist`, `Contents/Resources/app.icns`) and optionally
`hdiutil create` a `.dmg`. Unsigned bundles need a right-click → Open the first
time. (No script is shipped for this yet — PRs welcome.)

## Automated releases

`.github/workflows/release.yml` runs on a `vX.Y.Z` tag: it builds the Windows
installer + portable exe and the Linux tarball + `.deb`, then opens a **draft**
GitHub Release with the files attached.

```bash
git tag v1.0.0
git push origin v1.0.0
```

`.github/workflows/ci.yml` builds on every push/PR to `main` (Windows + Linux).

## Notes on trimming / single-file

- **Don't enable `PublishTrimmed`** — Avalonia relies on reflection and trimming
  tends to strip types it needs at runtime.
- Single-file publish uses `-p:IncludeNativeLibrariesForSelfExtract=true` so the
  bundled native libraries (Skia, etc.) extract correctly.

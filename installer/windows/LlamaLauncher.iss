; Inno Setup script for Llama Launcher.
; Build:  1) publish the app into .\publish  (see packaging\windows\build-windows.ps1)
;         2) compile this script with ISCC.exe (Inno Setup 6+):
;               "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\windows\LlamaLauncher.iss
;         3) the setup .exe lands in .\installer\windows\Output
;
; The app stores its config under %APPDATA%\LlamaLauncher, so a normal
; Program Files install is fine — nothing is written into the install folder.

#define MyAppName "Llama Launcher"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "AptusWorks LLC"
#define MyAppURL "https://github.com/theJaraxleNUKE/LlamaLauncher"
#define MyAppExeName "LlamaLauncher.exe"
; Folder containing the published, self-contained app (next to this script).
#define SourceDir "publish"

[Setup]
; NOTE: keep AppId STABLE across versions so upgrades replace in place.
AppId={{8F1E2A44-3B9C-4D77-9E21-5C6A7B8D9E01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\LlamaLauncher
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
OutputDir=Output
OutputBaseFilename=LlamaLauncher-{#MyAppVersion}-setup
SetupIconFile=..\..\app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Let the user pick per-user or all-users at install time.
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog commandline

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Everything from the publish folder (self-contained: exe + runtime + native libs).
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

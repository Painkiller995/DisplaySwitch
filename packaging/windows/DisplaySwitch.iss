; DisplaySwitch installer (Inno Setup 6). Built by packaging/windows/build.ps1, which passes AppVersion.
; Installs per user by default (no administrator prompt); users may choose "all users" in the
; privileges dialog. User settings in %APPDATA%\DisplaySwitch are kept on uninstall.

#ifndef AppVersion
  #error AppVersion must be defined, e.g. ISCC /DAppVersion=1.0.0
#endif

#define AppName "DisplaySwitch"
#define AppExe "DisplaySwitch.exe"
#define RepoRoot "..\.."
#define PublishDir RepoRoot + "\artifacts\publish\win-x64"

[Setup]
; Never change AppId: Windows uses it to recognise upgrades of the same product.
AppId={{6B0F3C61-7C4E-4E0B-9D6A-5E1A2C9B7D41}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=DisplaySwitch
AppComments=Switch your monitor's input from your desktop.
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} installer
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 1809 or later, the oldest version supported by .NET 10.
MinVersion=10.0.17763
OutputDir={#RepoRoot}\artifacts\installer
OutputBaseFilename={#AppName}-{#AppVersion}-win-x64-setup
SetupIconFile={#RepoRoot}\src\DisplaySwitch.App\Assets\displayswitch.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
; Closes a running copy (via Windows Restart Manager) before files are replaced or removed.
CloseApplications=force
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Start {#AppName} when I sign in (recommended, so keyboard shortcuts work right away)"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Must match WindowsAutostartService so the in-app toggle reflects it.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; \
  ValueData: """{app}\{#AppExe}"" --background"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
// The app survives window close (tray), so Restart Manager can't stop it: quit, then force-close.
procedure StopRunningApp;
var
  ResultCode: Integer;
begin
  if FileExists(ExpandConstant('{app}\{#AppExe}')) then
  begin
    Exec(ExpandConstant('{app}\{#AppExe}'), '--quit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(1500);
  end;
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM {#AppExe} /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApp;
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopRunningApp;
  Result := True;
end;

// Also removes a startup entry created from the app's Settings.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#AppName}');
end;

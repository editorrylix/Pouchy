; Pouchy installer (Inno Setup 6.3 or later).
; Built by the release workflow:
;   iscc /DAppVersion=1.2.0 /DArch=x64 /DSourceExe=..\publish\win-x64\Pouchy.exe installer\Pouchy.iss
;
; Installs for the current user only (no admin prompt) into %LocalAppData%\Programs\Pouchy,
; which also lets Pouchy update itself in place.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef FileVersion
  ; Numbers only (no "-beta.1"), for the file's version resource.
  #define FileVersion AppVersion
#endif
#ifndef SourceExe
  #define SourceExe "..\publish\win-" + Arch + "\Pouchy.exe"
#endif

#define AppName "Pouchy"
#define AppUrl "https://github.com/editorrylix/Pouchy"

[Setup]
; Never change the AppId: Windows uses it to recognise upgrades. Pouchy reads it too (UpdateInstaller.UninstallKey).
AppId={{6F1D5B8E-3C2A-4F7B-9E21-6B3D2A9C4E10}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=editorrylix
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
AppCopyright=MIT License
VersionInfoVersion={#FileVersion}
DefaultDirName={localappdata}\Programs\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=Pouchy-{#AppVersion}-setup-{#Arch}
SetupIconFile=..\Assets\pouchy.ico
UninstallDisplayIcon={app}\Pouchy.exe
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=100
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
MinVersion=10.0.19041
CloseApplications=no
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Start Pouchy when I sign in to Windows"
Name: "explorer"; Description: "Add ""Add to Pouchy"" to the Explorer right-click menu and to Send to"
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\Pouchy.exe"; Comment: "A drop shelf for Windows"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\Pouchy.exe"; Tasks: desktopicon
Name: "{usersendto}\{#AppName}"; Filename: "{app}\Pouchy.exe"; Parameters: "--add"; Comment: "Put the selected items in your pouch"; Tasks: explorer

; The same entries Pouchy's own settings write (StartupService and ExplorerIntegrationService), so either can turn them off.
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Pouchy"; ValueData: """{app}\Pouchy.exe"""; Tasks: startup; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\*\shell\Pouchy"; ValueType: string; ValueName: ""; ValueData: "Add to Pouchy"; Tasks: explorer; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\Pouchy"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\Pouchy.exe"",0"; Tasks: explorer
Root: HKCU; Subkey: "Software\Classes\*\shell\Pouchy"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Tasks: explorer
Root: HKCU; Subkey: "Software\Classes\*\shell\Pouchy\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Pouchy.exe"" --add ""%1"""; Tasks: explorer
Root: HKCU; Subkey: "Software\Classes\Directory\shell\Pouchy"; ValueType: string; ValueName: ""; ValueData: "Add to Pouchy"; Tasks: explorer; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\Pouchy"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\Pouchy.exe"",0"; Tasks: explorer
Root: HKCU; Subkey: "Software\Classes\Directory\shell\Pouchy"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Tasks: explorer
Root: HKCU; Subkey: "Software\Classes\Directory\shell\Pouchy\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Pouchy.exe"" --add ""%1"""; Tasks: explorer

[Run]
Filename: "{app}\Pouchy.exe"; Description: "Start Pouchy"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Removes whatever Pouchy added from its own settings (startup entry, Explorer menu).
Filename: "{app}\Pouchy.exe"; Parameters: "--cleanup"; Flags: runhidden waituntilterminated; RunOnceId: "PouchyCleanup"

[UninstallDelete]
Type: files; Name: "{app}\Pouchy.exe.old"

[Code]
procedure StopPouchy();
var
  ResultCode: Integer;
begin
  { Pouchy lives in the tray, so there's no window to close: end it so its file can be replaced. }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM Pouchy.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(400);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopPouchy();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopPouchy();
  Result := True;
end;

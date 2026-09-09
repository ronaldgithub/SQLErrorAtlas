; Inno Setup script for SQL Error Atlas
; Build:  ISCC /DAppVersion=0.1.0 build\installer.iss
; Expects the self-contained publish output in  build\publish\  (see release.yml / README).

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define AppName "SQL Error Atlas"
#define AppExe "SQLErrorAtlas.exe"
#define AppPublisher "Ronald de Groot"
#define AppUrl "https://github.com/ronaldgithub/SQLErrorAtlas"

[Setup]
AppId={{7B3D9A2C-1E4F-4A9B-9C21-5E7A2F0B8D33}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppSupportURL={#AppUrl}
DefaultDirName={autopf}\SQL Error Atlas
DefaultGroupName=SQL Error Atlas
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=Output
OutputBaseFilename=SQLErrorAtlas-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExe}
; Per-machine if elevated, else per-user — no admin rights required.
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\SQL Error Atlas"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,SQL Error Atlas}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\SQL Error Atlas"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,SQL Error Atlas}"; Flags: nowait postinstall skipifsilent

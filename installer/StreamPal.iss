#define MyAppName "StreamPAL"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Free Waves"
#define MyAppURL "https://www.freewaves.it"
#define MyAppExeName "StreamPAL.exe"

[Setup]
AppId={{BAE61F54-DCC5-49D6-9827-0847FF6C5A26}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\StreamPAL
DefaultGroupName=StreamPAL
DisableProgramGroupPage=yes
LicenseFile=EULA.txt
OutputDir=..\outputs
OutputBaseFilename=StreamPAL-Setup-1.0.0
SetupIconFile=..\src\StreamForge.App\Assets\StreamPAL.ico
WizardImageFile=..\src\StreamForge.App\Assets\setup-wizard.bmp
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion=1.0.0.0
VersionInfoCompany=Free Waves
VersionInfoDescription=StreamPAL Setup
VersionInfoCopyright=Copyright (c) 2026 Free Waves. All rights reserved.

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\outputs\StreamPAL-win-x64\StreamPAL.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\outputs\StreamPAL-win-x64\ThirdPartyNotices\FDK-AAC-LICENSE.txt"; DestDir: "{app}\ThirdPartyNotices"; Flags: ignoreversion
Source: "..\outputs\StreamPAL-win-x64\ThirdPartyNotices\FDK-AAC-SOURCE.txt"; DestDir: "{app}\ThirdPartyNotices"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\StreamPAL"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\StreamPAL"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,StreamPAL}"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCU; Subkey: "Software\FreeWaves\StreamPAL"; ValueType: string; ValueName: "InstallerLanguage"; ValueData: "{language}"; Flags: uninsdeletekey

#define MyAppName "StreamPAL Trial"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Free Waves"
#define MyAppURL "https://www.freewaves.it/streampal.html"
#define MyAppExeName "StreamPAL.exe"

[Setup]
AppId={{2CA64EE7-329E-43CE-A8CC-44454C87C456}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\StreamPAL Trial
DefaultGroupName=StreamPAL Trial
DisableProgramGroupPage=yes
LicenseFile=EULA-Trial.txt
OutputDir=..\outputs
OutputBaseFilename=StreamPAL-Trial-Setup-1.0.0
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
VersionInfoDescription=StreamPAL Trial Setup
VersionInfoCopyright=Copyright (c) 2026 Free Waves. All rights reserved.

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\outputs\StreamPAL-Trial-win-x64\StreamPAL.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\outputs\StreamPAL-Trial-win-x64\ThirdPartyNotices\FDK-AAC-LICENSE.txt"; DestDir: "{app}\ThirdPartyNotices"; Flags: ignoreversion
Source: "..\outputs\StreamPAL-Trial-win-x64\ThirdPartyNotices\FDK-AAC-SOURCE.txt"; DestDir: "{app}\ThirdPartyNotices"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\StreamPAL Trial"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\StreamPAL Trial"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,StreamPAL Trial}"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCU; Subkey: "Software\FreeWaves\StreamPALTrial"; ValueType: string; ValueName: "InstallerLanguage"; ValueData: "{language}"; Flags: uninsdeletekey

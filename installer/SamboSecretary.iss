[Setup]
AppName=Самбо-секретарь
AppVersion=1.0.0
DefaultDirName={autopf}\SamboSecretary
DefaultGroupName=Самбо-секретарь
OutputDir=output
OutputBaseFilename=SamboSecretary_Setup_1.0.0_win64
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\SamboSecretary.exe
[Files]
Source: "..\publish\SamboSecretary.exe"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{group}\Самбо-секретарь"; Filename: "{app}\SamboSecretary.exe"
Name: "{autodesktop}\Самбо-секретарь"; Filename: "{app}\SamboSecretary.exe"
[Run]
Filename: "{app}\SamboSecretary.exe"; Description: "Запустить Самбо-секретарь"; Flags: nowait postinstall skipifsilent
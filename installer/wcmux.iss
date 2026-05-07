[Setup]
AppName=wcmux
AppVersion=1.1.1
AppPublisher=wcmux
DefaultDirName={autopf}\wcmux
DefaultGroupName=wcmux
OutputDir=.
OutputBaseFilename=wcmux-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\docs\wcmux.ico
UninstallDisplayIcon={app}\Wcmux.App.exe

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\wcmux"; Filename: "{app}\Wcmux.App.exe"
Name: "{commondesktop}\wcmux"; Filename: "{app}\Wcmux.App.exe"

[Run]
Filename: "{app}\Wcmux.App.exe"; Description: "Launch wcmux"; Flags: nowait postinstall skipifsilent

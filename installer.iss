; Inno Setup script (free: jrsoftware.org/isinfo.php)
; Pehle build.bat chalao taaki publish\ folder ban jaye.
[Setup]
AppName=AutoPrint Agent
AppVersion=1.0.0
AppPublisher=AutoPrint
DefaultDirName={autopf}\AutoPrint Agent
DefaultGroupName=AutoPrint Agent
PrivilegesRequired=lowest
OutputDir=dist
OutputBaseFilename=AutoPrintAgent-Setup
Compression=lzma2
SolidCompression=yes
DisableProgramGroupPage=yes

[Files]
Source: "publish\AutoPrintAgent.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "sumatra\SumatraPDF.exe";     DestDir: "{app}\sumatra"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\AutoPrint Agent"; Filename: "{app}\AutoPrintAgent.exe"
Name: "{userdesktop}\AutoPrint Agent";  Filename: "{app}\AutoPrintAgent.exe"

[Run]
Filename: "{app}\AutoPrintAgent.exe"; Description: "AutoPrint Agent abhi kholo"; Flags: postinstall nowait skipifsilent

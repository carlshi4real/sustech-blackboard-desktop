[Setup]
AppId={{F4CD3038-49E9-4EB1-91CD-E89814683D85}
AppName=Blackboard Desktop
AppVersion=1.0.0
AppPublisher=SUSTech Blackboard Desktop
DefaultDirName={localappdata}\Programs\BlackboardDesktop
DefaultGroupName=Blackboard Desktop
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\dist
OutputBaseFilename=BlackboardDesktop-Windows-x64-Setup-v1.0.0
SetupIconFile=app.ico
UninstallDisplayIcon={app}\BlackboardDesktop.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Files]
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Icons]
Name: "{group}\Blackboard Desktop"; Filename: "{app}\BlackboardDesktop.exe"
Name: "{autodesktop}\Blackboard Desktop"; Filename: "{app}\BlackboardDesktop.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\BlackboardDesktop.exe"; Description: "Open Blackboard Desktop"; Flags: nowait postinstall skipifsilent

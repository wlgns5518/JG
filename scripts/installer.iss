#define MyAppName "Marketing AI Agent"
#define MyAppVersion "1.0.1"
[Setup]
AppId={{C66D81E0-DCB5-454B-B408-4B72EB60CB49}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Marketing AI Agent
DefaultDirName={localappdata}\Programs\MarketingAIAgent
DefaultGroupName={#MyAppName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=MarketingAIAgent-{#MyAppVersion}-win-x64-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\MarketingAIAgent.exe
CloseApplications=yes
[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\MarketingAIAgent.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\MarketingAIAgent.exe"; Tasks: desktopicon
[Tasks]
Name: "desktopicon"; Description: "바탕 화면 바로 가기 만들기"; Flags: unchecked
[Run]
Filename: "{app}\MarketingAIAgent.exe"; Description: "Marketing AI Agent 실행"; Flags: nowait postinstall skipifsilent

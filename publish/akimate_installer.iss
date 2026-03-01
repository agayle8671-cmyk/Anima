; akimate Installer Script
#define MyAppName "akimate"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "Andre Gayle"
#define MyAppURL "https://github.com/agayle8671-cmyk/Anima.git"
#define MyAppExeName "akimate.exe"
#define MyPublishDir "c:\Users\Kaafl\OneDrive\Desktop\AI Anime Studio\akimate\publish\akimate"
#define MyLogo "c:\Users\Kaafl\OneDrive\Desktop\AI Anime Studio\akimate\publish\logo.png"

[Setup]
AppId={{D3F7B1E5-E259-48FD-B0C9-112FB1E5E259}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
WizardStyle=modern
WizardSmallImageFile={#MyLogo}
OutputDir=c:\Users\Kaafl\OneDrive\Desktop\AI Anime Studio\akimate\publish\installer
OutputBaseFilename=akimate_setup
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Clean up stored API keys from Windows Credential Manager on uninstall
Filename: "powershell.exe"; Parameters: "-NoProfile -Command ""try {{ $vault = New-Object Windows.Security.Credentials.PasswordVault; $creds = $vault.FindAllByResource('akimate'); foreach ($c in $creds) {{ $vault.Remove($c) }} }} catch {{}}"""; Flags: runhidden

[UninstallDelete]
; Remove user data directory
Type: filesandordirs; Name: "{userappdata}\akimate"
Type: filesandordirs; Name: "{localappdata}\akimate"

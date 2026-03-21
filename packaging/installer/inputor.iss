#ifndef PublishedDir
  #error PublishedDir must be provided via ISCC /DPublishedDir=...
#endif

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#ifndef OutputDir
  #define OutputDir "."
#endif

#ifndef OutputBaseFilename
  #define OutputBaseFilename "inputor-setup-win-x64"
#endif

[Setup]
AppId={{7E6B6BA6-9DA3-4E1B-9A4D-4F6B89D58C38}
AppName=inputor
AppVersion={#AppVersion}
AppPublisher=inputor
DefaultDirName={localappdata}\Programs\inputor
DefaultGroupName=inputor
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\inputor.App.exe
SetupIconFile={#PublishedDir}\Assets\inputor.ico
ChangesAssociations=no
CloseApplications=yes
CloseApplicationsFilter=inputor.App.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishedDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\inputor"; Filename: "{app}\inputor.App.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\inputor.ico"
Name: "{autodesktop}\inputor"; Filename: "{app}\inputor.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon; IconFilename: "{app}\Assets\inputor.ico"

[Run]
Filename: "{app}\inputor.App.exe"; Description: "Launch inputor"; Flags: nowait postinstall skipifsilent

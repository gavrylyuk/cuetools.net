#ifndef PayloadDir
  #error PayloadDir is required
#endif
#ifndef RedistPath
  #error RedistPath is required
#endif
#ifndef OutputDir
  #error OutputDir is required
#endif
#ifndef AppVersion
  #define AppVersion "2.2.6.2"
#endif
#define RedistVersion GetFileVersion(RedistPath)

[Setup]
AppId={{DBDAE944-653B-4688-A128-8772C777E6C4}
AppName=CUETools
AppVersion={#AppVersion}-net10
AppVerName=CUETools {#AppVersion} (.NET 10)
AppPublisher=CUETools contributors / gavrylyuk fork
AppPublisherURL=https://github.com/gavrylyuk/cuetools.net
AppSupportURL=https://github.com/gavrylyuk/cuetools.net/issues
DefaultDirName={autopf}\CUETools
DefaultGroupName=CUETools
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
UninstallDisplayIcon={app}\CUETools.exe
SetupIconFile=..\CUETools\cue2.ico
LicenseFile=..\GPL.txt
OutputDir={#OutputDir}
OutputBaseFilename=CUETools-{#AppVersion}-net10-win-x64-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RedistPath}"; DestName: "vc_redist.x64.exe"; Flags: dontcopy

[Icons]
Name: "{group}\CUETools"; Filename: "{app}\CUETools.exe"; WorkingDir: "{app}"
Name: "{group}\Uninstall CUETools"; Filename: "{uninstallexe}"
Name: "{autodesktop}\CUETools"; Filename: "{app}\CUETools.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\CUETools.exe"; Description: "Launch CUETools"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
function NeedsVCRuntime: Boolean;
var
  Installed: Cardinal;
  VersionString: String;
  InstalledVersion, RequiredVersion: Int64;
begin
  Result := True;
  if not RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed) then Exit;
  if Installed <> 1 then Exit;
  if not RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Version', VersionString) then Exit;
  if Copy(VersionString, 1, 1) = 'v' then Delete(VersionString, 1, 1);
  if not StrToVersion(VersionString, InstalledVersion) then Exit;
  if not StrToVersion('{#RedistVersion}', RequiredVersion) then Exit;
  Result := ComparePackedVersion(InstalledVersion, RequiredVersion) < 0;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
begin
  Result := '';
  if not NeedsVCRuntime then Exit;
  if not IsAdminInstallMode then begin
    Result := 'The Visual C++ x64 runtime must be installed first. Run Setup as administrator to install the bundled runtime.';
    Exit;
  end;
  ExtractTemporaryFile('vc_redist.x64.exe');
  if not Exec(ExpandConstant('{tmp}\vc_redist.x64.exe'), '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then begin
    Result := 'Could not start the Visual C++ runtime installer.';
    Exit;
  end;
  if ExitCode = 3010 then NeedsRestart := True
  else if (ExitCode <> 0) and (ExitCode <> 1638) then
    Result := Format('Visual C++ runtime installation failed (code %d).', [ExitCode]);
end;

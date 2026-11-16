; =========================================================================
; PrintPulse - Professional Inno Setup Script with Pre-flight Check
; Target: Windows XP SP3, Vista, 7, 8, 8.1, 10, 11 (32-bit & 64-bit)
; =========================================================================

#define MyAppName "PrintPulse"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "PrintPulse Technologies"
#define MyAppURL "https://printpulse.app"
#define MyAppExeName "PrintPulse.exe"

[Setup]
AppId={{E834D970-13D9-44F0-A3DC-5527A792E881}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\dist\Installer
OutputBaseFilename=PrintPulse_Setup_v1.0
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\dist\Portable\PrintPulse.exe"; DestDir: "{app}"; Flags: ignoreversion
; Optional offline redistributable (place dotNetFx40_Client_setup.exe in redist/ if bundling)
; Source: "redist\dotNetFx40_Client_setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall ignoreversion; Check: not IsDotNet40Installed

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Check if .NET Framework 4.0 or higher (4.5, 4.6, 4.7, 4.8) is present
function IsDotNet40Installed: Boolean;
var
  Installed: Cardinal;
begin
  Result := False;
  // Check .NET 4.0 Client Profile
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Client', 'Install', Installed) then
  begin
    if Installed = 1 then
    begin
      Result := True;
      Exit;
    end;
  end;
  // Check .NET 4.0 Full
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Install', Installed) then
  begin
    if Installed = 1 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

// Pre-flight check before installation begins
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not IsDotNet40Installed then
  begin
    if MsgBox('PrintPulse requires Microsoft .NET Framework 4.0 or higher to run.' + #13#10 +
              'Would you like to open the official Microsoft download page now?', 
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet-framework/net40', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
    Result := False;
  end;
end;

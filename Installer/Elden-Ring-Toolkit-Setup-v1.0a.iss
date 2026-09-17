#define MyAppName "Elden Ring Toolkit"
#define MyAppVersion "1.0a"
#define MyAppPublisher "Kane"
#define MyAppExeName "Elden Ring Toolkit.exe"
#define DoubleAmp(Value) StringChange(Value, "&", "&&")

[Setup]
AppId={{CFE7B0B6-46FF-4716-B9CA-F2C3C132AA21}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
DisableWelcomePage=no
OutputDir=Output
OutputBaseFilename=Elden-Ring-Toolkit-Setup-v1.0a
SetupIconFile=..\Assets\EldenRingToolkit.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\bin\Release\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\bin\Release\publish\Data\*"; DestDir: "{app}\Data"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\bin\Release\publish\Libs\*"; DestDir: "{app}\Libs"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#DoubleAmp(MyAppName)}}"; Flags: nowait postinstall skipifsilent

[Code]
function IsDotNet10DesktopInstalled: Boolean;
var
  FindRec: TFindRec;
  RuntimePath: String;
begin
  Result := False;
  RuntimePath := ExpandConstant('{autopf}\dotnet\shared\Microsoft.WindowsDesktop.App\');

  if FindFirst(RuntimePath + '10.*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0) then
        begin
          Result := True;
          Exit;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function InitializeSetup: Boolean;
var
  ResultCode: Integer;
begin
  Result := True;

  if not IsDotNet10DesktopInstalled then
  begin
    if MsgBox(
      '.NET 10 Desktop Runtime (x64) is required to run Elden Ring Toolkit.' + #13#10 + #13#10 +
      'Would you like to download it now?',
      mbConfirmation,
      MB_YESNO
    ) = IDYES then
    begin
      ShellExec(
        'open',
        'https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-10.0.12-windows-x64-installer',
        '',
        '',
        SW_SHOWNORMAL,
        ewNoWait,
        ResultCode
      );
    end;

    Result := False;
  end;
end;

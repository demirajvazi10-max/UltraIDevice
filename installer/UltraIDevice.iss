; Ultra iDevice - Inno Setup skripta
; Kompajlira se automatski kroz .github/workflows/release.yml (Inno-Setup-Action)
; kada gurnes tag "v*". Moze i rucno, ako imas Inno Setup instaliran lokalno:
; ISCC.exe installer\UltraIDevice.iss  (pokretati iz root foldera repo-a)

#define MyAppName "Ultra iDevice"
#ifndef MyAppVersion
  #define MyAppVersion "2.0.1"
#endif
#define MyAppPublisher "Demir Ajvazi"
#define MyAppExeName "UltraIDevice.exe"
#define MyAppURL "https://github.com/demirajvazi10-max/UltraIDevice"

[Setup]
; Jedinstveni GUID za ovu aplikaciju - ne diraj posle prvog release-a,
; Windows ga koristi da prepozna update vs. novu instalaciju.
AppId={{B1E7C4A2-3F9D-4C21-8E6B-7A0D5C3F21E4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; GitHub-ov standardni GPL-3.0 template se zove "LICENSE" (bez ekstenzije).
LicenseFile=..\LICENSE
OutputDir=..\Output
OutputBaseFilename=UltraIDevice-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; x64 arhitektura - eksplicitno, da instalacija ide u pravi (64-bit) Program Files.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; dotnet publish (self-contained, single-file) izbacuje sve u /publish -
; pokupi ceo sadrzaj tog foldera, iskljucujuci .pdb fajlove za debug simbole.
; Ako kasnije budes hteo da alati (libimobiledevice/idevicerestore) stizu uz
; instalaciju umesto da se preuzimaju, stavi ih u publish\tools pre pakovanja -
; aplikacija prvo trazi bas taj "tools" podfolder pored exe-ja.
Source: "..\publish\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

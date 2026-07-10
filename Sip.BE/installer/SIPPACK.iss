; Inno Setup script - tạo installer SIPPACK-Setup.exe
; Yêu cầu: cài Inno Setup (https://jrsoftware.org/isdl.php), mở file này bằng Inno Setup Compiler -> Compile.
; Trước khi compile: chạy build-desktop.ps1 để tạo thư mục ..\publish.

#define MyAppName "SIPPACK"
#define MyAppVersion "2.1.0"
#define MyAppExe "SIPPACK.exe"

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=SaoMaiSolutions
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=installer-output
OutputBaseFilename={#MyAppName}-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; SetupIconFile=icon.ico   ; (tuỳ chọn) bỏ comment nếu có file icon.ico

[Languages]
Name: "vi"; MessagesFile: "compiler:Default.isl"

[Files]
; Toàn bộ output publish (đã kèm wwwroot + seed + .NET runtime self-contained). publish nằm ở thư mục gốc Sip\publish
Source: "..\..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
; (tuỳ chọn) nhúng WebView2 bootstrapper để tự cài nếu máy thiếu (đa số Win10/11 đã có sẵn):
; Source: "MicrosoftEdgeWebView2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"

[Run]
; (tuỳ chọn) cài WebView2 runtime nếu cần - bỏ comment nếu có bundle bootstrapper ở trên:
; Filename: "{tmp}\MicrosoftEdgeWebView2Setup.exe"; Parameters: "/silent /install"; Flags: waituntilterminated; Check: not WebView2Installed
Filename: "{app}\{#MyAppExe}"; Description: "Chạy {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
// Kiểm tra WebView2 Runtime đã cài chưa (Evergreen) - dùng cho Check ở [Run]
function WebView2Installed(): Boolean;
var
  v: String;
begin
  Result :=
    RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', v) or
    RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', v);
end;

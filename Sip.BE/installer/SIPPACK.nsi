; Bộ cài SIPPACK - NSIS (giấy phép zlib/libpng: miễn phí cho cả mục đích thương mại,
; không cần license key, không chèn banner vào sản phẩm).
; Biên dịch: makensis.exe SIPPACK.nsi   (build-desktop.ps1 tự gọi nếu tìm thấy makensis)
; Trước khi biên dịch: chạy build-desktop.ps1 để tạo ..\..\publish
;
; Bộ cài NHÚNG SẴN và TỰ CÀI IM LẶNG các phần mềm phụ thuộc mà máy đích có thể thiếu:
;   - Microsoft Edge WebView2 Runtime : nhân hiển thị giao diện (Photino)
;   - Bit4id Universal MW             : middleware bắc cầu USB token vào Windows Certificate Store
;   - VGCA GCA01-Client v2            : trình điều khiển token Ban Cơ yếu Chính phủ
; Không có middleware token thì bước 1 wizard Ký số không liệt kê được chứng thư nào, vì SIPPACK
; lấy cert qua Windows CNG/Cert Store chứ không nói chuyện trực tiếp với token qua PKCS#11.
; SIPPACK.exe là self-contained nên KHÔNG cần cài .NET trên máy đích.

Unicode true

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"

!define APP_NAME      "SIPPACK"
!define APP_VERSION   "2.2.0"
!define APP_PUBLISHER "SaoMaiSolutions"
!define APP_EXE       "SIPPACK.exe"

!define PUBLISH_DIR "..\..\publish"

; Bộ cài trình điều khiển nằm SẴN trong publish\drivers (do build-desktop.ps1 đưa vào) nên đi theo
; luôn qua "File /r" bên dưới - không nhúng riêng lần nữa. Nhờ vậy bản .zip cũng có driver, và
; chính app tự cài ngầm được khi khởi động (Sip.Desktop/Program.cs).
!define DRIVERS_DIR "drivers"

!define WEBVIEW2_EXE "MicrosoftEdgeWebView2Setup.exe"
!define BIT4ID_EXE   "bit4id_xpki_1.4.10.764-ng-user-vgca-pkimgr-bwc.exe"
!define VGCA_MSI     "gca01-client-v2-x64-8.3.msi"

; Khoá nhận diện lấy trực tiếp từ chính bộ cài / máy đã cài, không đoán theo tên hiển thị:
;   VGCA  : ProductCode đọc từ bảng Property của file .msi
;   bit4id: nhánh registry do middleware tạo + file gỡ cài đặt của bộ cài NSIS gốc
!define VGCA_PRODUCT_CODE "{648B71AE-68FC-45D4-B1A3-21BC86229A73}"
!define WEBVIEW2_CLIENT   "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"

!define UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"

!if /FileExists "${PUBLISH_DIR}\${APP_EXE}"
!else
  !error "Không thấy ${PUBLISH_DIR}\${APP_EXE} - chạy build-desktop.ps1 trước khi biên dịch."
!endif

Name        "${APP_NAME} ${APP_VERSION}"
OutFile     "installer-output\${APP_NAME}-Setup.exe"
InstallDir  "$PROGRAMFILES64\${APP_NAME}"
InstallDirRegKey HKLM "${UNINST_KEY}" "InstallLocation"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
BrandingText "${APP_PUBLISHER} - ${APP_NAME} ${APP_VERSION}"

VIProductVersion "2.2.0.0"
VIAddVersionKey "ProductName"     "${APP_NAME}"
VIAddVersionKey "ProductVersion"  "${APP_VERSION}"
VIAddVersionKey "FileVersion"     "${APP_VERSION}.0"
VIAddVersionKey "CompanyName"     "${APP_PUBLISHER}"
VIAddVersionKey "FileDescription" "Bộ cài ${APP_NAME}"
VIAddVersionKey "LegalCopyright"  "${APP_PUBLISHER}"

Var Detected        ; kết quả của các hàm Detect* : "1" = đã có trên máy
Var DepFailed       ; danh sách phụ thuộc cài KHÔNG thành công, gộp lại báo cuối phiên
Var DriverInstalled ; có cài trình điều khiển token trong phiên này không -> cần khởi động lại

!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Chạy ${APP_NAME} ngay"

!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Vietnamese"

; ---------------------------------------------------------------------------
; Nhận diện phụ thuộc. Dùng CHUNG cho lúc quyết định có cài hay không VÀ lúc
; kiểm tra lại sau khi cài -> kết quả cuối cùng dựa trên trạng thái thật của máy
; chứ không tin vào mã trả về của bộ cài (nhiều bộ cài trả 0 dù chưa xong việc).
; ---------------------------------------------------------------------------
Function DetectWebView2
  StrCpy $Detected "0"
  SetRegView 64
  ReadRegStr $R0 HKLM "SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\${WEBVIEW2_CLIENT}" "pv"
  ${If} $R0 == ""
    ReadRegStr $R0 HKCU "SOFTWARE\Microsoft\EdgeUpdate\Clients\${WEBVIEW2_CLIENT}" "pv"
  ${EndIf}
  ${If} $R0 != ""
  ${AndIf} $R0 != "0.0.0.0"
    StrCpy $Detected "1"
  ${EndIf}
FunctionEnd

Function DetectBit4id
  StrCpy $Detected "0"
  ; Middleware là 32-bit -> nhánh registry nằm ở view 32-bit (WOW6432Node)
  SetRegView 32
  EnumRegKey $R0 HKLM "SOFTWARE\bit4id" 0
  ${If} $R0 != ""
    StrCpy $Detected "1"
  ${EndIf}
  ${If} $Detected != "1"
    ${If} ${FileExists} "$PROGRAMFILES32\Bit4Id\Universal MW\uninst.exe"
      StrCpy $Detected "1"
    ${EndIf}
  ${EndIf}
FunctionEnd

Function DetectVgca
  StrCpy $Detected "0"
  SetRegView 64
  ReadRegStr $R0 HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${VGCA_PRODUCT_CODE}" "DisplayName"
  ${If} $R0 == ""
    SetRegView 32
    ReadRegStr $R0 HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${VGCA_PRODUCT_CODE}" "DisplayName"
  ${EndIf}
  ${If} $R0 != ""
    StrCpy $Detected "1"
  ${EndIf}
FunctionEnd

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "${APP_NAME} chỉ chạy trên Windows 64-bit."
    Abort
  ${EndIf}
  SetShellVarContext all
  StrCpy $DepFailed ""
  StrCpy $DriverInstalled "0"
FunctionEnd

; ---------------------------------------------------------------------------
Section "-Ứng dụng ${APP_NAME}"
  SetDetailsPrint both
  DetailPrint "Đóng ${APP_EXE} nếu đang chạy..."
  nsExec::Exec 'taskkill /F /IM "${APP_EXE}"'
  Pop $0

  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*.*"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut  "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
  CreateShortcut  "$SMPROGRAMS\${APP_NAME}\Gỡ cài đặt ${APP_NAME}.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortcut  "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"

  SetRegView 64
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayName"     "${APP_NAME} ${APP_VERSION}"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayVersion"  "${APP_VERSION}"
  WriteRegStr   HKLM "${UNINST_KEY}" "Publisher"       "${APP_PUBLISHER}"
  WriteRegStr   HKLM "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayIcon"     "$INSTDIR\${APP_EXE}"
  WriteRegStr   HKLM "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKLM "${UNINST_KEY}" "EstimatedSize" "$0"
SectionEnd

; ---------------------------------------------------------------------------
Section "-Phụ thuộc cho USB token và ký số"
  SetDetailsPrint both
  CreateDirectory "$INSTDIR\install-logs"

  ; --- 1) WebView2 Runtime ---------------------------------------------------
  DetailPrint "Kiểm tra Microsoft Edge WebView2 Runtime..."
  Call DetectWebView2
  ${If} $Detected == "1"
    DetailPrint "  đã có sẵn - bỏ qua."
  ${Else}
    ${If} ${FileExists} "$INSTDIR\${DRIVERS_DIR}\${WEBVIEW2_EXE}"
      DetailPrint "  chưa có - đang cài..."
      ExecWait '"$INSTDIR\${DRIVERS_DIR}\${WEBVIEW2_EXE}" /silent /install' $0
      DetailPrint "  bộ cài trả về mã $0"
      Call DetectWebView2
      ${If} $Detected != "1"
        StrCpy $DepFailed "$DepFailed$\n   - Microsoft Edge WebView2 Runtime (mã $0)"
      ${EndIf}
    ${Else}
      DetailPrint "  không có bộ cài kèm theo - bỏ qua."
    ${EndIf}
  ${EndIf}

  ; --- 2) Bit4id Universal MW ------------------------------------------------
  DetailPrint "Kiểm tra middleware USB token (Bit4id Universal MW)..."
  Call DetectBit4id
  ${If} $Detected == "1"
    DetailPrint "  đã có sẵn - bỏ qua."
  ${Else}
    ${If} ${FileExists} "$INSTDIR\${DRIVERS_DIR}\${BIT4ID_EXE}"
      DetailPrint "  chưa có - đang cài (có thể mất vài phút)..."
      ; Bộ cài gốc của bit4id cũng là NSIS -> /S là cờ cài im lặng chuẩn
      ExecWait '"$INSTDIR\${DRIVERS_DIR}\${BIT4ID_EXE}" /S' $0
      DetailPrint "  bộ cài trả về mã $0"
      Call DetectBit4id
      ${If} $Detected == "1"
        StrCpy $DriverInstalled "1"
      ${Else}
        StrCpy $DepFailed "$DepFailed$\n   - Bit4id Universal MW (mã $0)"
      ${EndIf}
    ${Else}
      DetailPrint "  không có bộ cài kèm theo - bỏ qua."
    ${EndIf}
  ${EndIf}

  ; --- 3) VGCA GCA01-Client v2 ----------------------------------------------
  DetailPrint "Kiểm tra trình điều khiển token Ban Cơ yếu (GCA01-Client)..."
  Call DetectVgca
  ${If} $Detected == "1"
    DetailPrint "  đã có sẵn - bỏ qua."
  ${Else}
    ${If} ${FileExists} "$INSTDIR\${DRIVERS_DIR}\${VGCA_MSI}"
      DetailPrint "  chưa có - đang cài..."
      ExecWait 'msiexec /i "$INSTDIR\${DRIVERS_DIR}\${VGCA_MSI}" /qn /norestart /l*v "$INSTDIR\install-logs\gca01-client.log"' $0
      DetailPrint "  msiexec trả về mã $0"
      Call DetectVgca
      ${If} $Detected == "1"
        StrCpy $DriverInstalled "1"
      ${Else}
        StrCpy $DepFailed "$DepFailed$\n   - VGCA GCA01-Client v2 (mã $0, log: install-logs\gca01-client.log)"
      ${EndIf}
    ${Else}
      DetailPrint "  không có bộ cài kèm theo - bỏ qua."
    ${EndIf}
  ${EndIf}

  ; --- Báo cáo kết quả -------------------------------------------------------
  ${If} $DepFailed != ""
    DetailPrint "CẢNH BÁO: có phụ thuộc chưa cài được."
    MessageBox MB_ICONEXCLAMATION "Các thành phần sau chưa cài được:$\n$DepFailed$\n$\n\
${APP_NAME} vẫn chạy được, nhưng chức năng Ký số có thể không đọc được chứng thư trong USB token.$\n\
Xem log tại: $INSTDIR\install-logs"
  ${EndIf}

  ; Windows chỉ nhận thiết bị token sau khi khởi động lại -> để MUI hiện lựa chọn reboot
  ; ở trang cuối, tránh việc người dùng tưởng token hỏng.
  ${If} $DriverInstalled == "1"
    SetRebootFlag true
  ${EndIf}
SectionEnd

; ---------------------------------------------------------------------------
Section "Uninstall"
  SetShellVarContext all
  nsExec::Exec 'taskkill /F /IM "${APP_EXE}"'
  Pop $0

  Delete "$DESKTOP\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\Gỡ cài đặt ${APP_NAME}.lnk"
  RMDir  "$SMPROGRAMS\${APP_NAME}"

  RMDir /r "$INSTDIR"

  SetRegView 64
  DeleteRegKey HKLM "${UNINST_KEY}"

  ; CỐ Ý không gỡ Bit4id / GCA01-Client: đó là middleware dùng chung, phần mềm ký số
  ; khác trên máy vẫn đang phụ thuộc vào chúng.
  ; CỐ Ý không xoá %AppData%\SipPack\sip.db: đó là cấu hình của người dùng.
SectionEnd

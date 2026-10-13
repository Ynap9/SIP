# Đóng gói SIPPACK desktop (hướng C: .NET host WebView2 / Photino)
# Chạy: powershell -ExecutionPolicy Bypass -File build-desktop.ps1
$ErrorActionPreference = 'Stop'

$root    = Split-Path -Parent $MyInvocation.MyCommand.Path   # Sip.BE
$repo    = Split-Path -Parent $root                          # Sip
$fe      = Join-Path $repo 'Sip.FE'
$desktop = Join-Path $root 'Sip.Desktop'
$publish = Join-Path $repo 'publish'   # xuất ra thư mục gốc Sip\publish cho dễ tìm

# 0) Tắt app đang chạy (nếu có) để giải phóng file DLL trong publish/
Get-Process -Name 'SIPPACK' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400

# 1) Build Angular (config "desktop" -> baseUrl rỗng, same-origin)
Write-Host '==> Build Angular (desktop)...' -ForegroundColor Cyan
Push-Location $fe
npm run build -- --configuration desktop
Pop-Location

# 2) Publish .NET self-contained, single-file (gói hết DLL + runtime vào SIPPACK.exe)
Write-Host '==> Publish .NET (single-file, self-contained win-x64)...' -ForegroundColor Cyan
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish $desktop -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $publish

# 3) Copy FE dist -> publish/wwwroot (Kestrel serve SPA cùng origin)
Write-Host '==> Copy Angular dist -> wwwroot...' -ForegroundColor Cyan
$dist    = Join-Path $fe 'dist/Sip.FE/browser'
$wwwroot = Join-Path $publish 'wwwroot'
if (-not (Test-Path $dist)) { throw "Không thấy dist Angular: $dist" }
New-Item -ItemType Directory -Force -Path $wwwroot | Out-Null
Copy-Item (Join-Path $dist '*') $wwwroot -Recurse -Force

# 4) Bundle DB seed (config TT05 có sẵn) -> app cài về đã có cấu hình
$seedSrc = Join-Path $root 'Sip.be.Api/sip.db'
if (Test-Path $seedSrc) {
    Write-Host '==> Bundle DB seed (sip.db)...' -ForegroundColor Cyan
    $seedDir = Join-Path $publish 'seed'
    New-Item -ItemType Directory -Force -Path $seedDir | Out-Null
    Copy-Item $seedSrc (Join-Path $seedDir 'sip.db') -Force
} else {
    Write-Host '   (Bỏ qua seed: không thấy Sip.be.Api/sip.db)' -ForegroundColor Yellow
}

# 5) Kiểm tra chứng thư CA đã ra output chưa.
#    Tên file là load-bearing: SignatureVerifier nạp theo đúng tên trong SignatureConstants,
#    thiếu file nào là nhánh chain đó null âm thầm -> MỌI file PDF bị chấm untrusted.
Write-Host '==> Kiểm tra chứng thư CA (publish/certs)...' -ForegroundColor Cyan
$crtRequired = @('rootcag2.crt', 'dcscag2.crt', 'cpg2.crt', 'rootca.crt', 'cp.crt')
$crtMissing  = $crtRequired | Where-Object { -not (Test-Path (Join-Path $publish "certs/$_")) }
if ($crtMissing) {
    Write-Host "   THIẾU chứng thư: $($crtMissing -join ', ')" -ForegroundColor Red
    Write-Host '   -> Đặt file vào Sip.be.Api/ rồi build lại, nếu không app sẽ coi mọi chữ ký là untrusted.' -ForegroundColor Red
} else {
    Write-Host "   Đủ $($crtRequired.Count)/$($crtRequired.Count) chứng thư (G1 + G2)." -ForegroundColor DarkGray
}

# 6) Đưa bộ cài trình điều khiển token vào publish/drivers.
#    Nằm TRONG thư mục app (không chỉ trong installer) vì hai lý do: bản .zip cũng phải cài được
#    driver, và chính app tự cài ngầm khi khởi động nếu máy còn thiếu (Sip.Desktop/Program.cs).
Write-Host '==> Đóng gói trình điều khiển token (publish/drivers)...' -ForegroundColor Cyan
$driverSrc = Join-Path $root 'installer/drivers'
$driverOut = Join-Path $publish 'drivers'
New-Item -ItemType Directory -Force -Path $driverOut | Out-Null
$drivers = [ordered]@{
    'bit4id_xpki_1.4.10.764-ng-user-vgca-pkimgr-bwc.exe' = 'Bit4id Universal MW - middleware bắc cầu token vào Windows Cert Store'
    'gca01-client-v2-x64-8.3.msi'                        = 'VGCA GCA01-Client v2 - trình điều khiển token Ban Cơ yếu'
    'VGCAv1_Installer_1.0.21.0402.exe'                   = 'VGCA TokenManager - trình điều khiển token đời cũ (trước 2022)'
    'MicrosoftEdgeWebView2Setup.exe'                     = 'WebView2 Runtime - nhân hiển thị giao diện'
}
$driverMissing = @()
foreach ($f in $drivers.Keys) {
    $p = Join-Path $driverSrc $f
    if (Test-Path $p) {
        Copy-Item $p (Join-Path $driverOut $f) -Force
        $mb = [math]::Round((Get-Item $p).Length / 1MB, 1)
        Write-Host ("   [OK]     {0,-6} MB  {1}" -f $mb, $drivers[$f]) -ForegroundColor DarkGray
    } else {
        $driverMissing += $f
        Write-Host ("   [THIẾU]          {0}" -f $drivers[$f]) -ForegroundColor Yellow
    }
}
if ($driverMissing) {
    Write-Host '   -> App và installer vẫn chạy nhưng sẽ KHÔNG tự cài được phần thiếu.' -ForegroundColor Yellow
    Write-Host "   -> Đặt file vào: $driverSrc" -ForegroundColor Yellow
}

# 6b) Thư mục Log đi kèm bản build. Có sẵn trong publish nên bản .zip giải ra là ghi log ngay
#     cạnh app - không phải đi tìm ở AppData. Bản cài được installer cấp quyền ghi cho thư mục này.
$logOut = Join-Path $publish 'Log'
New-Item -ItemType Directory -Force -Path $logOut | Out-Null
Write-Host "==> Tạo thư mục log: $logOut" -ForegroundColor Cyan

# 7) Biên dịch installer bằng NSIS. Chọn NSIS vì giấy phép zlib/libpng cho phép dùng
#    thương mại vô điều kiện, không license key và không chèn banner vào sản phẩm.
Write-Host '==> Biên dịch installer (NSIS)...' -ForegroundColor Cyan
$makensis = @(
    "${env:ProgramFiles(x86)}\NSIS\makensis.exe",
    "$env:ProgramFiles\NSIS\makensis.exe",
    "$env:LOCALAPPDATA\Programs\NSIS\makensis.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if ($makensis) {
    Push-Location (Join-Path $root 'installer')
    & $makensis /V2 'SIPPACK.nsi'
    $nsisExit = $LASTEXITCODE
    Pop-Location
    if ($nsisExit -ne 0) { throw "NSIS biên dịch lỗi (mã $nsisExit)." }
    $setup = Join-Path $root 'installer/installer-output/SIPPACK-Setup.exe'
    Write-Host ("   {0} ({1} MB)" -f $setup, [math]::Round((Get-Item $setup).Length / 1MB, 1)) -ForegroundColor DarkGray
} else {
    Write-Host '   Không thấy makensis.exe -> bỏ qua bước tạo installer.' -ForegroundColor Yellow
    Write-Host '   Cài NSIS (miễn phí): winget install --id NSIS.NSIS' -ForegroundColor Yellow
}

# 8) Đóng gói bản phát hành .zip, đặt cạnh các bản cũ ở thư mục gốc repo.
#    Version lấy từ chính exe đã build để zip không bao giờ lệch tên với binary bên trong.
Write-Host '==> Đóng gói bản phát hành (.zip)...' -ForegroundColor Cyan
$ver = ((Get-Item (Join-Path $publish 'SIPPACK.exe')).VersionInfo.ProductVersion -split '\+')[0]
$zip = Join-Path $repo "SIPPACK-v$ver-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
# CreateFromDirectory chứ KHÔNG dùng Compress-Archive: Compress-Archive bỏ qua thư mục rỗng, nên
# thư mục Log\ (rỗng lúc đóng gói) biến mất khỏi bản .zip - đúng thứ vừa cần thêm vào.
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $publish, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)

# Chuẩn hoá entry thư mục rỗng "Log". CreateFromDirectory trên .NET Framework ghi nó thành "Log\",
# mà chuẩn ZIP quy định dấu phân cách là "/" - vài trình giải nén hiểu "Log\" là TÊN FILE chứ không
# phải thư mục. Xoá bản sai rồi tạo lại đúng chuẩn, để giải nén ở đâu cũng ra thư mục Log.
# (App vẫn tự tạo thư mục này lúc khởi động; có sẵn trong zip là để người dùng thấy ngay chỗ chứa log.)
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Update)
@($archive.Entries | Where-Object { $_.FullName -eq 'Log\' }) | ForEach-Object { $_.Delete() }
if (-not ($archive.Entries | Where-Object { $_.FullName -eq 'Log/' })) {
    $archive.CreateEntry('Log/') | Out-Null
}
$archive.Dispose()
Write-Host ("   {0} ({1} MB)" -f $zip, [math]::Round((Get-Item $zip).Length / 1MB, 1)) -ForegroundColor DarkGray

if ($makensis) {
    $setupRelease = Join-Path $repo "SIPPACK-v$ver-win-x64-Setup.exe"
    Copy-Item (Join-Path $root 'installer/installer-output/SIPPACK-Setup.exe') $setupRelease -Force
    Write-Host ("   {0} ({1} MB)" -f $setupRelease, [math]::Round((Get-Item $setupRelease).Length / 1MB, 1)) -ForegroundColor DarkGray
}

Write-Host ''
Write-Host "==> Xong. Thư mục app: $publish" -ForegroundColor Green
Write-Host ('   Chạy thử: ' + (Join-Path $publish 'SIPPACK.exe'))
Write-Host '   Installer tự cài Bit4id MW + GCA01-Client + WebView2 nếu máy đích chưa có,'
Write-Host '   kiểm tra lại sau khi cài và báo rõ thành phần nào không cài được.'

# Đóng gói SIPPACK desktop (hướng C: .NET host WebView2 / Photino)
# Chạy: powershell -ExecutionPolicy Bypass -File build-desktop.ps1
$ErrorActionPreference = 'Stop'

$root    = Split-Path -Parent $MyInvocation.MyCommand.Path   # Sip.BE
$repo    = Split-Path -Parent $root                          # Sip
$fe      = Join-Path $repo 'Sip.FE'
$desktop = Join-Path $root 'Sip.Desktop'
$publish = Join-Path $repo 'publish'   # xuất ra thư mục gốc Sip\publish cho dễ tìm

# 0) Tắt app đang chạy (nếu có) để giải phóng file DLL trong publish/
Get-Process -Name 'SIPPACK' -ErrorAction SilentlyContinue | Stop-Process -Force
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

Write-Host "==> Xong. Thư mục app: $publish" -ForegroundColor Green
Write-Host '   Chạy thử: ' + (Join-Path $publish 'SIPPACK.exe')
Write-Host '   Tạo installer: biên dịch installer/SIPPACK.iss bằng Inno Setup.'

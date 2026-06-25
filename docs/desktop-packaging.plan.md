# Đóng gói Desktop — Hướng C (.NET host WebView2 / Photino) — Build Plan

> Mục tiêu: 1 app cài đặt Windows (.exe/.msi) "SIPPACK", mở nhanh, nhẹ, UI giữ nguyên (Angular + PrimeNG). KHÔNG dùng Electron/Node. Người dùng chỉ chạy 1 installer.

## Kiến trúc chốt
- **1 tiến trình .NET duy nhất** (project mới `Sip.Desktop`):
  1. Khởi động Kestrel (chính là `Sip.be.Api` hiện có) trên cổng local.
  2. Kestrel vừa phục vụ **API** vừa **serve Angular static** (`UseDefaultFiles` + `UseStaticFiles` + `MapFallbackToFile("index.html")`).
  3. Mở **PhotinoWindow** trỏ tới `http://127.0.0.1:<port>/` (WebView2). Đóng cửa sổ → tắt host.
- FE + API **same-origin** → bỏ cross-process, không spawn 2 tiến trình, mở app nhanh nhất.

## Decisions
- **D1 — Host = Photino.NET** (WebView2 sẵn trên Win10/11). Fallback nếu vướng: WinForms + control `WebView2`. Lý do: nhẹ, cross-webview, JS-bridge có sẵn.
- **D2 — Native dialog qua CẦU NỐI Photino (web message), KHÔNG cần endpoint API/COM.** FE gửi `pickFolder` / `openPath:<path>` qua `window.external.sendMessage`; host xử lý: `pickFolder` → `PhotinoWindow.ShowOpenFolder(...)` (dialog native sẵn có) rồi `SendWebMessage("folder:<path>")`; `openPath` → `Process.Start("explorer.exe", path)`. Lý do: Photino có sẵn dialog native, tránh COM/WinForms trong BE; FE fallback web-dev (clipboard) khi không có bridge.
- **D3 — `environment.baseUrl=''` (same-origin) cho bản desktop.** Thêm `environment.desktop.ts` (baseUrl rỗng + cờ `desktop=true`) + cấu hình build `production-desktop`. Lý do: FE gọi thẳng Kestrel host.
- **D4 — SQLite ở `%AppData%/SipPack/sip.db`** (không để trong thư mục cài, tránh quyền ghi). Set connection string lúc runtime; tạo thư mục nếu chưa có; chạy `Database.Migrate()` khi khởi động.
- **D5 — Publish self-contained win-x64**; installer Inno Setup (hoặc WiX/MSIX) + icon + tên "SIPPACK"; nhúng WebView2 bootstrapper (tự cài nếu máy thiếu).

## Steps
1. **FE desktop mode**: `environment.desktop.ts` (`baseUrl:''` same-origin) + cấu hình build `desktop` (fileReplacements) trong `angular.json`; `DesktopService` (cầu nối Photino: `isDesktop`, `pickFolder()`, `openPath()`, fallback web). `npm run build`.
2. **FE thay điểm tạm**: nút "Chọn thư mục" (check + outputPath) gọi `DesktopService.pickFolder()`; `openFolder()` gọi `DesktopService.openPath()`; ẩn ô "path TEMP" khi `isDesktop`. `npm run build`.
3. **BE serve SPA** (DONE): `Sip.be.Api` thêm `UseDefaultFiles/UseStaticFiles` + `MapFallbackToFile("index.html")`, bỏ https-redirect, `Migrate()` khi khởi động. FE `dist` copy vào `wwwroot` lúc publish.
4. **BE refactor host**: tách logic build app trong `Program.cs` thành `public static WebApplication BuildApp(string[] args)` để `Sip.Desktop` tái dùng (Program.Main vẫn chạy như cũ). Connection string lấy từ config (host inject AppData). `dotnet build`.
5. **BE SQLite AppData**: host đặt `ConnectionStrings:DefaultConnection = %AppData%/SipPack/sip.db` (auto-create thư mục); `Migrate()` đã có ở bước 3. `dotnet build`.
6. **Project `Sip.Desktop`**: ref `Sip.be.Api` + Photino.NET; `Main()`: set AppData connection string → `BuildApp` → bind cổng trống → `StartAsync` (nền) → mở `PhotinoWindow` trỏ URL + handler `WebMessageReceived` (pickFolder/openPath) → `WaitForClose` → shutdown. `dotnet build`.
7. **Publish + installer**: script `ng build --configuration desktop` (copy `dist` → `Sip.be.Api/wwwroot`) + `dotnet publish Sip.Desktop -c Release -r win-x64 --self-contained true` → Inno Setup `installer.iss` tạo `SIPPACK-Setup.exe` (icon, shortcut, nhúng WebView2 bootstrap). Chạy thử máy sạch.

## Expected output
- New: `Sip.Desktop/` (host Photino), `DesktopController` + 2 endpoint, `Sip.FE/src/environments/environment.desktop.ts`, `DesktopService`, script publish + `installer.iss`.
- Modified: `Sip.be.Api` (static SPA + fallback + tắt https-redirect + SQLite AppData), `check.ts/html` + `package-dialog` (dùng native pick/open, ẩn TEMP khi desktop).
- Untouched: nghiệp vụ Check/Pack/Config, METS/EAD generator, DB schema.

## Cần bạn chốt (trước khi build)
1. Tên app/installer & icon (mặc định "SIPPACK").
2. Cổng: cố định `5145` hay tự tìm cổng trống (an toàn hơn nếu cổng bận).
3. Có cần **single-instance** (chặn mở 2 cửa sổ) không.

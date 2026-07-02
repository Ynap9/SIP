# Desktop packaging — Approach C (.NET host + WebView2 / Photino) — Build Plan

> Goal: a single Windows installer app ("SIPPACK"), fast to open, lightweight, UI unchanged (Angular + PrimeNG).
> NO Electron/Node. The user runs a single installer.

## Chosen architecture
- **A single .NET process** (new project `Sip.Desktop`):
  1. Starts Kestrel (the existing `Sip.be.Api`) on a local port.
  2. Kestrel serves both the **API** and the **Angular static files** (`UseDefaultFiles` + `UseStaticFiles`
     + `MapFallbackToFile("index.html")`).
  3. Opens a **PhotinoWindow** (WebView2) pointing at `http://127.0.0.1:<port>/`. Closing the window shuts down the host.
- FE + API are **same-origin** → no cross-process calls, no second process, fastest startup.

## Decisions
- **D1 — Host = Photino.NET** (WebView2 ships with Win10/11). Fallback if blocked: WinForms + `WebView2` control.
  Rationale: lightweight, cross-webview, built-in JS bridge.
- **D2 — Native dialogs via the Photino BRIDGE (web message), no API/COM endpoint needed.** FE sends
  `pickFolder` / `openPath:<path>` through `window.external.sendMessage`; the host handles it: `pickFolder` →
  `PhotinoWindow.ShowOpenFolder(...)` (built-in native dialog) then `SendWebMessage("folder:<path>")`; `openPath`
  → `Process.Start("explorer.exe", path)`. Rationale: Photino already has native dialogs; avoids COM/WinForms in
  the BE; FE falls back to web-dev (clipboard) when there is no bridge.
- **D3 — `environment.baseUrl=''` (same-origin) for the desktop build.** Add `environment.desktop.ts` (empty
  baseUrl + a `desktop=true` flag) plus a `production-desktop` build configuration. Rationale: FE calls the
  Kestrel host directly.
- **D4 — SQLite at `%AppData%/SipPack/sip.db`** (not inside the install dir, to avoid write-permission issues).
  Set the connection string at runtime; create the folder if missing; run `Database.Migrate()` at startup.
- **D5 — Publish self-contained win-x64**; installer via Inno Setup (or WiX/MSIX) + icon + name "SIPPACK";
  embed the WebView2 bootstrapper (auto-install if the machine lacks it).

## Steps
1. **FE desktop mode**: `environment.desktop.ts` (`baseUrl:''` same-origin) + a `desktop` build config
   (fileReplacements) in `angular.json`; `DesktopService` (Photino bridge: `isDesktop`, `pickFolder()`,
   `openPath()`, web fallback). `npm run build`.
2. **FE replace the temporary bits**: the "Choose folder" button (check + outputPath) calls
   `DesktopService.pickFolder()`; `openFolder()` calls `DesktopService.openPath()`; hide the "TEMP path" input
   when `isDesktop`. `npm run build`.
3. **BE serve SPA** (DONE): `Sip.be.Api` adds `UseDefaultFiles/UseStaticFiles` + `MapFallbackToFile("index.html")`,
   removes https-redirect, `Migrate()` at startup. FE `dist` is copied into `wwwroot` on publish.
4. **BE host refactor**: extract the app-building logic in `Program.cs` into
   `public static WebApplication BuildApp(string[] args)` so `Sip.Desktop` can reuse it (Program.Main still runs
   as before). Connection string comes from config (the host injects AppData). `dotnet build`.
5. **BE SQLite AppData**: the host sets `ConnectionStrings:DefaultConnection = %AppData%/SipPack/sip.db`
   (auto-create the folder); `Migrate()` already added in step 3. `dotnet build`.
6. **Project `Sip.Desktop`**: reference `Sip.be.Api` + Photino.NET; `Main()`: set the AppData connection string →
   `BuildApp` → bind a free port → `StartAsync` (background) → open `PhotinoWindow` at the URL + a
   `WebMessageReceived` handler (pickFolder/openPath) → `WaitForClose` → shutdown. `dotnet build`.
7. **Publish + installer**: script `ng build --configuration desktop` (copy `dist` → `Sip.be.Api/wwwroot`) +
   `dotnet publish Sip.Desktop -c Release -r win-x64 --self-contained true` → Inno Setup `installer.iss`
   produces `SIPPACK-Setup.exe` (icon, shortcut, embedded WebView2 bootstrap). Test on a clean machine.

## Expected output
- New: `Sip.Desktop/` (Photino host), `DesktopController` + 2 endpoints,
  `Sip.FE/src/environments/environment.desktop.ts`, `DesktopService`, publish script + `installer.iss`.
- Modified: `Sip.be.Api` (static SPA + fallback + disable https-redirect + SQLite AppData), `check.ts/html` +
  `package-dialog` (native pick/open, hide TEMP on desktop).
- Untouched: the Check/Pack/Config business logic, the METS/EAD generator, the DB schema.

## To confirm (before building)
1. App/installer name & icon (default "SIPPACK").
2. Port: fixed `5145` or auto-pick a free port (safer if the port is busy).
3. Whether **single-instance** (block opening two windows) is needed.

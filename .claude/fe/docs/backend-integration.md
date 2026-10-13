# Sip.FE — Back-end Integration & Desktop Host

> **Part 3 of 3** · previous: [shared-and-conventions.md](shared-and-conventions.md) · index: [project-structure.md](project-structure.md).

## 1. Talking to `Sip.BE`

- **Base URL**: always `DesktopService.apiBase`, never `environment.baseUrl` directly and never a hardcoded string.
  `apiBase` is `isDevMode() ? environment.baseUrl : ''` — `http://localhost:5145` under `ng serve`, and the **empty
  string** in any built app, because Photino serves the SPA from the same origin as the API (section 3).
- **Envelope** — every REST call returns **HTTP 200**; the real status is in the body:
  ```ts
  // shared/models/request-paging.base.models.ts
  type IBaseResponse = { status: number; code: number; message: string }; // status: 1 = Success, 0 = Error
  type IBaseResponseWithData<T> = IBaseResponse & { data: T };
  ```
  **Gate every read on `BaseComponent.isResponseSucceed(res)`** (`res.status === 1`) before touching `res.data`.
  Paged data adds `{ items, totalItems, customData }` — note `customData`, one *m*.
- **REST**: `HttpClient` + `desktop.apiBase`. Services live in `services/`, `providedIn: 'root'`.
- JSON is **camelCase** on both sides. Keep `models/*.models.ts` diffable one-to-one against the BE DTO.

## 2. SSE — realtime progress

Every stream is a **`POST`** returning `text/event-stream`, so `EventSource` (GET-only) is unusable. Read it with
`fetch` + `ReadableStream`: decode chunks, split the buffer on `\n\n`, strip the `data: ` prefix, `JSON.parse`, and
abort with an `AbortController` on unsubscribe (swallow the abort error).

There are **no named `event:` lines** — discriminate on the payload's **`targetType`** field:

| Stream | `targetType` values |
|---|---|
| `check/ho-so-folder` | `manifest` (seeds the file rows), `folder`, `file`, `sheet`, `field`, `code`, `summary` |
| `check/multi-folder` | `manifest`, `parent` (fatal), `hoso`, `duplicate`, `summary` |
| `pack/multi-folder` | `hoso`, `parent`, `summary` |
| `sign/kiem-tra-dinh-dang`, `sign/ky-thu-muc` | `manifest`, `file`, `summary` |

Every progress DTO carries `status` (1/0), `code`, `message`, `current`/`total`, `percent`, `completed`.
Typical page shape: a `p-progressbar` + a `signal` array of rows seeded by the `manifest` event, each row showing
`pi-spin` → `pi-check-circle` / `pi-times-circle`.

## 3. BE endpoints (all under `api/core/document/`)

- `config/ho-so` | `config/van-ban` — `POST` import Excel, `GET` list.
- `check/ho-so-folder` (SSE) · `check/multi-folder` (SSE) · `check/collect-unsigned` | `collect-unsigned-multi`.
- `pack/ho-so-folder` · `pack/multi-folder` (SSE).
- `unpack/ho-so`.
- `sign/chung-thu-so` · `sign/kiem-tra-token` · `sign/kiem-tra-chu-ky` · `sign/danh-sach-file` ·
  `sign/xem-truoc` · `sign/kiem-tra-vi-tri` · `sign/kiem-tra-dinh-dang` (SSE) · `sign/ky-thu-muc` (SSE).
  PDF/A is a **warning only**: `NotPdfA` / `NotTwoLayer` come back `canSign = true` (amber tag), only `NotPdf` /
  `ReadFailed` are blocked. `verdict` keeps the file's real format; colour by `canSign` + `verdict`.
- `sign/xem-file?path=` — **raw PDF bytes, no envelope**; feed the URL straight to pdf.js, do not send it through
  `HttpClient`.
- `template-chu-ky` (`POST`/`PUT`) · `template-chu-ky/{id}` (`GET`/`DELETE`) · `template-chu-ky/find-paging` ·
  `template-chu-ky/file-mau` · `template-chu-ky/xem-anh?path=` (**raw image, no envelope** — use as `<img src>`) —
  `sign-template.service.ts`, contract [../../contracts/sign-template.contract.md](../../contracts/sign-template.contract.md).

Contracts for the sign flow live in [`../../contracts/`](../../contracts/) — read the contract, not the BE source.

## 4. Desktop host — Photino, one process, same origin

- There is **no Electron and no Tauri**, and the BE is **not** a background process spawned by a shell. `Sip.Desktop`
  calls `Sip.be.Api.Program.BuildApp()` **in-process**, starts Kestrel on `127.0.0.1:0` (OS-assigned port), and opens
  a **Photino/WebView2** window on that same URL. The built Angular app is served from the API's `wwwroot`, so FE and
  API share **one origin** — that is why `apiBase` is `''` in a build and CORS never matters at runtime.
- Native dialogs go through `window.external`, wrapped by `DesktopService`:
  `isDesktop`, `pickFolder()`, `pickFile()` (.zip), `pickImage()` (.png/.jpg/.jpeg), `openPath(path)` (returns
  `false` when there is no bridge).
  The check/pack/sign flows operate on a **local Windows folder path**, never a web upload.
- **Always keep a browser fallback** (paste the path, copy to clipboard) so `ng serve` stays usable — guard on
  `DesktopService.isDesktop`, and normalise any pasted path with `Utils.normalizeFolderPath()`.
- No SSR / SEO / deep-link assumptions; the app boots at `/` inside a webview.
- Offline-first: bundle fonts, icons, the pdf.js worker and every asset locally. The app ships to machines with no
  internet, so any CDN reference is a runtime failure, not a slow load.
- **The window scales, it does not reflow** — `LayoutService` sets CSS `zoom` on `<html>` to
  `clamp(innerWidth / 1440, 0.6, 1)` and publishes `--app-vh`. Never add a layout breakpoint that rearranges the
  page; details in [`../architecture/04-layout.md`](../architecture/04-layout.md).

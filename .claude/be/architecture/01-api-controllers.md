# API & Controllers (`Sip.be.Api`)

## Layout
```
Sip.be.Api/
├── Program.cs                       # composition root (services + pipeline)
├── Base/BaseController.cs           # [ApiController] base; OkException(ex)
└── Controllers/Documents/           # feature controllers grouped by domain area
    ├── ConfigController.cs
    ├── CheckController.cs
    ├── PackController.cs
    ├── UnpackController.cs
    └── SignController.cs
```
(`Controllers/WeatherForecastController.cs` is leftover template scaffolding — not part of the app.)

## Controller rules
- Inherit `BaseController` and pass `ILogger<T>` to `base(logger)`.
- Decorate the class with `[Route("api/core/document/<feature>")]` and `[ApiController]`.
- Inject the feature interface (e.g. `ICheckService`) via constructor; store in a `private readonly` field.
- Route segments are **kebab-case Vietnamese** (`ho-so`, `van-ban`, `ho-so-folder`).
- Keep controllers thin: validate nothing here — delegate to the service, then wrap the result.

## Standard action shape (non-streaming)
Return `ApiResponse` / `ApiResponse<T>`. Wrap the whole body in try/catch and funnel errors through `OkException`:
```csharp
[HttpGet("ho-so")]
public ApiResponse GetConfigHoSo()
{
    try { return new(_configService.GetConfigHoSo()); }   // ApiResponse(data) → 200
    catch (Exception ex) { return OkException(ex); }        // maps to error ApiResponse
}
```
Note: actions return HTTP 200 even on business errors; the real status lives in `ApiResponse.Status`/`Code` (see [06-shared.md](06-shared.md)).

**One documented exception:** `GET sign/xem-file` returns the **raw PDF bytes** (`IActionResult` + `File(...)`), not an envelope — the output feeds pdf.js in the browser, and a JSON wrapper would make it unreadable. Errors there return HTTP 404. Every other action keeps the envelope.

## BaseController.OkException(ex)
Central error translator. Inspects the exception type and builds an error `ApiResponse`:
- `UserFriendlyException` → uses its `ErrorCode` + localized/`ErrorMessages.GetMessage(code)` message.
- `DbUpdateException` / `InvalidOperationException` → `InternalServerError` + inner message.
- anything else → `InternalServerError` + generic message.
All branches log via `ILogger` with request path + query. **Do not** hand-craft error responses in controllers — always use `OkException`.

## Server-Sent Events
Five actions stream progress instead of returning a body — `check/ho-so-folder`, `check/multi-folder`, `pack/multi-folder`, `sign/kiem-tra-dinh-dang`, `sign/ky-thu-muc`. They all copy the same shape, described here for `CheckController.CheckHoSoFolder`:
- Sets headers `Content-Type: text/event-stream`, `Cache-Control: no-cache`, `Connection: keep-alive`.
- Disables response buffering (`IHttpResponseBodyFeature.DisableBuffering()`) for realtime push.
- Iterates `await foreach (var p in _checkService.CheckHoSoFolderAsync(input, HttpContext.RequestAborted))`, serializes each event as **camelCase** JSON and writes `data: {json}\n\n`, then flushes.
- Always thread `HttpContext.RequestAborted` so the client cancelling stops the work.

## Non-SSE actions on CheckController
Besides the two SSE streams, `CheckController` also exposes standard `ApiResponse` actions (try/catch → `OkException`):
- `POST check/collect-unsigned` (single) and `POST check/collect-unsigned-multi` (multi) — gom các file **chưa ký số** vào một thư mục `chưa ký số_{date}` để người dùng xem; trả `CollectUnsignedResultDto` (đường dẫn thư mục cha). FE gọi rồi `DesktopService.openPath(parentPath)`.

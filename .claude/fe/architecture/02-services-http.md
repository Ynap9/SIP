# Services & HTTP

All API access goes through root-provided services in `src/app/services/`. One service per feature, mirroring the BE controllers.

## Service convention
```ts
@Injectable({ providedIn: 'root' })
export class ConfigService {
  private http = inject(HttpClient);
  private desktop = inject(DesktopService);

  getConfigHoSo(): Observable<IBaseResponseWithData<IConfigItem[]>> {
    return this.http.get<IBaseResponseWithData<IConfigItem[]>>(
      `${this.desktop.apiBase}/api/core/document/config/ho-so`);
  }
}
```
- `providedIn: 'root'`, dependencies via `inject()` (not constructor params).
- Return **typed `Observable`s** wrapped in `IBaseResponse` / `IBaseResponseWithData<T>` (see `shared/models/request-paging.base.models.ts`) — these match the BE `ApiResponse` envelope.
- Model types come from `src/app/models/*.models.ts` and must stay in sync with BE DTOs (camelCase fields).
- **Never hardcode the host** — always prefix URLs with `this.desktop.apiBase`.

## API base URL — `DesktopService.apiBase`
```ts
get apiBase(): string { return isDevMode() ? environment.baseUrl : ''; }
```
- `ng serve` (dev) → `environment.baseUrl` (e.g. `http://localhost:5145`).
- Built app (desktop or API-served web) → **same-origin** (empty string).
- `environment*.ts` files hold `{ production, baseUrl }` typed by `IEnvironment`.

## DesktopService — the native bridge
Bridges to the Photino host via `window.external`:
- `isDesktop` — true when running inside the desktop host.
- `pickFolder(): Promise<string>` — native folder dialog (empty string if not desktop / cancelled).
- `openPath(path): boolean` — open in Explorer; returns false off-desktop so callers fall back.
- Pages must **fall back gracefully** in a browser (paste-path input / clipboard) when `isDesktop` is false.

## Server-Sent Events (CheckService)
The check endpoint streams `text/event-stream` over **POST**, so `EventSource` (GET-only) can't be used. `CheckService.checkFolder` uses `fetch` + `ReadableStream`:
- Wrap in `new Observable<ICheckProgress>`, drive an `async` reader loop.
- Split the buffer on `\n\n`, take lines starting with `data:`, `JSON.parse` the payload → `subscriber.next(...)`.
- Cancellation: return a teardown that calls `AbortController.abort()`; ignore the resulting abort error.
- Each event is an `ICheckProgress` mirroring BE `CheckProgressDto` (has `percent`, `status`, `target`, `files?`...).

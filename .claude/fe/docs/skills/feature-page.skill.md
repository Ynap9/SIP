# Skill: Add a Feature Page (Sip.FE)

Reusable steps to add any new route page. Read `docs/project-structure.md` first.

## Checklist
1. **Model** `src/app/models/<feature>.models.ts` — DTO **interfaces only**, matching BE fields (camelCase).
   Enums / label maps / constants go in `src/app/shared/constants/<feature>.constants.ts` (see `docs/shared-and-conventions.md` §5).
2. **Service** `src/app/services/<feature>.service.ts` — `@Injectable({ providedIn: 'root' })`, inject `HttpClient`, base from `environment.baseUrl`. Return `IBaseResponse` / `IBaseResponseWithData<T>`.
3. **Page** `src/app/pages/<feature>/<feature>.ts|html|scss` — standalone, `extends BaseComponent`, `imports: [...SharedImports, <shared components used>]`.
4. **Route** — add `{ path: '<path>', component: <Page> }` under `Layout.children` in `src/app.routes.ts`.
5. **Providers** — ensure `src/app.config.ts` has `provideHttpClient(withFetch())`, PrimeNG theme/animations, and `MessageService` (+ `ConfirmationService`/`DialogService` if confirms/dialogs used); add `<p-toast />` (+ `<p-confirmdialog />`) to `layout.html`.
6. `npm run build` must pass.

## Conventions
- File names: NO `.component` suffix; selector prefix `app-`.
- State: `signal()`/`input()`/`computed()`; templates `@if`/`@for`/`@switch`; inject via `inject()`.
- Pages extend `BaseComponent` → use `messageSuccess/Warning/Error`, `confirmDelete/confirmAction`, `isFormInvalid/getError`, `isResponseSucceed(res)`.
- Lists → `app-data-table` + `IColumn` + `CellViewTypes`; page header → `app-breadcrumb`.
- Formatting → `Utils`.

## Snippets

### Service
```ts
@Injectable({ providedIn: 'root' })
export class FeatureService {
  private http = inject(HttpClient);
  private base = environment.baseUrl;
  list() {
    return this.http.get<IBaseResponseWithData<IItem[]>>(`${this.base}/api/.../endpoint`);
  }
}
```

### Page calling REST (in a BaseComponent page)
```ts
this.featureService.list().subscribe(res => {
  if (this.isResponseSucceed(res)) this.rows.set(res.data);
});
```

### Data-table columns
```ts
columns: IColumn[] = [
  { header: 'STT', cellViewType: CellViewTypes.INDEX },
  { header: 'Tên', field: 'name' },
  { header: 'Bắt buộc', field: 'required', cellViewType: CellViewTypes.BOOL_CHECK },
];
```

### SSE — check endpoint (POST, text/event-stream)
EventSource is GET-only, so use `fetch` + ReadableStream for the POST SSE endpoint:
```ts
const res = await fetch(`${environment.baseUrl}/api/core/document/check/ho-so-folder`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ folderPath }),
});
const reader = res.body!.getReader();
const dec = new TextDecoder();
let buf = '';
while (true) {
  const { value, done } = await reader.read();
  if (done) break;
  buf += dec.decode(value, { stream: true });
  let i;
  while ((i = buf.indexOf('\n\n')) >= 0) {
    const line = buf.slice(0, i).trim();
    buf = buf.slice(i + 2);
    if (line.startsWith('data:')) {
      const ev = JSON.parse(line.slice(5).trim()); // CheckProgressDto
      // update UI by ev.target / ev.status / ev.completed
    }
  }
}
```

## Envelope
`IBaseResponse { status; code; message }` — `status === 1` = success (all REST returns HTTP 200). Always check before using `data`.

## Testing convention
- **Every component MUST have a `<name>.spec.ts`** next to it — minimal smoke test following the project template: `TestBed.configureTestingModule({ imports: [Component] }).compileComponents()` + `it('should create', () => expect(component).toBeTruthy())`. Do not stuff providers into the spec (providers live in `app.config.ts`).

## Nested feature routes
- A feature action can be a CHILD route, e.g. Check lives under "Đóng gói": route `dong-goi/check`. Put the child component inside the parent folder (`pages/dong-goi/check/`) and declare it in the parent's `*.router.ts` `children`; the parent renders `<router-outlet />`.

## Path input (desktop)
- Normalize a user-pasted folder path before sending to BE via `Utils.normalizeFolderPath()` (strips quotes, `\`→`/`, collapses slashes).

## Planning convention
- Plans live in `docs/plans/<feature>.plan.md`, **≤100 lines**.
- MUST contain three sections: **Requirements / Steps / Expected output**.
- Steps are ordered, small, each independently verifiable, and end with `npm run build` (BE-touching steps end with `dotnet build`).
- Add one explicit **Decision** note for any non-obvious tradeoff (e.g. component reuse vs custom).
- Brainstorm first; the plan is reviewed/approved before any build agent executes it.

## SSE realtime page pattern (desktop)
- Service: `fetch` + ReadableStream (POST) parsing `data:` lines → model; cancel via `AbortController`.
- Page: drive a `signal` for progress + a `signal<Row[]>` for per-item status; bind `p-progressbar [value]="percent()"`; the `completed` event = 100%.
- Per-row status icon: processing `pi pi-spin pi-spinner` → OK green `pi pi-check-circle` → error red `pi pi-times-circle` (+ a reason column).
- BE may emit a `manifest` event first (e.g. `TargetType="manifest"`, `files: string[]`) to seed all rows before per-item events resolve them.
- For live per-row updates prefer a dedicated `p-table` over `app-data-table` (which targets static `IColumn` data).

## SelectButton + empty-state pattern
- `p-selectbutton` (`SelectButtonModule`, not in SharedImports — import in the page) for mode toggles; branch with `@switch`/`@if` on the selected value.
- "Coming soon"/empty state: a centered muted text block.

## Folder input (desktop) pattern
- A drag-drop box PLUS a "Chọn thư mục" button wired to `DesktopService.pickFolder()`, PLUS a text input to paste an
  absolute path. Keep all three: the paste input is the fallback under `ng serve`, where `window.external` is absent.
- Guard native calls on `DesktopService.isDesktop`; normalise any pasted value with `Utils.normalizeFolderPath()`.
- The BE check works on a local absolute folder path; never upload files.

## PrimeNG modules note
`SelectButtonModule`, `ProgressBarModule`, `TableModule` are NOT in `SharedImports` — import them directly in the page that needs them (or add to `SharedImports` if broadly reused).

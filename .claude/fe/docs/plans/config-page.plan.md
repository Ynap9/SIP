# Config Page — Build Plan

## Requirements
- Route `/cau-hinh` under `Layout.children` in `src/app.routes.ts`. Sidebar label already "Cấu hình".
- Page at `src/app/pages/config/config.ts|html|scss`: standalone, `extends BaseComponent`, `imports: [...SharedImports, Breadcrumb, DataTable]`.
- Header: `app-breadcrumb` with home + "Cấu hình".
- `p-select` dropdown, 2 options: "Hồ sơ" (value `ho-so`), "Văn bản" (value `van-ban`). No selection => empty table; default-load "Hồ sơ" on init.
- On change → call API → render `app-data-table` (NO paging, ALL rows):
  - Hồ sơ → `GET {baseUrl}/api/core/document/config/ho-so`
  - Văn bản → `GET {baseUrl}/api/core/document/config/van-ban`
  - Response: `IBaseResponseWithData<IConfigItem[]>`, success when `status === 1`.
- Columns (`IColumn`): INDEX "STT"; `truongThongTin` "Trường thông tin"; `moTa` "Mô tả"; `batBuoc` "Bắt buộc" (`CellViewTypes.BOOL_CHECK`).
- Service `src/app/services/config.service.ts` (`providedIn:'root'`, `HttpClient` + `environment.baseUrl`): `getConfigHoSo()`, `getConfigVanBan()`.
- Model `src/app/models/config.models.ts`: `IConfigItem { id; truongThongTin; moTa; batBuoc; createdDate?; modifiedDate?; deletedDate?; deleted }`.
- Errors via `BaseComponent.isResponseSucceed(res)` / `messageError`.

### No-paging decision
Add optional `hidePaginator = input<boolean>(false)` to `DataTable`; wrap the bottom paginator block in `@if (!hidePaginator())`. Backward-compatible (default keeps current behaviour). Config page passes `[hidePaginator]="true"` and `[pageSize]="rows().length"` so the inner `p-table` ([paginator]="false") shows every row.

## Steps
1. **Model** — create `models/config.models.ts` with `IConfigItem`. Verify: `npm run build`.
2. **Service** — `ConfigService` injecting `HttpClient`; two GETs returning `Observable<IBaseResponseWithData<IConfigItem[]>>` to `${environment.baseUrl}/api/core/document/config/{ho-so|van-ban}`. Verify: compiles.
3. **data-table** — add `hidePaginator = input(false)`; guard bottom block with `@if (!hidePaginator())`. Verify: existing usages unaffected.
4. **Page** — `Config` component: signals `selected`, `rows`, `columns`, `loading`. Template: breadcrumb, `p-select` (options Hồ sơ/Văn bản), `app-data-table [data]="rows()" [columns]="columns" [hidePaginator]="true"`. `onSelect()` → service call → `isResponseSucceed` ? `rows.set(res.data)` : `messageError`. `ngOnInit` default-selects `ho-so`. Verify: compiles.
5. **Route** — add `{ path: 'cau-hinh', component: Config }` under `Layout.children`. Verify: navigate renders page.
6. **Providers** — add `provideHttpClient(withFetch())` + `MessageService` to `src/app.config.ts`; add PrimeNG theme/animations provider if missing (`providePrimeNG`, `provideAnimationsAsync`); add `<p-toast />` to `layout.html`. Verify: toast + data load work.
7. `npm run build` passes; manual check: each option loads its table, all rows shown, no paginator UI.

## Expected output
- New files: `pages/config/config.ts|html|scss`, `services/config.service.ts`, `models/config.models.ts`.
- Edited: `shared/components/data-table` (hidePaginator), `src/app.routes.ts`, `src/app.config.ts`, `layout/layout.html` (toast).
- `/cau-hinh` shows breadcrumb + dropdown; choosing Hồ sơ/Văn bản renders a full unpaged `app-data-table` from the matching GET API; errors surface via toast.

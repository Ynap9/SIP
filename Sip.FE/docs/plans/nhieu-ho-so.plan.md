# Multi-record ("Nhiều hồ sơ") check & package screen — Build Plan

> Builds the `multi` mode of `/dong-goi`: check a PARENT folder of many hồ sơ, show a two-level expandable table
> (hồ sơ → files), then package. Mirrors the single `pages/dong-goi/check` flow. Requires small BE enrichments first.

## Requirements
- New FE screen under the `dong-goi` parent, route `dong-goi/multi` (sibling of `dong-goi/check`), lazy via
  `dong-goi.router.ts`. Full-width, Times New Roman, extends `BaseComponent`.
- Drag-drop box for the PARENT folder (desktop absolute path via `DesktopService`) + a TEMP paste-path input for
  web-dev (mark `REMOVE on desktop build`; normalize with `Utils.normalizeFolderPath`) + two buttons "Kiểm tra"
  / "Đóng gói" (right-aligned) + a `p-progressbar` (`percent = completedHoSo/totalHoSo`).
- "Kiểm tra" → SSE `POST api/core/document/check/multi-folder` (`MultiCheckProgressDto`). A `manifest` event seeds
  all hồ sơ rows (spinner); each hồ sơ event resolves its row; the `summary` event completes the bar at 100%.
- Result = expandable PrimeNG `p-table`: parent rows = hồ sơ with **Trạng thái** (processed: spinner→green check),
  **Tình trạng** (verdict: green check / red X), **Nguyên nhân lỗi**. A small **circular chevron-down** button next
  to each hồ sơ name toggles a nested child `p-table` of that hồ sơ's files with the SAME three columns.
- "Đóng gói" (enabled once check `completed`, even if some hồ sơ failed) → popup (outputPath REQUIRED + maPhong +
  nguoiTao + optional maxDegreeOfParallelism) → SSE `POST api/core/document/pack/multi-folder`
  (`MultiPackProgressDto`) → packing progress → success popup: output parent folder path (openable via
  `DesktopService.openPath`) + per-hồ sơ zip results (each openable).
- **Only the check-PASSED hồ sơ are packed** (failed ones skipped): the FE sends the list of passed hồ sơ folder
  names; the BE packs only those.

## Decisions
- **D1 — BE returns per-file rows.** The current `MultiCheckProgressDto` only has a flat `Errors: string[]` per
  hồ sơ; deriving per-file rows on the FE from error strings is fragile. So enrich the BE to attach a structured
  `Files: [{ name, status, reason }]` to each hồ sơ event, computed inside `InspectHoSo` (which already knows each
  attachment file and which errors reference it).
- **D2 — Manifest event.** Add a `manifest` event (mirrors single) carrying all child hồ sơ folder names so the FE
  shows every parent row immediately; results tick in as they complete. Emit it right after Phase-1 enumerates children.
- **D3 — Row expansion via PrimeNG.** Use `p-table [dataKey]="'name'"` + `pTemplate="expansion"` for the nested file
  table; the chevron is a custom circular button toggling `expandedRowKeys` (do not rely on the built-in expander
  column, to control styling). Keeps `app-data-table` untouched (it targets static `IColumn` data).
- **D4 — Pack the passed hồ sơ (allowlist), skip failed.** Do NOT hard-require all-ok; enable "Đóng gói" on
  `completed()`. But "skip failed" means the FE sends an **allowlist** of check-passed hồ sơ folder names and BE
  packs only those — so `PackMultiFolderRequestDto` + `PackMultiFolderAsync` need an optional `HoSoNames` filter
  (empty/null = pack all). The success popup lists the packed hồ sơ per zip.
- **D5 — New models/services, not reused from single.** Multi has different DTOs (per-hồ sơ + nested files); keep
  `check.service.ts`/`pack.service.ts` (single) intact and add `multi-*` siblings. Reuse the single's SSE
  fetch+ReadableStream+`AbortController` pattern verbatim.

## Steps

### Part A — BE prerequisites (per-file data + manifest)
1. **Per-file DTO** — add `FileCheckResultDto { string Name; StatusCodeE Status; string? Reason; }` in
   `Sip.be.Application/Document/Dtos/Check/`. In `MultiCheckProgressDto.cs` add `List<FileCheckResultDto>? Files`
   (per-hồ sơ event) and `List<string>? HoSoNames` (manifest event). `dotnet build`.
2. **Constant** — add `TargetTypeManifest = "manifest"` to `Sip.be.Shared/Constants/Check/CheckConstants.cs`.
   `dotnet build`.
3. **Enrich `HoSoCheckResult` + `InspectHoSo`** (`CheckService.cs`): add `List<FileCheckResult> Files` to the
   nested `HoSoCheckResult`; in `InspectHoSo`, seed one `FileCheckResult{Name, Ok=true}` per attachment file, and
   when adding a file-referencing error (invalid extension; missing/extra doc-code match; Van_ban per-row field
   errors whose `rowLabel` == docCode == file-name-without-ext) mark that file `Ok=false` + set `Reason`
   (first error wins). Hồ sơ-level errors (structure, mã hồ sơ mismatch) stay in `Errors` only. `dotnet build`.
4. **Emit manifest + attach files** (`CheckMultiFolderAsync`): after enumerating `childDirs`, yield one event
   `{ Phase=StructureValidation, TargetType="manifest", HoSoNames=<all child folder names>, TotalHoSo }`. In the
   Phase-2 per-hồ sơ event set `Files = r.Files`. Phase-1 invalid-hồ sơ events keep `Files=null` (or empty).
   `dotnet build`. Manual: SSE emits manifest first, then per-hồ sơ with `files[]`, then summary.
5. **Pack allowlist + failed list (BE)** — (a) add optional `List<string>? HoSoNames` to
   `PackMultiFolderRequestDto`; in `PackMultiFolderAsync`, if non-empty, filter `childDirs` to only those whose
   folder name is in the set (case-insensitive) before the parallel loop (null/empty = pack all, unchanged).
   (b) add `List<string>? FailedHoSo` to `MultiPackProgressDto`; collect the failed hồ sơ folder names in a
   thread-safe bag (e.g. `ConcurrentBag<string>`) inside the worker's catch, and populate `FailedHoSo` on the
   final **summary** event (alongside `OkCount`/`ErrorCount`/`OutputParentPath`). Keep per-hồ sơ error events.
   `dotnet build`. Untouched: `PackOneHoSo`, single Pack.

### Part B — FE build
6. **Models** `src/app/models/multi-check.models.ts`: `IMultiCheckProgress` (mirror the DTO incl. `hoSoNames?`,
   `files?`, `phase`, `hoSoName`, `status`, `message`, `errors?`, `completed`, `percent`, counts). `IHoSoRow`
   `{ name; status:'processing'|'ok'|'error'; verdict:'ok'|'error'|'pending'; reason?; files:IFileRow[]; }` reusing
   `IFileRow` from `check.models.ts` (or redefine locally). `npm run build`.
7. **Service** `src/app/services/multi-check.service.ts`: `checkMulti(parentFolderPath)` → `Observable<IMultiCheckProgress>`
   via `fetch` + ReadableStream + `AbortController` to `${desktop.apiBase}/api/core/document/check/multi-folder`
   (copy `check.service.ts`). `npm run build`.
8. **Pack models/service** `models/multi-pack.models.ts` (`IMultiPackRequest{parentFolderPath, outputPath,
   maPhong?, nguoiTao?, maxDegreeOfParallelism?, hoSoNames?}` — `hoSoNames` = allowlist of passed hồ sơ folder
   names, `IMultiPackProgress` mirroring `MultiPackProgressDto`) + `services/multi-pack.service.ts`
   (SSE to `pack/multi-folder`). `npm run build`.
9. **Screen** `src/app/pages/dong-goi/multi/multi.ts|html|scss` (`app-multi`, `extends BaseComponent`,
   `imports:[...SharedImports, ProgressBarModule, TableModule]`): drop box + TEMP input + 2 buttons + progressbar;
   signals `folderPath`, `running`, `percent`, `completed`, `hoSoRows`, `summary`, `expandedRowKeys`,
   `showPackageDialog`. `handleEvent` seeds rows from `manifest`, resolves each `hoso` event (status→ok/error,
   verdict, reason=`errors?.join(' | ')`, files→child rows), and finalizes on `summary`. `+ multi.spec.ts`.
   `npm run build`.
10. **Nested file table + chevron** in `multi.html`: parent `p-table [value]="hoSoRows()" dataKey="name"
   [expandedRowKeys]`; body row shows the circular chevron button (`pi pi-chevron-down`, rounded) toggling
   expansion + the 3 status columns; `pTemplate="expansion"` renders an inner `p-table [value]="row.files"` with
   the same 3 columns. Status icons: processing `pi pi-spin pi-spinner` → ok `pi pi-check-circle` (green) → error
   `pi pi-times-circle` (red). `npm run build`.
11. **Pack popup** `src/app/pages/dong-goi/multi/package-multi-dialog/package-multi-dialog.ts|html|scss` (+ spec),
    following `docs/skills/dialog-action.skill.md`: inputs `parentFolderPath` + `passedHoSoNames: string[]`
    (the check-passed hồ sơ, sent as `hoSoNames`), `[(visible)]`; form (outputPath required + maPhong + nguoiTao).
    Do NOT expose `maxDegreeOfParallelism` in the UI — the BE auto-computes it from `Environment.ProcessorCount`
    (the machine running the desktop app), so the user never enters a thread count. `onSubmit` → SSE pack progress
    signal → success `p-dialog` listing
    `outputParentPath` + per-hồ sơ `{ hoSoName, zipPath }` (click → `openFolder`) + a **"Hồ sơ lỗi" section**
    showing `failedHoSo` (the failed folder names from the summary event). `npm run build`.
12. **Wire the container** — in `dong-goi.router.ts` add `{ path: 'multi', component: Multi }`; in `dong-goi.ts`
    `onModeChange('multi')` navigates to `['multi']` (instead of rendering `Empty` inline) and `'single'` →
    `['check']`; update `dong-goi.html`/imports (drop `Empty` usage if unused). `npm run build`.
13. Manual verify: pick parent → Kiểm tra → all hồ sơ rows appear (spinner) → tick to ok/error with reasons →
    chevron expands file sub-table → Đóng gói → popup (only passed hồ sơ packed) → SSE progress → success popup
    paths open. Final `npm run build`.

## Expected output
- **BE new/modified:** `Dtos/Check/FileCheckResultDto.cs` (new); `MultiCheckProgressDto.cs` (+`Files`,+`HoSoNames`);
  `CheckConstants.cs` (+`TargetTypeManifest`); `CheckService.cs` (`HoSoCheckResult.Files`, `InspectHoSo` per-file
  attribution, `CheckMultiFolderAsync` manifest + `Files` on hồ sơ events); `PackMultiFolderRequestDto.cs`
  (+`HoSoNames` allowlist); `MultiPackProgressDto.cs` (+`FailedHoSo`); `PackService.PackMultiFolderAsync`
  (filter childDirs by allowlist + collect failed hồ sơ into the summary event).
  Untouched: single Check/Pack flow, `PackOneHoSo`, Unpack.
- **FE new:** `models/multi-check.models.ts`, `models/multi-pack.models.ts`, `services/multi-check.service.ts`,
  `services/multi-pack.service.ts`, `pages/dong-goi/multi/multi.{ts,html,scss,spec.ts}`,
  `pages/dong-goi/multi/package-multi-dialog/*` (+spec).
- **FE modified:** `pages/dong-goi/dong-goi.router.ts` (+`multi` route), `dong-goi.ts` + `dong-goi.html`
  (mode switch navigates to `multi`).
- Result: `/dong-goi` "Nhiều hồ sơ" mode checks a parent folder with realtime progress, an expandable
  hồ sơ→files status table, and a batch packaging popup with openable output paths.

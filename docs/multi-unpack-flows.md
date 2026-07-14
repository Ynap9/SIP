# Multi check/pack & Unpack flows

> The **batch** (multi hồ sơ) and **reverse** (unpack) flows — beyond the single Config/Check/Pack flows in
> `.claude/be/architecture`. Source of truth is the code: `CheckService`, `PackService`, `UnpackService`.
> Related: [chuan-dong-goi-sip-tt05.md](chuan-dong-goi-sip-tt05.md),
> [bug/pack-multi-missing-shared-excel-structure.md](bug/pack-multi-missing-shared-excel-structure.md).

## 1. Placement
FE menu has three working areas: **Kiểm tra & đóng gói** (`/dong-goi`, single + multi modes via
`DongGoiStateService.mode`), and **Bóc tách dữ liệu** (`/boc-tach`, unpack). `/dong-goi` has child routes
`check` (1 hồ sơ) and `multi` (N hồ sơ); both use `data.reuse` + a route-reuse strategy so state survives back-nav.
No DB writes in any of these flows (only Config persists).

## 2. The three parent-folder structures (multi)
`CheckService` **and** `PackService` must both recognize all three (keep them in sync — see the bug doc).
Detection: `ResolveSharedExcelPath(parent)` → shared Excel in `parent/metadata` (loại 3) else directly in `parent`
(loại 2); `null` ⇒ loại 1.

| Kind | Per-hồ sơ layout | Excel location |
|---|---|---|
| **loại 1** | `{hoso}/metadata/` (Excel) + `{hoso}/attachment/` (files) | one Excel **per hồ sơ** |
| **loại 2** | `{hoso}/` holds data files **directly** | one **shared** Excel in the **parent** |
| **loại 3** | `{hoso}/` holds data files **directly** | one **shared** Excel in **parent/`metadata`** |

For loại 2/3 each hồ sơ's rows are sliced from the shared sheets: `Ho_so` row where `Mã hồ sơ` = folder name;
`Van_ban` rows whose `Mã định danh tài liệu` starts with `"{maHoSo}."` (longest-prefix match). File identity is
always `docId` = data-file name without extension = `Mã định danh tài liệu`.

## 3. Multi check — `POST api/core/document/check/multi-folder` (SSE)
`CheckMultiFolderAsync` streams `MultiCheckProgressDto` (one event **per hồ sơ**, not per field, to avoid event
floods). Two phases:
- **Phase 1 (StructureValidation, sequential):** validate the parent folder + duplicates (duplicate hồ sơ folder
  names, duplicate file names). Parent-level error ⇒ stop immediately (fail-fast). A child with a structure error is
  marked failed but the batch continues.
- **Phase 2 (Processing, parallel, thread-limited):** validate each valid hồ sơ's content + detect duplicate mã hồ
  sơ. First event is a `manifest` (`hoSoNames[]`) so FE seeds all rows; final event `TargetType=summary` totals
  `okCount`/`errorCount`. `TargetType` ∈ `parent | hoso | duplicate | summary | manifest`. Per-file signature info
  rides in `files[].signature`.

## 4. Multi pack — `POST api/core/document/pack/multi-folder` (SSE)
`PackMultiFolderAsync` detects the structure like check: loại 1 → `PackOneHoSo` (per-hồ sơ `metadata/`+`attachment/`);
loại 2/3 → `PackMultiSharedExcelAsync` (shared Excel parsed **once** into thread-safe row dictionaries, then
`PackOneHoSoShared` + `BuildSimpleDcFromCells`; data files taken directly from each folder). Config is loaded once
before the parallel fan-out. Output: one parent folder `"{parentName}_{timestamp}"` inside `OutputPath`, one ZIP per
hồ sơ (each ZIP = a plain lowercase GUID). Streams `MultiPackProgressDto` per hồ sơ; failed hồ sơ are marked and the
batch continues; final `summary` event carries `outputParentPath` + `failedHoSo[]`.

## 5. Unpack (bóc tách) — `POST api/core/document/unpack/ho-so`
`UnpackOneAsync` reverses one SIP package (a `.zip` **or** an extracted package folder — auto-detected; zip is
extracted to a temp dir, cleaned up in `finally`) back into a hồ sơ folder. Steps:
1. `FindPackageRoot` — locate the folder holding both `metadata/` and `representations/` (searches up to 2 levels).
2. Read `metadata/descriptive/EAD.xml` (record) + every `representations/rep1/metadata/descriptive/*.xml` (per doc).
3. Rebuild `{OutputPath}/{fileCode}/` with `Metadata/` + `Attachment/` (capitalized, matching the sample template).
4. Write `{fileCode}.xlsx` (sheets `Ho_so` + `Van_ban`): headers come from **ConfigHoSo/ConfigVanBan** mapped via
   `EadFieldMapping`; required fields always get a column, optional only when the package has data; enum codes are
   expanded `"01"` → `"01: label"` from the config `MoTa`. Config must be present (else `ConfigEmpty`).
5. Copy `representations/rep1/data/*` → `Attachment/`.
Returns `UnpackResultDto` with a `FileNodeDto` **tree** (folder→file, absolute paths) for the FE library view.
`UnpackOneCore` is DB-free (config passed in) so it can be reused for a future multi-unpack.

## 6. FE surface
- `services/multi-check.service.ts`, `multi-pack.service.ts` — `fetch`+ReadableStream POST SSE (same as single check).
- `pages/dong-goi/multi/` (+ `package-multi-dialog/`) — batch table (hồ sơ rows with nested file rows,
  `IMultiCheckProgress`/`IHoSoRow`/`IFileRow`), then multi-pack dialog.
- `pages/boc-tach/` + `services/unpack.service.ts` — pick source zip/folder + output, show result tree via
  `shared/components/file-tree/`. Native pick/open through `DesktopService`.

## 7. Quirks / gotchas
- **Check and pack must share the structure set.** A "multi-check OK but multi-pack fails" for the same input means
  a structure branch is missing in one service (grep `ResolveSharedExcelPath` — it must exist in both).
- Multi events are **per hồ sơ** (percent = `completedHoSo/totalHoSo`); single-check events are per file/field.
- Unpack output dirs are `Metadata`/`Attachment` (capitalized); the check/pack input dirs are `metadata`/`attachment`
  (lowercase, from `CheckConstants`) — the round trip is case-different by design (matches the sample template).
- ClosedXML `IXLRow` is not thread-safe; the parallel pack reads pre-parsed dictionaries, never live rows.

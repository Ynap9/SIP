# BUG: Pack-multi only handles loại 1; shared-Excel batches (loại 2/3) fail

> **Severity: HIGH — a batch that passes multi-check with a shared Excel cannot be packed.**
> Affects **PackService** (`PackMultiFolderAsync` + `PackOneHoSo`). Read before touching the pack-multi flow.
> Related: [pack-file-lookup-by-doccode-instead-of-docid.md](pack-file-lookup-by-doccode-instead-of-docid.md),
> [../chuan-dong-goi-sip-tt05.md](../chuan-dong-goi-sip-tt05.md).

## 1. Summary
`CheckService` recognizes **three** parent-folder structures for multi-check; `PackService` only handled the
**first**. So a parent folder that passes multi-check via a **shared Excel** produces one `MetadataFolderMissing`
error per hồ sơ when packed — no ZIP is ever written, even though check said the batch was valid.

## 2. The three structures (source of truth: `CheckService`)
| Kind | Per-hồ sơ layout | Excel location | Check path |
|---|---|---|---|
| **loại 1** | `{hoso}/metadata/` (Excel) + `{hoso}/attachment/` (files) | one Excel **per hồ sơ** | `CheckMultiFolderAsync` → `InspectHoSo` |
| **loại 2** | `{hoso}/` holds data files **directly** | one **shared** Excel in the **parent** | `CheckMultiSharedExcelAsync` → `InspectHoSoShared` |
| **loại 3** | `{hoso}/` holds data files **directly** | one **shared** Excel in **parent/`metadata`** | same shared path |

Detection: `ResolveSharedExcelPath(parent)` — shared Excel in `parent/metadata` (loại 3) else directly in `parent`
(loại 2); `null` ⇒ loại 1. For loại 2/3 each hồ sơ's rows are sliced from the shared sheets: `Ho_so` row where
`Mã hồ sơ` = folder name; `Van_ban` rows whose `Mã định danh tài liệu` starts with `"{maHoSo}."` (longest prefix).

## 3. Root cause
`PackMultiFolderAsync` calls `PackOneHoSo(folder, …)` directly for every child, and `PackOneHoSo` is hard-wired to
loại 1:
```csharp
metadataDir   = folder/metadata      // null ⇒ throw MetadataFolderMissing
attachmentDir = folder/attachment    // null ⇒ throw AttachmentFolderMissing
excelPath     = metadataDir/*.xlsx   // reads a PER-HỒ-SƠ Excel
```
Loại 2/3 hồ sơ folders have **no** `metadata/`, **no** `attachment/`, and **no** per-hồ sơ Excel (data files sit
directly in the folder; the Excel is shared at parent level). So `PackOneHoSo` throws immediately. The shared-Excel
parsing/slicing logic existed **only** in `CheckService`, never in `PackService`.

## 4. Evidence
- `CheckService` has `ResolveSharedExcelPath`, `CheckMultiSharedExcelAsync`, `TryParseSharedExcel`,
  `InspectHoSoShared`; `PackService` had none of these — `PackMultiFolderAsync` has a single loại-1 loop.
- Packing a loại 2/3 batch (which `CheckMultiFolderAsync` marks OK) yields a `MultiPackProgressDto` with
  `Code = MetadataFolderMissing` for every hồ sơ and an empty output folder.

## 5. Fix (implemented)
Mirror the check flow in `PackService` (same duplication style as `InspectHoSo` vs `InspectHoSoShared`):
- `ResolveSharedExcelPath` + `TryParseSharedExcel` + `BuildRowCells` + `GetCellValue` — parse the shared Excel once
  into per-row `Dictionary<normalizedHeader,value>` (thread-safe; workbook closed before packing).
- `BuildSimpleDcFromCells(cells, fields, map)` — a cells-based twin of `BuildSimpleDc(IXLRow, …)` so EAD is built
  from a dict instead of a live `IXLRow` (ClosedXML rows are not thread-safe across the parallel pack).
- `PackOneHoSoShared(folder, hoSoRow, vanBanRows, …)` — data files taken **directly** from the folder; matched by
  `docId` = file name (no ext) = `Mã định danh tài liệu`, exactly like loại 1.
- `PackMultiFolderAsync` branches to `PackMultiSharedExcelAsync` when `ResolveSharedExcelPath(parent) != null`.

File identity stays `docId` (see the docId/docCode bug). Single-hồ-sơ pack (`PackHoSoFolderAsync`) is **unchanged** —
a single folder is loại 1 by definition (it must have `metadata/` + `attachment/` to be checked).

## 6. How to prevent
- **Keep check and pack on the same structure set.** If `CheckService` learns a new folder layout, `PackService`
  must learn it too, or a batch that passes check silently fails to pack. Grep for `ResolveSharedExcelPath` — every
  such branch should exist in **both** services.
- A "successful multi-check + failing multi-pack" for the same input is the tell-tale of a structure mismatch.

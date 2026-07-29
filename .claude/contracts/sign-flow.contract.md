# Contract — sign flow (`ky-thu-muc` SSE)

> **Superseded in part, 2026-07-28:** `SignRequestDto` is no longer "unchanged" — it gained `useCustomPosition`,
> `positions`, `fileOverrides`, `allowOverlapContent`. See [sign-placement.contract.md](sign-placement.contract.md).
> Everything below about the SSE stream, the three per-file states and the summary still holds.

> Additive changes, 2026-07-21. `SignFileResultDto` gained `signedTime` + `skipped`; `SignResultDto` gained
> `skippedCount`. **No field renamed or removed**, so existing FE code keeps working; new fields are optional.
>
> **Update 2026-07-21 (output location).** The sign flow no longer creates a new output folder. Each signed file is
> now written **next to its source file** in the source folder itself, as a **new** file named
> `<name-without-ext>_daky_<yyyyMMdd_HHmmss><ext>` (e.g. `vanban.pdf` → `vanban_daky_20260721_143005.pdf`). The
> timestamp is taken **once** at the start of the batch (VietnamNow) and shared by every file in the batch. The
> original files are left untouched. `SignRequestDto` is unchanged — it still has no output-path field
> (`thumbprint` / `sourcePath` / `reason` / `location` only). No field renamed/removed; `outputFolder` and
> `outputPath` keep their names and types, only their meaning is refined (see below).

## Where the FE receives it

| Method | Route | Request | Response | Notes |
|---|---|---|---|---|
| POST | `api/core/document/sign/ky-thu-muc` | `SignRequestDto` | SSE stream of `SignProgressDto` | `text/event-stream` |

Each SSE `data:` line is one `SignProgressDto`. `targetType` = `manifest` (seed file list) · `file` (one file
processed) · `summary` (batch total). `SignFileResultDto` arrives as the `file` field of a `targetType = file` event.

## Three per-file states (BE gates format before signing)
Before signing each file the BE runs the PDF/A two-layer inspector (defense-in-depth: a non-conforming file is
**never** signed, even if the FE forgot to pre-check). Each file ends in exactly one of three states — **do not merge
skipped into failed**:

| State | `success` | `skipped` | Meaning |
|---|---|---|---|
| **Đã ký** (signed) | `true` | `false` | Signed OK. `outputPath` = the new `<name>_daky_<yyyyMMdd_HHmmss><ext>` file written **next to the source file**; `signedTime`, `trustedTime` populated. |
| **Bỏ qua** (skipped) | `false` | `true` | Not signed — failed the PDF/A two-layer gate. `reason` = why. Not an error. |
| **Lỗi** (failed) | `false` | `false` | Signing was attempted and errored (locked file, token pulled…). `reason` = error. |

## `SignProgressDto.file` shape (camelCase, as received)
```json
{
  "fileName": "vanban.pdf",
  "outputPath": "C:\\...\\vanban_daky_20260721_143005.pdf",
  "success": true,
  "skipped": false,
  "reason": null,
  "verdict": 0,
  "verifyReason": null,
  "trustedTime": "21/07/2026 09:15:03",
  "signedTime": "21/07/2026 09:15:01"
}
```

| Field | Type | Meaning |
|---|---|---|
| `skipped` | `boolean` | `true` = file bỏ qua vì không đạt PDF/A hai lớp (not signed, not an error). Branch on this to tell skipped from a real signing failure. |
| `signedTime` | `string \| null` | **Giờ ký (Time ký)** — signingTime the signer wrote into the CMS. `null` unless signed. |
| `trustedTime` | `string \| null` | **Dấu thời gian** — the TSA `genTime` (raw UTC). `null` unless signed. |

Both times use `dd/MM/yyyy HH:mm:ss`; the FE renders them as two separate columns without reformatting.
On a skipped file the SSE event carries `status = 0` and `code = ErrorCodes.PdfFormatInvalid`; still tell it from a
failure via `file.skipped`, not via `status`.

## `SignProgressDto.summary` shape (`targetType = summary`)
```json
{
  "outputFolder": "C:\\...\\hoso",
  "totalFiles": 5,
  "successCount": 3,
  "skippedCount": 1,
  "failedCount": 1
}
```

| Field | Type | Meaning |
|---|---|---|
| `outputFolder` | `string` | The **source folder** where the signed `_daky_` files were written alongside the originals (no new folder is created). For a single-file source it is the folder that contains that file. Open this to show the results. |
| `successCount` | `number` | Files **signed** (state Đã ký). |
| `skippedCount` | `number` | Files **skipped** by the format gate (state Bỏ qua). |
| `failedCount` | `number` | Files that **errored** while signing (state Lỗi). |

`successCount + skippedCount + failedCount = totalFiles`. All-skipped (`successCount = 0`) is a normal outcome, **not**
a system error — the stream completes normally and no exception is thrown.

## Enums (serialised as NUMBERS)
`TimestampVerdict` (`verdict`): 0 ValidTrusted · 1 NoTimestamp · 2 UntrustedTsa · 3 TimeMismatch · 4 NotApplicable ·
5 UntrustedSigner · 6 InvalidSignature

## Unchanged
`fileName`, `success`, `reason`, `verdict`, `verifyReason`, `trustedTime`, `totalFiles`, `successCount`,
`failedCount` keep their names, types and semantics. `outputPath` and `outputFolder` keep their **names and
types**; only their meaning is refined per the 2026-07-21 output-location update above (signed file written next to
the source; `outputFolder` = the source folder). `SignRequestDto` is unchanged (no output field).

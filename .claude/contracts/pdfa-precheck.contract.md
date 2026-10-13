# Contract — PDF/A two-layer pre-check (sign gate)

> A standalone SSE endpoint the FE calls BEFORE `ky-thu-muc`. It inspects every file in the source and reports,
> per file, its format. **PDF/A is a warning, not a block** (same as `kssm.be`): a readable PDF that is not a
> two-layer PDF/A is still signed. Only non-PDF and unreadable files are blocked; the BE re-runs this gate during
> `ky-thu-muc`, so a blocked file that slips through is skipped, never signed.

| Method | Route | Request | Response | Notes |
|---|---|---|---|---|
| POST | api/core/document/sign/kiem-tra-dinh-dang | PdfFormatCheckRequestDto | SSE PdfFormatCheckProgressDto | text/event-stream |

- SSE shape is identical to `ky-thu-muc`: HTTP 200, `data: {json}\n\n` per event, camelCase JSON.
- Non-PDF files ARE reported here (verdict `NotPdf`) — unlike `GET sign/danh-sach-file` which silently drops them.
- On source not found the stream throws (no events) — same behaviour as `ky-thu-muc`.

## Request (JSON, camelCase)
```json
{ "sourcePath": "C:\\ho-so\\VT.01\\data" }
```
`sourcePath` = one file OR one folder. A folder inspects all top-level files (not recursive).

**2026-10-13:** the `allowNonTwoLayerPdf` flag (added 2026-08-10) was **removed** from this request and from
`SignRequestDto`. Its relaxed behaviour is now the only behaviour.

| `canSign = true` | Blocked |
|---|---|
| `Valid`, `NotPdfA` (warning), `NotTwoLayer` (warning) | `NotPdf`, `ReadFailed` |

`verdict` keeps its real value so the FE shows the true format; `summary.validCount` / `invalidCount` / `allValid`
count by `canSign`.

## Event stream (each event = one `PdfFormatCheckProgressDto`)
`targetType` drives which optional block is present: `manifest` → `files`, `file` → `file`, `summary` → `summary`.

```jsonc
// 1) manifest — sent once, seed the rows
{ "targetType": "manifest", "target": "<sourcePath>", "status": 1, "code": 200,
  "message": "Chuẩn bị kiểm tra định dạng 3 file.", "current": 0, "total": 3,
  "files": ["a.pdf", "b.pdf", "c.docx"] }

// 2) file — one per inspected file
{ "targetType": "file", "target": "c.docx", "status": 0, "code": 1036,
  "message": "Chưa thể ký số với file khác định dạng PDF.", "current": 3, "total": 3,
  "percent": 100,
  "file": { "fileName": "c.docx", "fullPath": "C:\\...\\c.docx", "verdict": 1,
            "canSign": false, "message": "Chưa thể ký số với file khác định dạng PDF.",
            "pdfAPart": null, "pdfAConformance": null } }

// 3) summary — sent once, last
{ "targetType": "summary", "target": "<sourcePath>", "status": 0, "code": 1036,
  "message": "Kiểm xong: 2 file ký được, 1 file không ký được.", "completed": true,
  "current": 3, "total": 3,
  "summary": { "totalFiles": 3, "validCount": 2, "invalidCount": 1, "allValid": false } }
```
`status`: 1 = ok, 0 = error. `code`: 200 when ok, `1036` (PdfFormatInvalid) when a file/batch is not signable.
`percent` is computed (0–100) from `current`/`total`.

## Enum `verdict` (serialised as NUMBERS)
| Value | Name | Meaning | `canSign` | Message |
|---|---|---|---|---|
| 0 | Valid | two-layer PDF/A | true | PDF/A hai lớp hợp lệ, ký được. |
| 1 | NotPdf | no `%PDF-` header | false | Chưa thể ký số với file khác định dạng PDF. |
| 2 | NotPdfA | PDF but not PDF/A | true | Cảnh báo: file không phải PDF/A, vẫn ký được. |
| 3 | NotTwoLayer | PDF/A but missing image or text layer | true | Cảnh báo: PDF/A không đủ 2 lớp (ảnh scan + text OCR), vẫn ký được. |
| 4 | ReadFailed | unreadable/encrypted | false | Không đọc được file, không thể ký số. |

`pdfAPart` (`"1"`/`"2"`/`"3"`) and `pdfAConformance` (`"A"`/`"B"`/`"U"`) are filled only when PDF/A is detected.

## How the FE uses it
1. Call this endpoint with the same `sourcePath` the user picked for signing; render one row per `files` entry.
2. Tick each row on its `file` event: green tag for `Valid`, amber (warning) tag + amber reason for `canSign` with
   another verdict, red for `canSign = false`.
3. On `summary`: enable "Ký số" when `summary.validCount > 0`. Blocked files come back from `ky-thu-muc` as
   `skipped` (see `sign-flow.contract.md`).

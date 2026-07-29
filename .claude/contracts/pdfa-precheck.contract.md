# Contract — PDF/A two-layer pre-check (sign gate)

> A standalone SSE endpoint the FE calls BEFORE `ky-thu-muc`. It inspects every file in the source and reports,
> per file, whether it is a two-layer PDF/A. It informs the FE which files are signable; the BE still runs this
> gate defensively during `ky-thu-muc`, so a non-`Valid` file that slips through is skipped, never signed.

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
| Value | Name | Meaning | Message |
|---|---|---|---|
| 0 | Valid | two-layer PDF/A → signable | PDF/A hai lớp hợp lệ, ký được. |
| 1 | NotPdf | no `%PDF-` header | Chưa thể ký số với file khác định dạng PDF. |
| 2 | NotPdfA | PDF but not PDF/A | File không phải PDF/A, chưa thể ký số. |
| 3 | NotTwoLayer | PDF/A but missing image or text layer | PDF/A không đủ 2 lớp (ảnh scan + text OCR), chưa thể ký số. |
| 4 | ReadFailed | unreadable/encrypted | Không đọc được file, không thể ký số. |

`pdfAPart` (`"1"`/`"2"`/`"3"`) and `pdfAConformance` (`"A"`/`"B"`/`"U"`) are filled only when PDF/A is detected.

## How the FE uses it as a gate
1. Call this endpoint with the same `sourcePath` the user picked for signing; render one row per `files` entry.
2. Tick each row on its `file` event using `file.verdict` / `file.message` (`canSign` = whether it may be signed).
3. On `summary`: enable "Ký thư mục" (`POST ky-thu-muc`) when at least one file is signable (`summary.validCount > 0`).
   `ky-thu-muc` may be called even when some files are non-`Valid` — the BE defensive gate returns those with the
   `skipped` state (see `sign-flow.contract.md`) instead of signing them. Only block when `validCount === 0`.

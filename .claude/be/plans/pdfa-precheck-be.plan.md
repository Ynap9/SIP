# Plan — PDF/A two-layer pre-check (BE)

> A standalone SSE CHECK API that runs BEFORE signing, verifying each file is a two-layer PDF/A.
> It is a gate: the FE blocks signing any file whose verdict is not `Valid`. Independent of `ky-thu-muc`.

## Requirements / Input
- Source: `docs/chuan-dong-goi-sip-tt05.md:41,262,269` (text docs in a SIP = two-layer PDF/A) and
  `docs/thiet-ke-db-dong-goi-sip.md:279` (the "two-layer" property is unchecked today — this fills that gap).
- Input: `{ sourcePath }` — one PDF file OR one folder. All top-level files are inspected (non-PDF is reported,
  not skipped — unlike `ListPdfFiles`).
- Detection is fail-closed: on any doubt the file is NOT signable; never infer `Valid` from a read failure.

## Verdicts (stop at first failure, per file)
| Verdict | Meaning | Message |
|---|---|---|
| `NotPdf` | no `%PDF-` header | Chưa thể ký số với file khác định dạng PDF. |
| `NotPdfA` | PDF but no `pdfaid:part` in XMP | File không phải PDF/A, chưa thể ký số. |
| `NotTwoLayer` | PDF/A but not image+text | PDF/A không đủ 2 lớp (ảnh scan + text OCR), chưa thể ký số. |
| `Valid` | two-layer PDF/A | PDF/A hai lớp hợp lệ, ký được. |
| `ReadFailed` | exception while reading | Không đọc được file, không thể ký số. |

## Detection (reuse `PdfRevisionInfo`, no new parser, no heavy libs)
- **NotPdf**: search first 1024 bytes for `%PDF-`.
- **PDF/A**: `PdfRevisionInfo.Load` → catalog `/Metadata` stream → XMP; require namespace
  `http://www.aiim.org/pdfa/ns/id/` + `pdfaid:part` (read `pdfaid:conformance` too). Inflate the stream if
  `/FlateDecode` (some generators compress it though PDF/A forbids it).
- **Two-layer**: scan objects — image layer = any `/Subtype /Image`; text/OCR layer = any font object
  (`/Type /Font` or `/BaseFont`). Both present ⇒ two-layer. Short-circuit once both found.

## Steps
1. DTOs in `Dtos/Sign/`: enum `PdfFormatVerdict`, `PdfFormatCheckRequestDto`, `PdfFormatInspectResult`,
   `PdfFormatFileResultDto`, `PdfFormatCheckSummaryDto`, `PdfFormatCheckProgressDto` (mirror `SignProgressDto`).
2. `IPdfFormatInspector.Inspect(filePath)` in Application/Interfaces; impl `PdfFormatInspector` in External —
   never throws, catch → `ReadFailed`. Inline logic (house style), reuse `PdfRevisionInfo` public API only.
3. Add `CheckPdfFormatAsync(PdfFormatCheckRequestDto, ct)` → `IAsyncEnumerable<PdfFormatCheckProgressDto>` to
   `ISignService`/`SignService`: validate source → `manifest` event → one `file` event per file → `summary`.
4. Endpoint `POST api/core/document/sign/kiem-tra-dinh-dang` on `SignController`, SSE shape copied from
   `SignFolder` exactly (3 headers, `DisableBuffering`, camelCase + `UnsafeRelaxedJsonEscaping`, `data: {json}\n\n`).
5. `ErrorCodes.PdfFormatInvalid` (+ `ErrorMessages`); register `IPdfFormatInspector` in `Program.cs`.
6. `dotnet build`.

## Expected output
- New endpoint streams `manifest` → per-file `file` (verdict + message) → `summary` (valid/invalid counts).
- Contract at `.claude/contracts/pdfa-precheck.contract.md` for the FE gate. Existing sign flow untouched.

## Decision
- Text-layer heuristic = "document has a font" (needs only object dicts, no page-tree walk / content-stream
  decompress) — matches the spec's "Tj/TJ with a font" without rewriting a parser. Scan-only PDF (no OCR) has
  no font ⇒ correctly `NotTwoLayer`; born-digital text PDF has no image ⇒ `NotTwoLayer`.

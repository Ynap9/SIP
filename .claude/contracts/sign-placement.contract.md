# Contract — signature placement & sign config (step "Cấu hình chữ ký")

> Added 2026-07-28, reworked the same day. `SignRequestDto` now carries a **list** of placements plus optional
> **per-file overrides**; `signPageCount` is **gone** (each placement names its own page). Two GET endpoints feed
> the PDF preview. The sign SSE stream itself is unchanged — see [sign-flow.contract.md](sign-flow.contract.md).

## Routes

| Method | Route | Request | Response | Notes |
|---|---|---|---|---|
| GET | `api/core/document/sign/xem-truoc` | `?sourcePath=<folder>&index=<0-based>` | `ApiResponse<SignPreviewDto>` | Navigate prev/next over the PDFs in the folder |
| GET | `api/core/document/sign/xem-file` | `?path=<absolute pdf path>` | **raw PDF bytes** (`application/pdf`) | No envelope — it feeds pdf.js (the only other such endpoint is `template-chu-ky/xem-anh`). Errors → HTTP 404 |
| POST | `api/core/document/sign/kiem-tra-vi-tri` | `SignPositionCheckRequestDto` (`sourcePath` + the same placement fields, **no thumbprint**) | `ApiResponse<SignPositionCheckResultDto>` | Measures every file and predicts which blocks the signer will repair. **Reports only — never blocks signing** |

## `SignPreviewDto` (JSON as the FE receives it)
```json
{
  "index": 0,
  "total": 12,
  "hasPrev": false,
  "hasNext": true,
  "files": [{ "name": "vanban.pdf", "fullPath": "C:\\…\\vanban.pdf", "sizeBytes": 128394, "alreadySigned": false }]
}
```
- `files` holds **exactly one** item, or is **`[]`** when `index` is outside the list (paging past the first/last
  file). An empty array is a **normal** result, never an error — the FE keeps showing the current file.
- The FE disables its prev/next buttons on `hasPrev`/`hasNext`, so `[]` is only a safety net.
- File order is identical to `sign/danh-sach-file`, which is the same order the sign batch uses.

## `SignRequestDto` — placement fields
```json
{
  "thumbprint": "…", "sourcePath": "…", "reason": null, "location": null,
  "useCustomPosition": true,
  "positions": [{ "pageNumber": 1, "xRatio": 0.62, "yRatio": 0.08, "widthRatio": 0.2857, "heightRatio": 0.0356 }],
  "fileOverrides": [{ "path": "C:\\…\\vanban.pdf", "positions": [{ "pageNumber": 3, "xRatio": 0.1, "…": 0 }] }],
  "allowOverlapContent": false
}
```

| Field | Type | Meaning |
|---|---|---|
| `useCustomPosition` | `boolean` | `false` (default) = BE picks the spot: **top-right of page 1** with an 8pt margin (scaled to the page). `true` = use `positions` / `fileOverrides`. |
| `positions` | `array` | The blocks the user dropped, **no upper bound**. Ratios 0..1 of the page, origin **top-left**, Y downwards (screen-like). Applies to every file **without** an override. |
| `positions[].pageNumber` | `number` | 1-based page the block was dropped on — that exact page gets the block. A file with fewer pages puts it on its **last** page: never an error, never skipped. |
| `fileOverrides` | `array` | Per-file placement, keyed by absolute `path` (matched case-insensitively). A listed file uses its own `positions` **instead of** the shared ones; an empty list falls back to the default top-right spot. Lets one request carry a different layout per file — the batch is still **one** call. |
| `allowOverlapContent` | `boolean` | Carried end-to-end but **has no effect**: the page-content detector is deliberately not built. The block is always placed exactly where the user dropped it. |
| `fontFamily` | `string?` | One of `Times New Roman` (default), `Arial`, `Verdana`, `Georgia`, `Courier New`; anything else → default. `ky-thu-muc` only. |
| `fontSize` | `number?` | Point size on an A4 page (default 10), clamped to 6..24, scaled by each page's size. `ky-thu-muc` only. |
| `textColor` | `string?` | `#RRGGBB` (default `#000000`); unparsable → black. `ky-thu-muc` only. |
| `positions[].kind` | `number` | `0` signature block (default) · `1` seal image · `2` wet-signature image. Unknown → `0`. |
| `sealImagePath` / `wetSignatureImagePath` | `string?` | Absolute `.png/.jpg/.jpeg` path, ≤ 5 MB, read once per batch; missing/invalid → error `1040`. A `kind = 1/2` block without its image is skipped. `ky-thu-muc` only. |

### Image blocks (seal / wet signature)
Each image block becomes a `/Subtype /Stamp` annotation (`/F 4`, `/AP /N` = the image drawn to fit the block,
aspect kept, PNG transparency kept via `/SMask`) appended in the **same** incremental revision, so the signature
covers it. Its rect is the dropped ratios × page size (shifted inside the page if needed), **not** re-sized like
the text block. Signature widgets come first in `/Annots`, stamps after, so images draw on top. When the request
has custom positions but **no `kind = 0` block**, the signature is **invisible** (one widget, `/Rect [0 0 0 0]`, no
`/AP`) — same as `kssm.be`. With no positions at all the default visible block is still used.

### Why ratios, not points
The user picks a spot once and it is applied to every file. Files differ in page size (A4 portrait/landscape, A3,
odd scans), so absolute points would overflow small pages and shrink on large ones. The BE re-multiplies the ratios
by each page's real `/MediaBox`, and scales the signature block (and its font) with it.

### Many blocks = still ONE signature
N blocks (on one page or spread over pages) produce **one** signature field with N widget kids (PDF 12.7.3.1) —
one `/V`, one `/ByteRange`, **one TSA call**. It does not multiply signatures, token access, or signing time.

## Placement repair — the BE fixes, it does not reject
`ErrorCodes.SignPositionInvalid = 1037` is raised **only** when `useCustomPosition = true` and both `positions` and
`fileOverrides` are empty (or the page box is unreadable). Every other oddity is repaired per block, because
failing a whole batch over one bad block would force the user to redo the entire layout:

| Problem | Repair |
|---|---|
| block runs past a page edge | shifted back inside, **size kept** (`SignService.ToPlacements` + `SignaturePlacementResolver`) |
| `widthRatio`/`heightRatio` ≤ 0 | back to the standard 170×30pt ratio (only used to decide the anchor side) |
| `pageNumber` < 1, or > the file's page count | clamped to page 1 / the last page |

The signature block's **size is not taken from the ratios**: it is the measured text box of the two lines at the
chosen font/size (`PdfAppearanceBuilder.MeasureText`). The block keeps the user's **top edge**, and keeps the
**right edge** when its centre is in the right half of the page (left edge otherwise) — so a block dragged flush
into the top-right corner is signed flush in that corner, with no gap.

`kiem-tra-vi-tri` runs the **same** `NormalizePlacements` + `ISignaturePlacementResolver` the signer uses, then
compares the requested rect with the resolved one (0.5pt tolerance) — so its prediction cannot drift from what the
signed file gets. Per file it returns `pageCount`, `blockCount`, `readable`, `fitted` and human-readable `issues`;
the batch totals are `fittedCount` / `adjustedCount` / `unreadableCount`.

## Appearance
Both lines (signer CN, signing time `yyyy-MM-ddTHH:mm:ss.fffffff`) are **left-aligned**, starting at the block's
top-left corner with **no padding and no border**; line height = the font's own line spacing. Font family, size
and colour come from the request (the sign template's `fontChuKySo` / `coChuKySo` / `mauChuKySo`).

## FE side (already implemented)
Placement is edited **once per sign template** at `/cau-hinh/template-chu-ky/{id}`
(`pages/config/template-chu-ky/template-config/`) on a blank A4 page or a chosen folder, and stored as
template positions. `/ky-so` picks a folder + template and hands the template's `kind = 0` positions to `sign-step`
as `positions` (`useCustomPosition = positions.length > 0`, `fileOverrides = null`). `sign-step` fires both checks
(`kiem-tra-vi-tri` + the `kiem-tra-dinh-dang` SSE) when the folder is set, and re-runs `kiem-tra-vi-tri` when the
template changes. `fileOverrides` stays in the contract but the FE no longer sends it (a template has no per-file
layout). The preview renders with **`pdfjs-dist`** (worker bundled locally — the app is offline) and shows **all pages
stacked in one scrollable column**, each page drawn only when it scrolls into view (an `IntersectionObserver`;
drawing a 200-page file up front would exhaust memory). Ratios come from the drop point relative to that page's
box, so zoom never affects the result, and the grab offset inside the dragged block is preserved so the block
lands exactly where the drag image was. A placed block can be **moved** (drag its body) and **resized** (drag any
corner; the opposite corner anchors). The signature block is sized from the text measured with a canvas at the
template font/size (line height `SIGN_LINE_HEIGHT_RATIO`), so resizing it changes the template font size (step
0.5pt, 6..24) and every signature block re-fits, keeping its right edge when it sits in the right half of the page.
Image blocks keep the image's own aspect ratio, base width `SIGN_IMAGE_BLOCK_WIDTH_PT`.

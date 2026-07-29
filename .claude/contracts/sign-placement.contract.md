# Contract — signature placement & sign config (step "Cấu hình chữ ký")

> Added 2026-07-28, reworked the same day. `SignRequestDto` now carries a **list** of placements plus optional
> **per-file overrides**; `signPageCount` is **gone** (each placement names its own page). Two GET endpoints feed
> the PDF preview. The sign SSE stream itself is unchanged — see [sign-flow.contract.md](sign-flow.contract.md).

## Routes

| Method | Route | Request | Response | Notes |
|---|---|---|---|---|
| GET | `api/core/document/sign/xem-truoc` | `?sourcePath=<folder>&index=<0-based>` | `ApiResponse<SignPreviewDto>` | Navigate prev/next over the PDFs in the folder |
| GET | `api/core/document/sign/xem-file` | `?path=<absolute pdf path>` | **raw PDF bytes** (`application/pdf`) | **Only endpoint in the repo without the envelope** — it feeds pdf.js. Errors → HTTP 404 |
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
| `useCustomPosition` | `boolean` | `false` (default) = BE picks the spot: **top-right of page 1**, standard 170×30pt block **scaled to the page size**. `true` = use `positions` / `fileOverrides`. |
| `positions` | `array` | The blocks the user dropped, **no upper bound**. Ratios 0..1 of the page, origin **top-left**, Y downwards (screen-like). Applies to every file **without** an override. |
| `positions[].pageNumber` | `number` | 1-based page the block was dropped on — that exact page gets the block. A file with fewer pages puts it on its **last** page: never an error, never skipped. |
| `fileOverrides` | `array` | Per-file placement, keyed by absolute `path` (matched case-insensitively). A listed file uses its own `positions` **instead of** the shared ones; an empty list falls back to the default top-right spot. Lets one request carry a different layout per file — the batch is still **one** call. |
| `allowOverlapContent` | `boolean` | Carried end-to-end but **has no effect**: the page-content detector is deliberately not built. The block is always placed exactly where the user dropped it. |

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
| `widthRatio`/`heightRatio` ≤ 0 | back to the standard 170×30pt block for that page |
| block smaller than `AppearanceMinScale` (0.5) of the page's standard block | scaled up to that floor, aspect kept — smaller than this and the text is unreadable |
| `pageNumber` < 1, or > the file's page count | clamped to page 1 / the last page |

`kiem-tra-vi-tri` runs the **same** `NormalizePlacements` + `ISignaturePlacementResolver` the signer uses, then
compares the requested rect with the resolved one (0.5pt tolerance) — so its prediction cannot drift from what the
signed file gets. Per file it returns `pageCount`, `blockCount`, `readable`, `fitted` and human-readable `issues`;
the batch totals are `fittedCount` / `adjustedCount` / `unreadableCount`.

## Appearance
Both lines (signer CN, signing time) are **centred** in the block — horizontally and vertically — at
`fontSize = 10pt × height / 30pt`. The production sample left-aligned them at x=2pt; centring is a deliberate
business change, which is why `AppearanceLine1X/Y` and `AppearanceLine2X/Y` no longer exist.

## FE side (already implemented)
`pages/ky-so` is a **3-step wizard**: cert → **config** (`config-step/` + `config-confirm-dialog/`) → sign.
Step 3 has **no source picker at all**: it inherits `sourcePath` from step 2 and fires both checks
(`kiem-tra-vi-tri` + the `kiem-tra-dinh-dang` SSE) the moment it mounts. Letting the user change the folder there
would strand every placement, which was picked on the *previous* folder's files.
The preview renders with **`pdfjs-dist`** (worker bundled locally — the app is offline) and shows **all pages
stacked in one scrollable column**, each page drawn only when it scrolls into view (an `IntersectionObserver`;
drawing a 200-page file up front would exhaust memory). Ratios come from the drop point relative to that page's
box, so zoom never affects the result, and the grab offset inside the dragged block is preserved so the block
lands exactly where the drag image was. A placed block can be **moved** (drag its body) and **resized** (drag any
corner; the opposite corner anchors, and one scale factor keeps the 170:30 shape).

The checkbox *"Chỉ áp dụng cho file hiện tại"* is **per file, not a mode**: it routes newly placed blocks into
`fileOverrides` for the previewed file, and **re-derives itself on every file change** — checked only if that file
already has an override. Otherwise the third file inherits the tick from the second and silently becomes an
override too. Checking it **seeds the override with a copy of the shared blocks** (so the user edits a layout
instead of starting from a blank page); unchecking it deletes that file's override **behind a confirm** — a hidden
override would still be sent, but silently discarding the blocks the user just placed is worse. Neither branch
ever writes to the shared `positions`.

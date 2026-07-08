# Check & Package Page — Build Plan (revised)

## Requirements
- Route `/dong-goi` (sidebar "Kiểm tra & đóng gói", already above "Cấu hình"), lazy via `dong-goi.router.ts` + `loadChildren` under `Layout.children`. Full-width, no box, Times New Roman.
- **Container** `pages/dong-goi/dong-goi.ts|html`: standalone, thin (no business logic). Shows breadcrumb + `p-selectbutton` (SINGLE default / MULTI) and `@switch` renders `<app-check/>` (single) or `<app-empty/>` (multi).
- **Empty page** `pages/empty/empty.ts` (`app-empty`): a SEPARATE page; centered muted text "Tính năng đang trong quá trình phát triển. Xin vui lòng liên hệ admin."
- **Check page** `pages/check/check.ts|html|scss` (`app-check`): `extends BaseComponent`, `imports:[...SharedImports, ProgressBarModule, TableModule]`. Holds the whole single-flow + SSE logic. "Đóng gói" is just an ACTION (button) inside this page.
- Check layout — vertical order:
  1. Drop box "Hãy đưa thư mục cần kiểm tra vào đây!" (drag&drop folder → desktop `file.path`) + TEMP path input (mark `REMOVE on desktop build`).
  2. Progress bar + file table — rendered BETWEEN the box and the buttons (only after Kiểm tra), pushing the buttons down.
  3. Buttons row "Kiểm tra" + "Đóng gói" **right-aligned** (`justify-end`) at the bottom; "Đóng gói" appears only after `completed`.
- On "Kiểm tra" → SSE; `p-progressbar [value]="percent()"`; file `p-table`.
- File table cols: file name (mã lưu trữ tài liệu) | status (spinner→green/red) | error reason.
- File row = a document in `Van_ban`. Row error if any related event has Status=Error: `targetType==='file'` & `target===filename`, OR `targetType==='field'` whose label (before ' / ') matches filename → show message in reason. `Ho_so`-level field errors (no file match) → toast. First error wins.
- Rows seeded from `manifest` event; ALSO create a row from a `file` event if none matches (robust if manifest absent).
- "Đóng gói" → placeholder `messageWarning('Tính năng đang phát triển')`.
- Reuse `check.service.ts` (SSE fetch-stream + AbortController) and `check.models.ts` (`ICheckProgress`, `IFileRow`) as-is. BE manifest event + `Files` field already added.

## Steps
1. BE manifest event + `Files` on `CheckProgressDto` — DONE, keep. (BE must be rebuilt/restarted to emit it.)
2. `check.models.ts`, `check.service.ts` — DONE, keep.
3. `pages/empty/empty.ts` — new separate component. Verify build.
4. `pages/check/check.ts|html|scss` — move SSE logic here; new layout (progress+table above right-aligned buttons); manifest + file-event row seeding. Verify build.
5. `pages/dong-goi/dong-goi.ts|html` — rewrite as container (breadcrumb + SelectButton + `@switch` check/empty). Verify build.
6. `npm run build` passes; manual: single → path → Kiểm tra → progress+table appear between box & buttons, buttons stay bottom-right; multi → empty page.

## Revision notes
- Check is an ACTION inside "Đóng gói": move it to `pages/dong-goi/check/`, route `dong-goi/check` (child of `dong-goi`; container renders `<router-outlet/>` + SelectButton navigates check/multi).
- EVERY component has a `.spec.ts` (minimal `should create`).
- Table: header + data centered; **Trạng thái** = processed-or-not (spinner → green check once checked, even if the file has errors); **Tình trạng** = đúng/lỗi square (green check / red X). Last (footer) row = hồ sơ summary (overall verdict + reason).
- Normalize pasted path via `Utils.normalizeFolderPath()` before sending to BE.

## Expected output
- New: `pages/check/check.ts|html|scss`, `pages/empty/empty.ts`.
- Rewritten: `pages/dong-goi/dong-goi.ts|html` (container).
- Kept: `services/check.service.ts`, `models/check.models.ts`, `pages/dong-goi/dong-goi.router.ts`, `src/app.routes.ts`, BE manifest.

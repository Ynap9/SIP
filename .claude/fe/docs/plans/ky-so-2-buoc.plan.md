# Plan — Ký số page (2-step wizard)

> **DONE, then extended 2026-07-28 → the wizard is now 3 steps.** A "Cấu hình chữ ký" step was inserted as step 2
> (`config-step/` + `config-confirm-dialog/`): PDF preview via `pdfjs-dist`, drag-drop the signature block to pick a
> position, set pages-to-stamp and the overlap option. What was step 2 here is now step 3. Contract:
> [../../../contracts/sign-placement.contract.md](../../../contracts/sign-placement.contract.md).

> Rework `pages/ky-so` into a 2-step wizard: **Step 1** pick a certificate (+ token PIN verify),
> **Step 2** pick a source, pre-check format (SSE), then sign the folder (SSE) with a per-file live table.
> FE only. Reuses the Check/Pack patterns (drag-drop source, `fetch`+`ReadableStream` SSE, dedicated `p-table`).

## Requirements / Input
- Existing: `pages/ky-so/ky-so.ts|html` (cert list UI), `services/sign.service.ts` (only `getCertificates`),
  `models/sign.models.ts` (`ISignCert`), `shared/constants/sign.constants.ts` (`ECertSource` + labels). Route + menu already exist.
- BE endpoints (all `api/core/document/sign`): `GET chung-thu-so`, `POST kiem-tra-token` (native PIN box + caches key),
  `POST kiem-tra-dinh-dang` (SSE pre-check, contract `pdfa-precheck.contract.md`), `POST ky-thu-muc` (SSE sign).
- DTOs to mirror (camelCase, enums as numbers): `TokenVerifyDto`, `PdfFormatCheckProgressDto`/`PdfFormatFileResultDto`/`PdfFormatCheckSummaryDto`,
  `SignProgressDto`/`SignFileResultDto`/`SignResultDto`, `SignRequestDto`.
- **Decision (structure):** split into a shell + child components (feature-page skill), do NOT rebuild the cert UI:
  `ky-so.ts` shell (breadcrumb + `p-steps` + `activeStep`), `cert-step/` (Step 1 = current cert UI moved in), `sign-step/` (Step 2),
  `sign-dialog/` (sign form popup). `p-steps` (`StepsModule`) as a signal-driven indicator gives full control over the advance gate.

## Steps
1. **Models** — extend `models/sign.models.ts` (interfaces only): `ITokenVerify`, `IPdfFormatFileResult`, `IPdfFormatSummary`,
   `IPdfFormatProgress`, `ISignFileResult`, `ISignResult`, `ISignProgress`, `ISignRequest`, and a view row `ISignFileRow`
   (`name`, `checkStatus: 'processing'|'ok'|'error'`, `verdict?`, `checkMessage?`, `signStatus: 'idle'|'processing'|'signed'|'failed'`,
   `signMessage?`, `signedTime?`, `trustedTime?`).
2. **Constants** — extend `shared/constants/sign.constants.ts`: `EPdfFormatVerdict {Valid=0,NotPdf=1,NotPdfA=2,NotTwoLayer=3,ReadFailed=4}`,
   `PDF_FORMAT_VERDICT_LABELS` + `PDF_FORMAT_VERDICT_SEVERITIES` (Valid=success, others=danger), sign-status labels.
3. **Service** — add to `sign.service.ts`: `verifyToken(thumbprint)` → `IBaseResponseWithData<ITokenVerify>` (POST `kiem-tra-token`,
   body `{ thumbprint }`); `checkPdfFormat(sourcePath)` → `Observable<IPdfFormatProgress>`; `signFolder(req: ISignRequest)` →
   `Observable<ISignProgress>`. Both SSE via `fetch`+`ReadableStream`, split on `\n\n`, `AbortController` teardown — copy `CheckService.checkFolder`.
4. **Shell `ky-so.ts|html`** — breadcrumb + `p-steps` (`StepsModule`, imported in page) items `['Chọn chứng thư số','Ký số']`, `[readonly]="true"`,
   `[activeIndex]="activeStep()"`. Hold `selectedThumbprint` + `verifiedCert` signals. `@switch (activeStep())` renders `<app-cert-step>` / `<app-sign-step>`.
   Guard: advance to step 2 only after `cert-step` emits `(verified)` with an OK token.
5. **Step 1 `cert-step/`** — move current cert list UI (dropdown + detail card + summary + `app-data-table`) into this child.
   Add a **"Tiếp tục"** button: on click call `signService.verifyToken(thumbprint)`; while pending show `[loading]`. On `isResponseSucceed && data.valid`
   emit `verified(cert)`; else `messageError(data.reason)` and stay (map `failedCheck` to a hint). Native token PIN box pops during this call.
   Disable button until a `canSign` cert is selected.
6. **Step 2 `sign-step/`** — inputs: `thumbprint`. Import `TableModule` + `ProgressBarModule` in page.
   - **Source picker**: reuse Check's drag-drop box + desktop `chooseFolder()` + web-only TEMP path input (`@if (!desktop.isDesktop)`),
     `Utils.normalizeFolderPath`. Button **"Kiểm tra"** — filled `severity="info"` (stands out from the primary "Ký số"), `[disabled]="!canCheck()"`.
   - **Pre-check (SSE `kiem-tra-dinh-dang`)**: seed rows from `manifest.files`; per `file` event set `checkStatus`, `verdict`, `checkMessage`
     (spinner while `processing`). Progressbar bound to `percent()`. On `summary` store it in `checkSummary` (drives `validCount`/`invalidCount`).
   - **Table (dedicated `p-table`)**: STT | Tên file | **Trạng thái** (spinner→verdict badge via `PDF_FORMAT_VERDICT_*`) | **Nguyên nhân** |
     **Trạng thái ký số** (empty at check) | **Time ký** (empty) | **Dấu thời gian** (empty).
   - **"Ký số" button**: visible once check is running/completed; enabled when at least one file is signable (`validCount > 0`). Non-Valid files are skipped by the BE defensive gate. Opens `sign-dialog`.
7. **Sign popup `sign-dialog/`** — `p-dialog` (dialog-action skill), fields **Lý do (Reason)** + **Nơi ký (Location)** (optional, placeholder shows BE default),
   footer buttons **Bắt đầu** / **Huỷ**. `visible = model()`. On **Bắt đầu** emit `start({reason,location})` and close; parent runs sign.
8. **Sign (SSE `ky-thu-muc`)** in `sign-step`: build `ISignRequest {thumbprint, sourcePath, reason, location}`; reset progress, set every row
   `signStatus:'processing'`. Per `file` event map the **three states** (`success`→signed / `skipped`→skipped badge / else failed) and fill
   **Time ký** (`signedTime`), **Dấu thời gian** (`trustedTime`); spinner while a row is still `processing`. On `summary` store it in `signResult` and
   open a **result dialog** (Step 8b) — no toast, no auto-open. Signed files are written **next to the source** as `<name>_daky_<ts><ext>`;
   `summary.outputFolder` = the source folder (no new folder created).
8b. **Result popup `sign-result-dialog/`** — `p-dialog` (dialog-action skill), `visible = model()`, `result = input<ISignResult|null>()`.
   Shows "Ký số hoàn tất" + three separate counts (Đã ký `successCount`, Bỏ qua `skippedCount`, Lỗi `failedCount`) + `outputFolder`
   (source folder holding the signed files). Footer **"Mở thư mục"** (emits `openFolder(outputFolder)` → parent runs desktop Explorer / clipboard) + **"Đóng"**.
9. **Specs** — `cert-step.spec.ts`, `sign-step.spec.ts`, `sign-dialog.spec.ts`, `sign-result-dialog.spec.ts` (import + `should create`); keep `ky-so.spec.ts`. No providers in specs.
10. **Build** — `npm run build` and report verbatim.

## Expected output
- `pages/ky-so` shows a 2-step `p-steps` under the breadcrumb; Step 1 verifies the token before unlocking Step 2.
- Step 2 pre-checks the source over SSE with a live per-row table, gates "Ký số" on having at least one valid file, then signs over SSE filling the
  sign-status / time / timestamp columns per file. Signed files land **next to the source** (`<name>_daky_<ts><ext>`); on finish a **result dialog**
  shows the three counts + source folder with an "Mở thư mục" button. UI matches Check/Pack (full-width, Aura light, TNR).
- `npm run build` green.

## BE dependency / open question (needs approval)
- The **"Time ký"** column: `SignFileResultDto` exposes `trustedTime` (TSA genTime = Dấu thời gian) but **no distinct signing time**.
  Proposed: ask `sip-be` to add `signedTime` to `SignFileResultDto` + update the sign contract. Until then Step 8 leaves "Time ký" as `signedTime` (blank).
  Do NOT fabricate it FE-side. Confirm before building Step 8.

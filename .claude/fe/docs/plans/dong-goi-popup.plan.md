# Đóng gói SIP — Popup Build Plan

## Requirements
- Gắn vào `pages/dong-goi/check/check.ts` `onPackage()` (đang là placeholder). Không đổi luồng check/SSE.
- Bấm "Đóng gói":
  - **Không đủ điều kiện** (`!completed()` || `hoSo().status !== 'ok'`) → chỉ toast: `messageWarning('Hồ sơ không đủ điều kiện! Vui lòng kiểm tra lại hoặc liên hệ admin!')`. Nút "Đóng gói" vẫn render khi `completed()` như hiện tại; điều kiện kiểm tra lại lúc bấm.
  - **Đủ điều kiện** → mở popup form (`showDialog.set(true)`).
- **Popup form** (`p-dialog`, modal, `[draggable]=false`, header "Đóng gói hồ sơ SIP"):
  - Fields nhập: `maPhong` (text), `nguoiTao` (text — người dùng tự điền, KHÔNG default), `soThuTuSip` (`p-inputnumber`, ≥1), `outputPath` (text — dán/chọn thư mục xuất, **có thể để trống** → BE dùng thư mục cha của folderPath; ô dán mark `REMOVE on desktop build`).
  - **Ẩn (không hiển thị trong dialog)**: `createZip` (luôn true), `schemaSourceDir` (để trống → BE tự template).
  - `folderPath` = `this.folderPath()` (đã normalize) — hiển thị **read-only**, không sửa.
  - Tất cả field nhập đều **optional** (BE nullable) → không chặn submit; nút "Đóng gói" chỉ disable khi `packing()`.
  - Footer: nút "Hủy" (`text`, đóng dialog, reset) + nút "Đóng gói" (`severity=success`, `[loading]="packing()"`, `[disabled]="packing()"`).
- **Gọi API**: `POST {baseUrl}/api/core/document/pack/ho-so-folder`, body `IPackRequest`, response `IBaseResponseWithData<IPackResult>`.
  - Trong khi gọi: `packing.set(true)` → nút xoay spinner; xong → `packing.set(false)`.
  - **Thành công** (`isResponseSucceed(res)`): đóng form, mở **popup thành công** hiển thị `packagePath` + `zipPath` (nếu có) là đường dẫn click được; click → `openFolder(path)`. Hiển thị `totalDoc`, và `warnings[]` (nếu có) dạng list muted.
  - **Lỗi** (`!isResponseSucceed` hoặc HTTP/network error) → `messageError(res?.message || 'Đóng gói thất bại. Vui lòng thử lại.')`, giữ form mở để sửa lại.
- **Mở folder**: `openFolder(path)` — desktop Electron/Tauri gọi `shell.openPath(path)` / `@tauri-apps/plugin-opener`; web-dev fallback: copy path vào clipboard + toast "Đã sao chép đường dẫn". Bọc trong 1 chỗ duy nhất, mark `TODO desktop: shell.openPath`.

## Decisions
- **D1 — Dialog là component riêng** `pages/dong-goi/check/package-dialog/package-dialog.ts|html|scss` (`app-package-dialog`), KHÔNG inline trong check.ts. Lý do: form có state/validate riêng + `.spec.ts` riêng, giữ check.ts mỏng. Inputs: `folderPath: input<string>`, `visible: model<boolean>` (two-way); Output: `packed = output<IPackResult>()`. Component tự gọi service + giữ `packing` signal.
- **D2 — Success popup = `p-dialog` thứ 2 NGAY TRONG package-dialog** (không tạo component thứ 3). Lý do: dùng chung kết quả `IPackResult`, vòng đời gắn liền form; 2 signal `showForm`/`showSuccess` trong cùng component. check.ts chỉ cần `[(visible)]`.
- **D3 — Tạo `models/pack.models.ts` + `services/pack.service.ts` mới** (không nhồi vào check.*). Lý do: tách REST pack khỏi SSE check, đúng convention feature-page (1 feature = 1 model + 1 service).
- **D4 — `openFolder` đặt trong package-dialog** (nơi có path), tách 1 method để dễ thay khi build desktop. Web-dev: clipboard + toast.

## Steps
1. `models/pack.models.ts` — `IPackRequest` (folderPath, outputPath?, schemaSourceDir?, maPhong?, nguoiTao?, soThuTuSip?, createZip) + `IPackResult` (objid, fileCode, packagePath, zipPath?, totalDoc, warnings: string[], message). camelCase khớp BE. `npm run build`.
2. `services/pack.service.ts` — `@Injectable({providedIn:'root'})`, inject `HttpClient`, `base=environment.baseUrl`; `pack(req: IPackRequest)` → `http.post<IBaseResponseWithData<IPackResult>>('/api/core/document/pack/ho-so-folder', req)`. `npm run build`.
3. `pages/dong-goi/check/package-dialog/package-dialog.ts|html|scss` — `extends BaseComponent`, `imports:[...SharedImports]` (Dialog/Button/InputText/InputNumber đã có). Form signals + validate + `packing` + `onSubmit()` gọi service → success/error; success `p-dialog` với path click→`openFolder`. `+ package-dialog.spec.ts` (smoke `should create`). `npm run build`.
4. Sửa `check.ts` — `onPackage()` đổi placeholder thành: check điều kiện → toast (không đủ) hoặc `showPackageDialog.set(true)` (đủ). Thêm signal `showPackageDialog`. `npm run build`.
5. Sửa `check.html` — thêm `<app-package-dialog [folderPath]="folderPath()" [(visible)]="showPackageDialog" />`; thêm import `PackageDialog` vào `imports` của check.ts. `npm run build`.
6. Manual verify: check OK → "Đóng gói" → form mở; bấm "Đóng gói" (kể cả khi outputPath trống) → spinner → success popup hiện path; click path → openFolder/copy. Check FAIL → "Đóng gói" → chỉ toast cảnh báo. `npm run build` cuối cùng phải pass.

## Expected output
- New: `models/pack.models.ts`, `services/pack.service.ts`, `pages/dong-goi/check/package-dialog/package-dialog.ts|html|scss` + `.spec.ts`.
- Modified: `pages/dong-goi/check/check.ts` (onPackage → mở dialog theo điều kiện + signal + import), `check.html` (gắn `<app-package-dialog>`).
- Untouched: BE (đã xong), `check.service.ts`, `check.models.ts`, SSE flow, routes.

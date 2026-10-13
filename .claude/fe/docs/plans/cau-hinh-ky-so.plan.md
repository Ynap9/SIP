# Plan — Move certificate + signature config into Cấu hình (sign templates)

> Status: **built** 2026-10-13. Reference UIs: `ksts.fe` (Chứng thư số / Cấu hình template menus) and
> `sao_mai_fe` `ky-so/template/config-template` (editor layout).

## Requirements
- Certificate selection and signature configuration leave `/ky-so` and live under `/cau-hinh`.
- Certificate tab has a button that shows only signable certificates.
- Signature configuration is a sign template editor laid out like `sao_mai_fe` config-template.
- Seal and wet-signature images are picked from the local machine.
- No code comments in new code; follow FE conventions and the existing `dong-goi` shell pattern.

## Steps
1. **BE (additive)** — `Sip.Desktop` bridge message `pickImage` → `image:<path>` (PNG/JPEG filter);
   `GET template-chu-ky/xem-anh?path=` returns raw image bytes (404 on error) via
   `ISignTemplateService.OpenImageForPreview`. Verify: `dotnet build`.
2. **FE plumbing** — `models/sign-template.models.ts`, `services/sign-template.service.ts`,
   `services/sign-cert-state.service.ts` (verified cert, RAM only), `DesktopService.pickImage()`,
   `ESignTemplatePositionKind` + labels in `sign.constants.ts`.
3. **`/cau-hinh` shell** — `p-selectbutton` tabs + `<router-outlet/>`; children `ho-so-van-ban` (old table, moved),
   `chung-thu-so` (old cert-step + "Chỉ hiện chứng thư ký được" toggle; picking a cert runs `kiem-tra-token`),
   `template-chu-ky` (paged list, search, delete) and `template-chu-ky/:id` (`moi` = create).
4. **Template editor** — left: cert / name / reason / location, palette (signature block, seal, wet signature) with
   image pick/remove, active-block panel; right: PDF preview (sample file, folder, pasted path) with drag/move/resize
   per block kind. Save = POST then PUT.
5. **`/ky-so`** — single page: folder (drag-drop / pick / paste) + template + certificate + "Xác thực PIN";
   `sign-step` gets the template's `kind = 0` positions and reason/location defaults. Remove `cert-step`,
   `config-step`, `config-confirm-dialog`. Verify: `npm run build`.
6. **Docs** — contracts (`sign-template`, `sign-placement`), BE/FE architecture, `project-structure.md`, CLAUDE.md.

7. **Revision (2026-10-13)** — editor previews on a blank A4 page (no sample PDF); signature text is left-aligned,
   no padding/border, block = measured text box anchored to the dragged top edge (and right edge in the right half);
   font family / size / colour stored on the template (`fontChuKySo`, `coChuKySo`, `mauChuKySo`, migration
   `AddSignTemplateAppearance`) and sent to `ky-thu-muc` as `fontFamily` / `fontSize` / `textColor`.

## Expected output
- `/cau-hinh` shows three tabs; certificates can be filtered to signable; templates can be created, edited
  (with images from the machine) and deleted.
- `/ky-so` signs a folder with the chosen template's signature-block layout and the verified certificate.
- Seal / wet-signature images are stamped by `ky-thu-muc` as `/Stamp` annotations (done 2026-10-13).
- Per-file overrides (`fileOverrides`) are no longer produced by the FE.

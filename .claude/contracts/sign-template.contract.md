# Contract — Signature configuration templates

> Reusable signing presets stored per machine: certificate, reason/location, seal and wet-signature images,
> and the ratio coordinates of every block. Published by `sip-be`; `sip-fe` implements against this file.

## Routes

All under `api/core/document/template-chu-ky`. Every call returns HTTP 200 with the real state in
`ApiResponse.status` (1 = ok, 0 = error).

| Method | Route | Request | Response `data` |
|---|---|---|---|
| POST | `template-chu-ky` | `AddSignTemplate` (body) | `ViewSignTemplate` |
| PUT | `template-chu-ky` | `UpdateSignTemplate` (body) | `ViewSignTemplate` |
| DELETE | `template-chu-ky/{id}` | — | `null` |
| GET | `template-chu-ky/{id}` | — | `ViewSignTemplate` |
| GET | `template-chu-ky/find-paging` | query: `pageNumber`, `pageSize`, `keyword?`, `sort?` | paged `ViewSignTemplate` |
| GET | `template-chu-ky/file-mau` | — | `SampleFile` |
| GET | `template-chu-ky/xem-anh` | query: `path` | **raw image bytes** (`image/png` / `image/jpeg`), no envelope; errors → HTTP 404 |

## DTO shapes (camelCase, exactly as FE receives)

```json
// AddSignTemplate — POST body. UpdateSignTemplate is the same plus "id".
{
  "tenTemplate": "Mẫu UBND phường",
  "thumbprint": "A1B2…",
  "tenChungThu": "Ủy ban Nhân dân phường Hà Tu",
  "lyDoKy": null,
  "noiKy": null,
  "anhDauDoPath": "D:\\anh\\dau-do.png",
  "anhChuKyTuoiPath": null,
  "fontChuKySo": "Times New Roman",
  "coChuKySo": 10,
  "mauChuKySo": "#000000",
  "positions": [
    { "kind": 0, "pageNumber": 1, "xRatio": 0.62, "yRatio": 0.08, "widthRatio": 0.2857, "heightRatio": 0.0356 }
  ]
}
```

```json
// ViewSignTemplate — returned by POST / PUT / GET {id}, and as each item of find-paging
{
  "id": 1, "tenTemplate": "…", "thumbprint": "…", "tenChungThu": "…",
  "lyDoKy": null, "noiKy": null,
  "anhDauDoPath": "C:\\Users\\x\\AppData\\Roaming\\SipPack\\template-assets\\1\\dau-do.png",
  "anhDauDoNguonPath": "D:\\anh\\dau-do.png",
  "anhChuKyTuoiPath": null, "anhChuKyTuoiNguonPath": null,
  "fontChuKySo": "Times New Roman", "coChuKySo": 10, "mauChuKySo": "#000000",
  "createdDate": "2026-08-05T09:49:16", "modifiedDate": null,
  "positions": [ … ]
}
```

```json
// SampleFile — GET file-mau
{ "filePath": "C:\\…\\Templates\\sample\\file-mau-ky-so.pdf",
  "fileName": "file-mau-ky-so.pdf", "exists": true }
```

```json
// find-paging data
{ "items": [ /* ViewSignTemplate */ ], "totalItems": 12, "customData": null }
```

## Enums (serialised as NUMBERS)

`positions[].kind` — `0 ChuKy` (signature block) · `1 DauDo` (red seal image) · `2 ChuKyTuoi` (wet signature image).
Never reorder: the numbers are already stored in the user's local database.

## Error codes

`1038` template not found · `1039` template name already used on this machine · `1040` image invalid
(missing / wrong extension / too large).

`1041 SignTemplateSampleMissing` is **declared but never thrown** — a missing sample PDF is not an error path.
`GET template-chu-ky/file-mau` always succeeds and reports `exists: false` instead. Treat `1041` as reserved.

## Notes FE must not get wrong

- **Coordinates are ratios 0..1** of the page, origin top-left, Y downwards — identical to
  [`sign-placement.contract.md`](sign-placement.contract.md), so a template feeds the sign flow unchanged.
- **`anhDauDoPath` on the way in is the user's own file path**; on the way out it is the copy the BE made under
  `%AppData%\SipPack\template-assets\{id}`. On PUT, send the returned copy path back to keep the image, send a
  different path to replace it, send `null` to remove it.
- **PUT overwrites everything**, `positions` included — always send the full current state, never a patch.
- **`file-mau` returns a path, not bytes.** Fetch the content with the existing
  `GET api/core/document/sign/xem-file?path=…`. When `exists` is `false`, hide the preview instead of showing an error.
- **`xem-anh` is the second envelope-less endpoint** (after `sign/xem-file`): it feeds `<img src>` for seal and
  wet-signature previews, accepts only `.png/.jpg/.jpeg`, and returns HTTP 404 on any error.
- **Images are picked from the machine** via the desktop bridge message `pickImage` → `image:<path>` (PNG/JPEG filter).
  The FE sends that path in `anhDauDoPath` / `anhChuKyTuoiPath`.
- **Images are stamped when signing (2026-10-13).** `/ky-so` sends every `kind = 0` position, plus `kind = 1/2`
  positions whose image exists, with `sealImagePath = anhDauDoPath` / `wetSignatureImagePath = anhChuKyTuoiPath`
  (the BE copies). See [`sign-placement.contract.md`](sign-placement.contract.md).

- **`fontChuKySo` / `coChuKySo` / `mauChuKySo`** (nullable, migration `AddSignTemplateAppearance`) — font family,
  point size on A4 and `#RRGGBB` colour of the signature text. `/ky-so` sends them as `fontFamily` / `fontSize` /
  `textColor` to `sign/ky-thu-muc` (see [`sign-placement.contract.md`](sign-placement.contract.md)).
- **`file-mau` is no longer used by the FE**: the editor previews on a blank A4 page (595×842pt) like `sao_mai_fe`.

- **Templates are per machine.** If the data is moved to another Windows machine, the BE wipes all templates and
  their images at the next start (see `05-infrastructure.md`). The FE simply sees an empty list.

## FE implementation

- `/cau-hinh/template-chu-ky` — paged list (`find-paging`), search, delete; `/cau-hinh/template-chu-ky/moi` and
  `/cau-hinh/template-chu-ky/{id}` — editor (cert, reason/location, images, font/size/colour, drag-drop blocks on a
  blank A4 page or a chosen folder). Resizing the signature block changes the font size. Create = POST, then PUT.
- `/ky-so` — picks a template (`pageSize: -1`), defaults the certificate to the template's `thumbprint`, and passes
  `lyDoKy` / `noiKy` as the sign dialog defaults.
- **`pageSize: -1` returns every row** (the repo-wide `PagingExtension` convention); page numbers start at **1**.
- The paging envelope field is **`customData`** (one *m*). The FE model used to declare `custommData` (two *m*);
  that typo was corrected in `Sip.FE/src/app/shared/models/request-paging.base.models.ts` — BE is the reference.

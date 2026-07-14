# ToolSign integration & test notes (SIPPACK signing)

> Research + runtime-test log for adding a **folder-based digital-signing** feature to SIPPACK by driving the
> third-party **ToolSign** (NILER SIGN CLIENT) local service over HTTP. No SIPPACK code written yet.
> Related: [ca-trust-certificates-g2.md](ca-trust-certificates-g2.md), [verify-signature-timestamp.md](verify-signature-timestamp.md).

## 1. What ToolSign is
`ToolSignClient.exe` — a **.NET 5** hybrid app (WinForms tray UI **+** self-hosted ASP.NET Core Web API with Swagger).
Signing engine = `Asene.Core.Sign` + `iTextSharp`/`itextsharp.pdfa` 5.5.13 + `BouncyCastle` 1.8.9 +
`Egelke.EHealth.Client.Pki` 2.2.1 (RFC 3161 timestamp) + `GdPicture` 14.2.9. **Asene + GdPicture are commercial**
(usage right must come from the vendor; the tested build produced clean output, no watermark).

## 2. Endpoints & Swagger
- **Swagger UI:** `http://localhost:5000/` (root 301 → `/index.html`). OpenAPI JSON: `http://localhost:5000/swagger/v1.0/swagger.json`.
- Listens on Kestrel ports **5000–5009** (http on 5000/5002-5005, https on 5001/5006-5009). Route prefix `/api/v1.0/…`.
- No HTTP-layer auth enforced (OAuth is optional); the app login (`niler` account) unlocks the token session.

| Group | Endpoints |
|---|---|
| Cert | `GET /cert` (pops "Chọn chứng thư số" box, returns selected cert incl. `serial`), `/cert/{serial}`, `/cert/{serial}/status` |
| Sign | `POST /sign/pdf`, `/sign/word`, `/sign/word/advance`, `/sign/xml`, `/sign/xml/content`, `/sign/message`, `/sign/binary` |
| Verify | `POST /verify/pdf`, `/verify/xml`, `/verify/message`, `/verify/binary` |

`sign/pdf` = **multipart/form-data**: attach the PDF file (read from `Request.Form.Files`, not shown in schema) +
`responseType` (`path`｜`binary`=base64｜`file`=download), `llx/lly/urx/ury` (signature rect), `pageIndex`,
`displayType` (0 LOGO｜1 DETAIL｜2 BOTH｜3 NONE), `handSignature` (base64 img), `reason`.
Response JSON: `{ status, success, error, servertime, result:{ totalitem, data }, messages:[{ errorCode, message }] }`.
Signed output is written to `Temp/{guid}/<name>.signed.pdf`. `MaxSizeUpload` default **1 MB** (`appsettings.json`) — raise for big PDFs.

## 3. Signing flow (for the SIPPACK feature)
1. Select cert once: `GET /api/v1.0/cert` → user picks cert in the popup (no PIN here — selection reads the public cert only).
2. `POST /api/v1.0/sign/pdf` per file → ToolSign **validates the signer chain**, then (if valid) prompts the **token PIN**, then signs.
3. Output = a PAdES `ETSI.CAdES.detached` signature **with RFC 3161 timestamp** → verifiable by SIPPACK's existing verifier.

## 4. Prerequisites (end-user machine)
- **.NET 5 runtime** (EOL): `windowsdesktop-runtime-5.0.17` **and** `aspnetcore-runtime-5.0.17` — bundled in `ToolSign/…/trinh_dieu_khien/`.
- **Token middleware** `bit4id Universal MW` (+ `VGCAv1`) and the USB token driver; PKCS#11 `bit4xpki.dll`.
- The signer cert's **CA chain must be trusted by Windows** (see §5).

## 5. Trust prerequisite — why the G2 test cert failed
`errorCode 14 "Chứng thư số đã chọn không thể xác thực"` = signer chain not trusted. It happens **before** the PIN
step (so no PIN box appears), and a failed sign leaves a **placeholder signature by ToolSign's internal `CN=localhost`
cert** (no signingTime, no timestamp) — not a real signature.
- **G1 certs** (issuer `Cơ quan chứng thực số Chính phủ`) are already trusted in Windows → sign fine (verified: the
  sample `file/ký số/H49.64.33.2001.160.1.pdf`, signer *UBND phường Hà Tu*, has a valid RFC 3161 timestamp).
- **G2 certs** (issuer `CA phục vụ các cơ quan Nhà nước G2` → Root G2) fail until **Root G2** is a trusted root.
- **Fix used for testing (2026-07-14):** installed `rootcag2.crt` → `LocalMachine\Root` (elevated) and `cpg2.crt` +
  `dcscag2.crt` → `CurrentUser\CA`; the `Phạm Ngọc Chính` cert chain then built trusted. **Production will use a G1
  token → no Root G2 install needed**; Root G2 was only for the current G2 test cert.

## 5b. Signing result after the trust fix (2026-07-14)
- **Signing works** — the token produced a real `Phạm Ngọc Chính` signature embedding the full 3-cert chain
  (signer + CP_G2 + Root G2). Trust fix cleared `errorCode 14`.
- **But the API always returns `errorCode -1` "lỗi hệ thống"** on every `responseType` (`path`/`binary`/`file`) even
  though the signed file *is* written to `Temp/{guid}/<name>.signed.pdf`. Integration currently can only retrieve the
  output by reading that Temp path — fragile.
- **No RFC 3161 timestamp** on the output (unlike the G1 Hà Tu reference which has one). Root cause found by
  decompiling — see §7 (it is **not** a connectivity issue; the ToolSign UI never shows an error, it is just an
  info window).

## 6. Integration decisions & open items
- Run ToolSign as a **separate sidecar process** (.NET 5); SIPPACK (net9) calls its REST API via `HttpClient` — do **not** host in-process.
- New BE `SignController`/`SignService` (`Document/`) + FE page `pages/ky-so`; batch per file with SSE progress (mirror the Check flow).
- **Licensing:** calling ToolSign over HTTP adds no developer-license burden to SIPPACK, but the right to use ToolSign itself must be licensed from the vendor.
- **Open:** confirm license terms; decide signed-file output location (in-place vs `signed/`); raise `MaxSizeUpload`.
- **Blocker 1 — API `-1`** (root cause in §7): a swallowed exception from forced **PDF/A-1A** signing on non-PDF/A
  input. The signed file in `Temp` is actually valid; the API just reports failure. *Retest with real PDF/A sources.*
- **Blocker 2 — no timestamp** (root cause in §7): a **ToolSign bug** — it never attaches a timestamp, so its output
  can never pass SIPPACK's `NoTimestamp` gate. Needs a vendor fix, or SIPPACK adds the timestamp itself (see §7).

## 7. Root cause — decompiled `ToolSignClient.dll` + `Asene.Core.Sign.dll` (2026-07-14)
Decompiled with `ilspycmd`; signing engine is `Niler.Core.Sign.Services.SignService` (in `Asene.Core.Sign.dll`).
- **No timestamp = a code bug.** `SignService.SignPDF` builds `new TSAClientBouncyCastle("http://tsa.ca.gov.vn/")`
  but **discards it**, then calls `MakeSignature.SignDetached(appearance, sig, chain, null, null, null, 0, CADES)` —
  the `tsaClient` argument is `null`. So **no RFC 3161 timestamp is ever applied**, regardless of TSA reachability
  (`tsa.ca.gov.vn` is in fact reachable: GET 403 / TSP POST 400). Not fixable from the API — it's inside the DLL.
  **Runtime-confirmed:** re-signing the already-signed `H49.64.33.2001.160.1.pdf` gave a 2-signature file where
  sig#1 (UBND Hà Tu, pre-existing) has a timestamp but sig#2 (ToolSign/Phạm Ngọc Chính) does not — same file, so it
  is the signer code, not the input. (Re-signing via append also preserved sig#1 intact.)
- **`-1` = forced PDF/A-1A.** `SignPDF` signs via `PdfAStamper.CreateSignature(…, PDF_A_1A, append:true)`; on a
  non-PDF/A source the PDF/A conformance step throws **after** the signature bytes are already flushed → the
  controller's `catch` maps it to `ErrorCode.EXCEPTION` (`-1`) while a valid `.signed.pdf` remains in `Temp`.
- **Consequence for SIPPACK:** ToolSign as shipped cannot produce a timestamped signature. Realistic paths:
  (a) get a fixed vendor build (pass the TSA client; don't force PDF/A on non-PDF/A input);
  (b) read the valid `Temp` output and have **SIPPACK apply the RFC 3161 timestamp itself** (it already has the infra);
  (c) drop ToolSign and self-sign in SIPPACK (CNG token access + BouncyCastle PAdES-T) — also removes the license need.

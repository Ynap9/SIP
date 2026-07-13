# Spike log — testing `signingTool_v2` against real PDFs

> Empirical test of the Air-Invoice signing tool before integrating it into SIPPACK. Records the exact
> requests, responses, and the blocker found. Date: 2026-07-11. Tool: `KySo_Mai/signingTool_v2_26082024.exe`.
> Reference: [token-signing-tool.md](token-signing-tool.md).

## 1. Goal
Answer the four unknowns from the reference doc: does `/api/sign` sign a **PDF**, what `type`, is `base64XML` the
file bytes, and does the signed output carry a Ban Cơ yếu timestamp.

## 2. Environment
- Tool listens on `http://localhost:8089`; it is a **Spring Boot** app (errors are Spring's
  `{timestamp,status,error,path}` whitelabel JSON).
- Test files: `Sip/file/H49.61.8.2017.01.0000001.pdf` … `.0000011.pdf` (11 PDFs, ~60 KB each).
- Running processes: `signingTool_v2_26082024.exe` + `java.exe`. **No Token Manager process** was found.

## 3. Requests run and results
| # | Request | Input | Result |
|---|---|---|---|
| 1 | `GET /api/certs` | — | **200 OK** — but lists only **Windows cert-store** certs (CN=localhost, OpenIddict, MS-Organization-Access, GUIDs). **No USB-token cert** (no personal/org CN, no VGCA issuer). |
| 2 | `POST /api/sign` | `{serial, type:2, base64XML=base64(pdf)}` | **404 Not Found** — endpoint absent in this build. |
| 3 | `POST /api/sign-batch` | `{serial, invoiceItems:[…]}` | **404 Not Found** — endpoint absent in this build. |
| 4 | `POST /api/signing` | `{serial:7093f89663d9d00d, type:2, base64XML=base64(pdf)}` | **500 Internal Server Error**. |
| 5 | `POST /api/signing` | valid tiny **XML** `<invoice>…</invoice>`, `type ∈ {0,1,2,3}` | **500** for every type. |
| 6 | `GET /v3/api-docs`, `/swagger-ui*`, `/actuator*` | — | 404 (no introspection available). |

All POSTs returned **instantly (~100 ms)** — **no PIN prompt** ever appeared in Token Manager.

## 4. Findings
- **This build exposes only `/api/certs` and `/api/signing`.** The reference doc's "recommended" `/api/sign` and
  `/api/sign-batch` are **not present** (404) — the doc is ahead of the shipped tool. Integration must target
  `/api/signing` (`{serial, type, base64XML}` → `{code, desc, data}`).
- **The 500 is not about file format.** Valid XML fails exactly like the PDF, for every `type`. So `type`/PDF-vs-XML
  is **not** the cause.
- **Root cause of the 500: no usable signing cert.** Every serial in `/api/certs` is a **Windows-store dev cert**
  with no accessible private key; the **USB token's cert never appears**. The instant failure (no PIN prompt)
  confirms the tool never reached the token.

## 5. Bug / blocker (must fix before the spike can continue)
**BLOCKER — the USB token is not visible to the tool.** Per the tool's own note ("Cần cài và chạy tool Token
manager … để đọc được chứng thư số từ usb token"), the token cert is bridged by **Token Manager**, which is **not
running** here (no such process). Until it runs and the token cert shows up in `/api/certs`, signing returns 500 for
any input — we have no valid `serial` to sign with.

**"Token Manager" identified** = the token middleware **bit4id xPKI / VGCA PKI Manager**, bundled at
`ToolSign/trinh_dieu_khien/bit4id_xpki_1.4.10.764-ng-user-vgca-pkimgr-bwc.exe` (+ `VGCAv1_Installer.exe` for the VGCA
plugin). Install it, plug the token, open VGCA PKI Manager → the token cert becomes readable.
> Note: `ToolSign` (`ToolSignClient.exe`, a separate .NET VGCA signing client) also bundles the token drivers here.
> For the integration target, see §6–§7 — the Java tool turns out **not** to sign PDF.

## 6. Resolution — token now visible, but the tool still cannot sign PDF
After installing Token Manager, `GET /api/certs` **now lists the real token cert**: `CN=Phạm Ngọc Chính …`, issuer
`CA phục vụ các cơ quan Nhà nước G2, Ban Cơ yếu Chính phủ`, serial `30d8c57549bfa5dc`. But `POST /api/signing` with
that serial still returns **500** for both a PDF and a plain XML (instant, no PIN) — the failure is in the tool's
signing logic, not the cert.

**Decompiled the exe** (a Spring Boot fat-jar, package `vn.air.invoice.signtool`) to settle it:
- **Endpoints:** only `GET /api/certs` + `POST /api/signing` (`{serial NotBlank, base64XML, type Integer}`). No
  `/api/sign` or `/api/sign-batch` in this build — the reference `.docx` is ahead of the shipped tool.
- **`SignService.signing` is XML-only:** `base64XML` → parse to an **XML DOM** → insert an enveloped
  `<Signature …xmldsig#>` (SHA-256 / RSA / exclusive-C14N) at a fixed invoice/declaration tag → return the **signed
  XML string** (not base64, not a file). There is **no PDF code path**.
- **`type` (enum `SigningContext`) = `INVOICE` (tag `DLHDon`, ref `_15202`) or `DECLARATION` (tag `DLTKhai`, ref
  `_430`)** — it signs **e-invoice / tax-declaration XML**, keyed to those exact elements. A PDF (not XML) or a
  generic XML (missing `_15202`/`_430`) throws → the 500 we saw.
- **PKCS#11 bundled:** only `bkav_pkcs11.dll` / `fpt_pkcs11.dll` (BKAV/FPT tokens) — no VGCA/bit4id driver.

**Conclusion:** `signingTool_v2` is a **XAdES signer for Vietnamese e-invoices / tax-declarations**, not a document
signer. It **cannot sign SIPPACK's archival PDFs** — integrating it for PDF signing is not feasible.

## 7. Recommendation
Use **`ToolSign` (`ToolSignClient.exe`)** instead: it exposes `CertController` + `SignController.SignPDF` /
`SignPDFArchive` (PDF/A) / `SignFolder` + `VerifyController` — a real **PDF / PDF-A signer**, the correct fit for
signing archival documents. Get the user's decision before building tasks #7–#8.

# USB-token signing tool (`signingTool_v2`) — reference

> What the Air-Invoice `signingTool` is and how it would plug into SIPPACK. Source: `KySo_Mai/`
> (`signingTool_v2_26082024.exe`, `jdk-8u202-windows-i586.exe`, and the Air-Invoice integration `.docx`).
> **Reference only — no SIPPACK code integrates it yet.** Related: [verify-signature-timestamp.md](verify-signature-timestamp.md).

## 1. What it is
A **local signing service** that runs on the end-user's machine and signs data with a **USB token** (hardware
smartcard / chứng thư số). A web/desktop app talks to it over HTTP; the token holds the private key so the key never
leaves the device.

- **Runtime:** Java — needs **JDK/JRE 8** (`jdk-8u202`) installed; the exe launches a background HTTP server.
- **Prerequisites to sign:** the **Token Manager** app running + the **USB token plugged in**. Signing pops a
  **PIN/password** prompt in Token Manager.
- **Base URL:** `http://localhost:8089`. Every response is `{ "code", "desc", "data" }`; `code = "000"` = OK.

## 2. Local tool API (the only part SIPPACK calls)
| Endpoint | Method | Input | Output `data` |
|---|---|---|---|
| `/api/certs` | GET | — | list of certs on the token: `{ Subject, issuer, serial, startDate, endDate }` |
| `/api/sign` (recommended; `/api/signing` deprecated) | POST | `{ serial, type, base64XML }` | signed string |
| `/api/sign-batch` | POST | `{ serial, invoiceItems:[{ invoiceId, invoiceXmlData }] }` | `[{ invoiceId, signedData }]` (base64) |

- `serial` — picks which cert on the token (from `/api/certs`).
- `/api/sign-batch` signs many items in one PIN entry — the natural fit for signing a whole hồ sơ's files at once.

## 3. Out of scope (NOT the local tool)
Doc sections 2.4–2.7 (`api/v1/external/release-invoice-batch`, `get-doc-xml`, `sign-complete`,
`get-active-by-company`) are **Airdata Inv-Portal server** APIs for the invoice-issuing workflow. SIPPACK does not
use them — it only calls the three `localhost:8089` endpoints above.

## 4. Open unknowns (the doc is written for XML invoices, SIPPACK signs PDFs)
The `.docx` only shows **XML invoice** signing (`base64XML`, `invoiceXmlData`, `type: 2`). SIPPACK signs **archival
PDF** attachments, so these must be confirmed by a real test with the token **before** building:
1. **Does `/api/sign` sign a PDF** and return a signed **PDF** (PAdES), or only XML?
2. **What `type` value** selects PDF signing (example uses `type: 2` for an invoice).
3. **Is `base64XML` = base64 of the raw file bytes** (so a PDF is `base64(pdfBytes)`), and is `data` the base64 of
   the signed file?
4. **Does the signed PDF carry a Ban Cơ yếu RFC 3161 trusted timestamp?** SIPPACK's check currently **blocks
   packing** any PDF without one (`ErrorCodes.TimestampInvalid`, see the verify-timestamp doc). If the tool does not
   add one, either the token/flow must, or the blocking rule needs adjusting.

## 5. Intended fit in SIPPACK (agreed direction — not built yet)
- **Standalone "Ký số" action:** pick a hồ sơ folder + pick a cert (from `/api/certs`) → sign all PDFs in it →
  overwrite/save signed files, then the normal Check/Pack flow runs on the signed result.
- **Called from the BE (.NET):** the BE reads each PDF → base64 → `POST localhost:8089/api/sign` (or
  `/api/sign-batch`) → writes back the signed bytes. Keeps it same-process with Check/Pack, avoids browser CORS, and
  lets it reuse `SignatureVerifier` to confirm the result. FE only drives folder/cert selection + progress.
- **Verification loop:** after signing, run the existing `PdfSignatureReader` + `SignatureVerifier` on each file to
  show signer + timestamp verdict, so the user sees whether the signed file will pass packing.

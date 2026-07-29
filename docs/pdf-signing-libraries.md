# PDF signing libraries — free/permissive options

> Research for the signature-**creation** side. SIPPACK ships as a closed-source desktop app to state agencies,
> so a copyleft licence (AGPL) is disqualifying and a paid runtime licence is unwanted. Assessed 2026-07-20.
> Companion: [pdf-signing-readiness.md](pdf-signing-readiness.md), [plans/pdf-signing.plan.md](plans/pdf-signing.plan.md).

## 1. Licence constraint

The app is **distributed** (not SaaS) and its source is not published. That makes **AGPL/GPL viral** for us — the
whole reason ToolSign's iTextSharp stack was rejected. Acceptable: **MIT, Apache 2.0, BSD**.

## 2. Candidates

| Library | Licence | Does | Verdict |
|---|---|---|---|
| **System.Security.Cryptography.Pkcs** (BCL) | MIT | `SignedCms`, `Rfc3161TimestampRequest/Token` | ✅ **core** — already used by `SignatureVerifier` |
| **PDFsharp 6.2+** (`PdfSharp.Cryptography`) | MIT | PDF write + signature dict + `IDigitalSigner` + TSA URI | ✅ **replaces the hand-rolled writer** |
| **Pkcs11Interop.X509Store** | Apache 2.0 | USB token via PKCS#11; providers plug into `SignedCms` | ✅ **fallback if CNG cannot drive the token** |
| **BouncyCastle.Cryptography** | MIT | CAdES attributes, ASN.1 | ◻ optional — only if BCL proves insufficient |
| **disig TimeStampClient** | Apache 2.0 | RFC 3161 client | ◻ redundant — BCL already covers it |
| **PdfPig** | Apache 2.0 | PDF **read** only | ❌ cannot sign |
| **iText 7/8, iTextSharp 5** | AGPL **or** paid | full PAdES incl. LTV | ❌ licence |
| **Aspose / Syncfusion / IronPDF / Docotic / GemBox / VintaSoft** | Commercial | full PAdES | ❌ paid runtime |

## 3. This changes the plan's biggest risk

[plans/pdf-signing.plan.md](plans/pdf-signing.plan.md) decided to **hand-roll the PDF incremental-update writer**,
rejecting PDFsharp for "weak signing support". That was true when written — **digital signatures landed in PDFsharp
6.2.0 (2024-08-12)**, after the decision.

Plan step 5 (`PdfIncrementalSigner`) is the **highest-risk component**: byte-exact `/ByteRange` slots, xref, trailer
`/Prev`, patch-in-place. PDFsharp does that plumbing under MIT, and `IDigitalSigner` lets SIPPACK keep the part that
matters — building the CMS itself, with its own certificate, its own `signingCertificateV2`, its own TSA call, and
the token key. **Library does the PDF; SIPPACK keeps the cryptography.** That is the right split, and it deletes the
riskiest file from the plan.

## 4. Recommended stack

```
PDF container      → PDFsharp 6.2+ (MIT), custom IDigitalSigner
CMS / CAdES        → System.Security.Cryptography.Pkcs (BCL, MIT)
RFC 3161 timestamp → Rfc3161TimestampRequest + HttpClient → tsa.ca.gov.vn
Token private key  → Windows CNG (RSACng) — fallback: Pkcs11Interop.X509Store (Apache 2.0)
Verification       → existing ISignatureVerifier (unchanged)
```

Zero paid licences, zero copyleft.

## 5. Open questions — settle by spike, do not assume

These decide whether PDFsharp fits or is only partially usable. Each is cheap to answer and expensive to get wrong:

1. **SubFilter.** PAdES (ETSI EN 319 142) requires `/SubFilter /ETSI.CAdES.detached`. Does PDFsharp write that, or
   the legacy `/adbe.pkcs7.detached`? If it hardcodes the Adobe value, the dictionary needs patching.
2. **Preserving existing signatures.** Real hồ sơ files are often **already signed**; adding a signature must be a
   true incremental append leaving earlier revisions byte-intact. Undocumented for PDFsharp — verify against
   `ISignatureVerifier`, which enforces exactly this (outermost signature must reach EOF).
3. **`IDigitalSigner` contract.** Does it hand us the bytes to sign so we return a full CMS blob? If yes we control
   everything; if it only accepts a key/certificate, we lose control of the signed attributes.
4. **Token key access.** CNG (`RSACng.SignData`) unproven on this token — see
   [pdf-signing-readiness.md](pdf-signing-readiness.md) §4. Pkcs11Interop.X509Store is the Apache-2.0 fallback, and
   ToolSign's working reference used PKCS#11 (`bit4xpki.dll`), not CNG.
5. **Timestamps on .NET Framework are unsupported in PDFsharp** — irrelevant here (`net9.0`), but do not let the
   desktop build drift to Framework.

## 6. Notes

- PDFsharp keeps signing in a **separate `PdfSharp.Cryptography` assembly**, so the dependency is opt-in.
- `PdfSharpDefaultSigner` takes a TSA URI as its 3rd constructor argument — usable as a reference implementation even
  if SIPPACK ships its own `IDigitalSigner` (it must, for the Ban Cơ yếu TSA and token keys).
- Neither PDFsharp nor the BCL gives **PAdES-LTV/LTA** (DSS with embedded OCSP/CRL). Still deferred; only iText and
  the commercial libraries offer it off the shelf. Revisit if long-term archival verification becomes a requirement.

## 7. Sources

PDFsharp [licence](https://github.com/empira/PDFsharp/blob/master/LICENSE) (MIT, empira Software GmbH) ·
[signature docs](https://docs.pdfsharp.net/PDFsharp/Topics/PDF-Features/Signatures.html) ·
[Pkcs11Interop.X509Store](https://github.com/Pkcs11Interop/Pkcs11Interop.X509Store) (Apache 2.0) ·
[disig TimeStampClient](https://github.com/disig/TimeStampClient) (Apache 2.0) ·
[ETSI EN 319 142-1](https://www.etsi.org/deliver/etsi_en/319100_319199/31914201/01.01.00_30/en_31914201v010100v.pdf) (PAdES).

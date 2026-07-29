# Plan — Folder-based PDF signing with a USB token (PAdES-T)

> Build SIPPACK's own signing tool: pick a folder + a token cert → sign every PDF with a Ban Cơ yếu RFC 3161
> timestamp, so the output passes the existing `NoTimestamp` gate. Replaces the rejected third-party ToolSign.
> Context: [../toolsign-integration.md](../toolsign-integration.md) §7 (why ToolSign cannot work),
> [../verify-signature-timestamp.md](../verify-signature-timestamp.md) (the verifier this must satisfy).
> **Status: IMPLEMENTED — this file is the original intent, not the shipped design.** The build diverged (e.g.
> `CertificateProvider`/`StoreSigningKey` instead of `TokenCertProvider`, routes `sign/chung-thu-so` and
> `sign/ky-thu-muc`, a PDF/A gate, output `_daky_` beside the original). The authority on what exists is
> `.claude/be/plans/{pdf-signing-be.plan.md, pdf-signing-be.decisions.md, pdf-incremental-signing.plan.md}`.

## Requirements / Input
- **Input**: a hồ sơ folder (or `attachment/`), a cert `serial` chosen by the user, an output mode, `reason`/`location`.
- **Preconditions**: USB token plugged in; token middleware (`bit4id xPKI` / VGCA PKI Manager) installed so the token
  cert appears in `X509Store(My, CurrentUser)` with `HasPrivateKey`. Production signs with a **G1** token.
- **Reuse as-is**: `ISignatureVerifier` (verdict), `SignatureConstants` (OIDs, tolerance).
- **Verified facts**: `tsa.ca.gov.vn` is reachable (GET 403 / TSP POST 400 — it answers TSP only). Real files are PAdES
  `ETSI.CAdES.detached`. TSA `genTime` is **true UTC** (the 7h skew seen on legacy files is the *signer* writing local
  time into `signingTime`), so signing with a true-UTC `signingTime` round-trips to `ValidTrusted` — measured residual
  233 s < 300 s tolerance. See [../bug/genTime-offset-compensates-on-the-wrong-side.md](../bug/genTime-offset-compensates-on-the-wrong-side.md).

## Steps
1. **Constants + DTOs.** `Sip.be.Shared/Constants/Documents/SigningConstants.cs`: TSA URL `http://tsa.ca.gov.vn/`,
   `OidSigningCertificateV2 = 1.2.840.113549.1.9.16.2.47`, `/Adobe.PPKLite`, `/ETSI.CAdES.detached`,
   `ContentsPlaceholderBytes = 16384`. `Dtos/Sign/`: `SignCertDto`, `SignRequestDto`, `SignProgressDto`,
   `SignFileResultDto`. `dotnet build`.
2. **Token cert provider.** `Sip.be.External/Documents/TokenCertProvider.cs` (+ `ITokenCertProvider` in Application):
   read `X509Store(My, CurrentUser)` → keep `HasPrivateKey` + not expired + EKU `digitalSignature`; return
   subject/issuer/serial/validity. `SignController.GetCerts` → `GET api/core/document/sign/certs`. `dotnet build`.
   - *Decision:* Windows CNG, not PKCS#11 — zero-dep and the middleware already bridges the token; CNG raises the
     PIN prompt itself. Trade-off: no control over slot/PIN caching (see step 6).
3. **TSA client.** `Sip.be.External/Documents/TimestampClient.cs` (+ `ITimestampClient`):
   `Rfc3161TimestampRequest.CreateFromSignerInfo(si, SHA256, requestSignerCertificates: true)` → `HttpClient` POST
   `application/timestamp-query` → `request.ProcessResponse(bytes, out _)` → `Rfc3161TimestampToken`. Fail-closed:
   no token ⇒ abort the file (never emit an untimestamped signature — that is ToolSign's exact bug). `dotnet build`.
4. **CMS signer.** `Sip.be.External/Documents/CmsPdfSigner.cs`: detached `SignedCms` over the ByteRange bytes;
   `CmsSigner(IssuerAndSerialNumber, cert)`, SHA-256, `IncludeOption = WholeChain`; signed attrs = `contentType`,
   `messageDigest`, `signingTime` (true UTC), **`signingCertificateV2`** hand-built with `AsnWriter`; then attach the
   step-3 token as unsigned attr `1.2.840.113549.1.9.16.2.14`. `dotnet build`.
   - *Decision:* `signingCertificateV2` is built by hand because .NET does not add it; without it the file is not
     CAdES-conformant and Adobe/Foxit may reject it even though SIPPACK's verifier (which does not check it) passes.
5. **PDF incremental-update writer — invisible signature.** *(SUPERSEDED 2026-07-21 by
   [../../.claude/be/plans/pdf-incremental-signing.plan.md](../../.claude/be/plans/pdf-incremental-signing.plan.md)
   — PDFsharp cannot append, so the writer was hand-rolled after all; PDFsharp only draws the appearance.)*
   `Sip.be.External/Documents/PdfIncrementalSigner.cs`:
   append an incremental update (sig dict `/Filter /Adobe.PPKLite /SubFilter /ETSI.CAdES.detached /M`, `/Contents`
   zero-filled placeholder, `/ByteRange` written with padded slots, new `/AcroForm` + `/Annots` refs, new xref +
   trailer `/Prev`) → compute the real ByteRange → sign the two ranges (step 4) → patch `/Contents` + `/ByteRange`
   in place. Must preserve any pre-existing signature (append-only, never rewrite earlier bytes). `dotnet build`.
6. **SignService + SSE.** `Application/Document/Implements/SignService.cs` + `Api/Controllers/Documents/SignController.cs`:
   `POST api/core/document/sign/folder` (SSE, mirror `CheckController`: 3 headers, `DisableBuffering()`, camelCase +
   `UnsafeRelaxedJsonEscaping`, flush per event). Scan `*.pdf`, sign **sequentially** per file, then run
   `ISignatureVerifier.Verify` on the result and emit its verdict in the same event. Errors are per-file, not
   fail-fast. Add `ErrorCodes.SignFailed`. `dotnet build`.
   - *Decision:* sequential — the token is a single hardware session and CNG PIN caching is per key handle; parallel
     signing risks a PIN prompt per file or a token lock. Not the ClosedXML thread-safety issue, but the same shape.
   - *Decision:* output mode is a request field (`InPlace` | `SignedFolder`), asked on the FE each run. `InPlace`
     writes to a temp file and swaps only after verify passes, so a failure never destroys the original.
7. **FE page.** `pages/ky-so` (+ `services/sign.service.ts`, `models/sign.models.ts`): folder picker (guard on
   `DesktopService.isDesktop`, `Utils.normalizeFolderPath`), cert dropdown from step 2, output-mode radio, per-file
   progress table with the verify verdict badge, `<name>.spec.ts`. Add a menu entry. `npm run build`.
8. **Visible signature appearance.** Extend step 5: `/Rect`, `/AP /N` form XObject (text + optional logo image
   XObject + resource dict), attach the widget to the target page's `/Annots`. Requires reading the page tree of the
   source PDF, not just appending. `dotnet build`.

## Expected output
- `GET sign/certs` lists the real token cert (e.g. `Phạm Ngọc Chính`, issuer `CA phục vụ các cơ quan Nhà nước G2`).
- Signing a folder produces PDFs that `ISignatureVerifier.Verify` rates **`ValidTrusted`** with a TSA + trusted time —
  i.e. they pass the pack gate that every ToolSign output fails. Adobe/Foxit also report the signature valid.
- A pre-signed file gains a second signature with the first left intact.

## Decisions
- **Build, don't integrate.** ToolSign can never emit a timestamp (`SignDetached(…, tsaClient: null)` inside
  `Asene.Core.Sign.dll`) and fakes `-1` by forcing PDF/A-1A. Building also removes the vendor licence obligation and
  the .NET 5 (EOL) sidecar. See [../toolsign-integration.md](../toolsign-integration.md) §7.
- ~~**Hand-rolled incremental update**, no iText7 (AGPL/viral or paid) and no PDFsharp (weak signing support)~~ —
  **SUPERSEDED 2026-07-20.** The iText7 rejection stands (licence), but PDFsharp gained digital signatures in
  **6.2.0** (2024-08-12, MIT) *after* this decision, including `IDigitalSigner` for custom signers. Step 5 is the
  highest-risk file in this plan and PDFsharp does that plumbing for free. Re-evaluate step 5 against
  [../pdf-signing-libraries.md](../pdf-signing-libraries.md) before implementing it.
  **Re-evaluated 2026-07-21 → the original decision stands**: PDFsharp always rewrites the whole document, which
  destroys any existing signature ([../bug/pdfsharp-rewrites-document-destroying-existing-signature.md](../bug/pdfsharp-rewrites-document-destroying-existing-signature.md)),
  so the incremental writer is hand-rolled and PDFsharp is kept only for drawing the appearance.
- **Invisible first (steps 1–7), appearance last (step 8).** Appearance is the highest-risk part of a hand-rolled
  writer; staging it last guarantees a working signer even if step 8 overruns. Step 8 is still in v1 scope.
- ~~**Signer-chain trust is out of scope.**~~ — **SUPERSEDED 2026-07-21.** `SignatureVerifier.IsTrustedSigner` now
  gates the signer's chain too (G1 tried first, then G2), so a G1 production token still passes — but an untrusted
  signer is rejected. See [signer-trust-gate.plan.md](signer-trust-gate.plan.md).
- *Known duplication (not touched):* `ByteRangeRegex` + `ExtractContents` already exist in both `PdfSignatureReader`
  and `SignatureVerifier`; this feature writes rather than scans, so no third copy is introduced.

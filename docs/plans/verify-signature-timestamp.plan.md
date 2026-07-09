# Plan — Verify digital-signature trusted timestamp (Ban Cơ yếu TSA)

> Implement the check that a PDF's signing time is proven by an RFC 3161 timestamp from the Ban Cơ yếu TSA.
> Spec: [../verify-signature-timestamp.md](../verify-signature-timestamp.md) (EN) / [../verify-chu-ky-so-dau-thoi-gian.md](../verify-chu-ky-so-dau-thoi-gian.md) (VN).

## Requirements / Input
- **Trust anchor**: `Sip.be.Api/rootcag2.crt` (Root G2, already placed & verified; **gitignored** — provided
  out-of-band from `http://ca.gov.vn/pki/pub/crt/rootcag2.crt`). The **SHA-256 pin lives in code** as the source of
  truth: `F04BA4B459A7C9A1B971AD9B1CB833E695A80C79AEA63B174B66A70EDFE2FDB8`. Cert file is validated against it on load.
- Per-attachment PDF (reuse the Check flow's `attachment/` scan). Non-PDF → `N/A`.
- Verified facts (13 real files): PAdES `ETSI.CAdES.detached`; TSA `Máy chủ cấp dấu thời gian 02`; token `genTime`
  stored **7h behind true UTC** → must add +7h before comparing to `signingTime`.

## Steps
1. **Constants + asset wiring.** New `Sip.be.Shared/Constants/Documents/SignatureConstants.cs`: pin SHA-256, cert
   file name `rootcag2.crt`, OIDs (timestamp `1.2.840.113549.1.9.16.2.14`, signingTime `1.2.840.113549.1.9.5`, EKU
   `1.3.6.1.5.5.7.3.8`), `VgcaGenTimeOffsetHours=7`, `ToleranceSeconds=300`. In `Sip.be.Api.csproj` add
   `<Content Include="rootcag2.crt"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>`. `dotnet build`.
   - *Decision:* fingerprint pin (code) is the trust root; the `.crt` file is only material, rejected if hash ≠ pin.
2. **DTO.** `Dtos/Check/TimestampVerifyDto.cs` + enum `TimestampVerdict` (`ValidTrusted`, `NoTimestamp`,
   `UntrustedTsa`, `TimeMismatch`, `NotApplicable`) with `TrustedTime`, `SigningTime`, `TimestampAuthority`, `Reason`.
   Add `TimestampVerifyDto? Timestamp` to `SignatureInfoDto`. `dotnet build`.
3. **SignatureVerifier** (static, zero-dep, `Document/Implements/SignatureVerifier.cs`, mirrors `PdfSignatureReader`):
   per signature — `SignedCms.Decode`; read `signingTime`; locate token (unsigned attr OID …2.14, or a
   `/Type /DocTimeStamp` `/ETSI.RFC3161` signature); `Rfc3161TimestampToken.TryDecode`;
   `VerifySignatureForSignerInfo`; `X509Chain` with **custom trust anchor = pinned Root G2** (loaded once via a
   **PEM-or-DER loader** — the ca.gov.vn download is PEM — hash-checked against the pin), ExtraStore = token certs,
   revocation offline; require chain root SHA-256 == pin;
   `trustedTime = TokenInfo.Timestamp.AddHours(7)`; verdict from the §5 table. `dotnet build`.
4. **Wire into CheckService.** In the stage-1 attachment loop call `SignatureVerifier.Verify(file)` and attach to
   `CheckProgressDto.Signature.Timestamp` (single) and `FileCheckResultDto.Signature.Timestamp` (multi), alongside the
   existing `PdfSignatureReader`. A **blocking** verdict (`NoTimestamp`/`UntrustedTsa`/`TimeMismatch`, PDF only) is an
   **error**: emit an error `CheckProgressDto` (code `TimestampInvalid`) and increment `errorCount`. `NotApplicable`
   (non-PDF) does **not** block — warning only. `dotnet build`.
5. **Block on failure.** Add `ErrorCodes.TimestampInvalid` (+ message). Failing files count toward `errorCount`
   (step 4) → the stage summary becomes "không đủ điều kiện đóng gói"; in multi the hồ sơ is marked failed and
   excluded from packing. `dotnet build`.
6. **FE.** Extend `models/check.models.ts` (`ISignatureInfo.timestamp`), add a "Dấu thời gian" column/badge in
   `check.html` + `dong-goi/multi` (Hợp lệ / Không có / Giả / Lệch giờ / Không áp dụng, tooltip = TSA + trustedTime).
   For `NotApplicable` (non-PDF): a hồ sơ that is otherwise valid but has non-PDF files → on đóng gói show a confirm
   "Đang phát triển trình kiểm tra ký số cho file không phải PDF, vui lòng liên hệ admin. Vẫn đóng gói?"; pack only if
   the user confirms. `npm run build`.

## Expected output
- Every attachment PDF gets a `TimestampVerdict`; the FE check table shows a "Dấu thời gian" badge with TSA + trusted
  time. On the 13 sample files all resolve to `ValidTrusted` (delta −2s…−131s after +7h).
- No genuine VGCA file is rejected (the +7h correction is applied); a missing / forged / foreign-TSA timestamp is a
  hard error that **blocks packing**.

## Decisions
- **+7h VGCA correction** is mandatory (measured); never compare raw UTC with a tight tolerance.
- Native `System.Security.Cryptography.Pkcs` (net9 → `Rfc3161TimestampToken` available); zero-dep like `PdfSignatureReader`.
- **Offline**: OCSP/CRL revocation out of scope for v1; pin Root G2 by SHA-256; TSA cert read from the token (not pinned).
- Cert file **gitignored**; must be present at build/deploy (installer step) — pin constant guards integrity.
- **Block on failure (decided):** a PDF without a valid Ban Cơ yếu timestamp is a hard error that blocks packing
  (`ErrorCodes.TimestampInvalid`), same as other check failures.
- **Non-PDF (decided):** signature check for non-PDF is **under development** → `NotApplicable`, **warn** (message:
  "…vui lòng liên hệ admin"), do **not** block; packing still allowed after the user confirms.
- **Cert loader accepts PEM and DER** (the ca.gov.vn download is PEM): detect `-----BEGIN CERTIFICATE` →
  `X509Certificate2.CreateFromPem(text)` (cert-only; NOT `CreateFromPemFile`, which wants a private key), else
  `X509CertificateLoader.LoadCertificateFromFile`. Pin by the cert **DER fingerprint** (`GetCertHashString(SHA256)`),
  never the file-bytes hash. See [bug/ca-trust-cert-pem-vs-der-loader.md](../bug/ca-trust-cert-pem-vs-der-loader.md).

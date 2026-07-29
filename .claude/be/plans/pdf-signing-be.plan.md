# Plan — PDF signing (BE), part 1 of 2: requirements & steps

> **Part 1 of 2.** Requirements, build steps, expected output.
> **Next:** [pdf-signing-be.decisions.md](pdf-signing-be.decisions.md) — the design decisions behind these steps
> (parallel pipeline, cache lifetime, token monitoring, PIN handling). **Read both before implementing.**
>
> Refines the BE half of [pdf-signing.plan.md](../../../docs/plans/pdf-signing.plan.md). Reference:
> [appearance](../../../docs/pdf-signature-appearance.md), [libraries](../../../docs/pdf-signing-libraries.md),
> [readiness](../../../docs/pdf-signing-readiness.md).

## Requirements / Input

- **Input**: the **token certificate to sign with** (`Thumbprint`, from `GET sign/chung-thu-so`) **+** `SourcePath`
  — an absolute path to **one PDF** or **a folder** of PDFs; optional `Reason` / `Location`.
- **Token verified BEFORE any file is signed**: a run aborts up front if the certificate cannot actually sign —
  signing 500 files with a bad certificate produces 500 unusable files.
- **Verify once per run, not per file.** The expensive checks (test-sign, chain build) run once; the result **and
  the private-key handle** are cached for the batch.
- **PIN is prompted once.** The middleware raises its own PIN dialog when the key is first used (the pre-flight
  test-sign); holding the key handle keeps that session alive for the whole batch.
- **Token monitored during the run.** If the token is unplugged mid-batch, stop **immediately**, mark **every
  remaining file failed**, and clear the cache.
- **Cache is cleared when the run ends** (success, failure, cancellation, or unplug). Re-running after re-plugging
  must verify again and prompt for the PIN again.
- **Output folder**: beside the source, `{tên thư mục gốc}_đãký_{yyyyMMdd_HHmmss}`; for a single-file input the base
  name is that file's **parent folder**. Originals are never modified.
- **Result**: output folder path + `SuccessCount` + `FailedCount` + per-file verdict. **Progress** streams over
  **SSE**, mirroring the Check flow. Non-PDF files are **skipped**, not counted as failures.
- **Reuse as-is**: `ISignatureVerifier`, `ICertificateProvider`, `SignatureConstants`, `BaseService.VietnamNow`,
  `BaseController.OkException`.

## Steps

1. **Constants + DTOs + error codes.** ✅ *done.* `SigningConstants` (TSA URL, `SubFilter`, appearance metrics from
   the appearance doc, folder suffix, parallelism bounds); `Dtos/Sign/` (`SignRequestDto`, `SignFileListDto`,
   `SignProgressDto`, `SignFileResultDto`, `SignResultDto`, `TokenVerifyDto`); `ErrorCodes` + `ErrorMessages`
   (`SignSourceNotFound`, `SignNoPdfFound`, `SignTokenInvalid`, `SignFailed`). `dotnet build`.
2. **Timestamp client.** ✅ *done.* `ITimestampClient` + `External/TimestampClient`:
   `Rfc3161TimestampRequest.CreateFromHash(SHA256(signatureValue), …, nonce, requestSignerCertificates: true)` →
   POST `application/timestamp-query` → `ProcessResponse`. Takes raw bytes, not `SignerInfo`, so the Application
   layer needs no Pkcs package. **Fail-closed**: no token ⇒ that file fails. `dotnet build`.
3. **Signing key + pre-flight + cache + monitoring.** ✅ *done.* `ISigningKey` (`Verify`, `SignData`,
   `IsTokenPresent`, `ClearCache`) + `External/StoreSigningKey`. Checks in order:
   | Check | Why it must run before the batch |
   |---|---|
   | found by thumbprint, `HasPrivateKey`, not expired at `VietnamNow`, KeyUsage allows signing | cheap metadata gate — the row may be stale, public-only, or expired |
   | **test-sign 32 dummy bytes** | only proof the token is **physically present** and the PIN works; metadata never touches the key. This is where the PIN dialog appears |
   | **chain to pinned Root G1 ∨ G2** | otherwise every signed file later fails the pack gate as `UntrustedSigner` |
   Reuses `SignatureVerifier.IsTrustedSignerCert` (made `internal`, same assembly) — no duplicated pinning code.
   `dotnet build`.
4. **PDF signer.** ⚠️ *superseded — see
   [pdf-incremental-signing.plan.md](pdf-incremental-signing.plan.md), which is **done**.* This step assumed
   PDFsharp's `DigitalSignatureHandler` would **append** to an already-signed PDF. It does not: `document.Save()`
   re-serialises the whole document and kills every pre-existing signature
   ([bug log](../../../docs/bug/pdfsharp-rewrites-document-destroying-existing-signature.md)). The container is now
   written by SIPPACK's own `PdfIncrementalSigner`; PDFsharp is kept only to draw the appearance, and the custom
   CMS builder (`signingCertificateV2`, `signingTime`, TSA token as unsigned attribute
   `1.2.840.113549.1.9.16.2.14`) carried over unchanged.
5. **SignService — orchestration, parallel pipeline, token monitor.** ✅ *done.* `ISignService` +
   `Implements/SignService.cs`:
   - `ListPdfFiles(path)` → `SignFileListDto` (name, size, already-signed flag).
   - `VerifyToken(thumbprint)` → `TokenVerifyDto` so the FE can validate before starting.
   - `SignAsync(SignRequestDto, CancellationToken)` → `IAsyncEnumerable<SignProgressDto>`: pre-flight **first**
     (abort with `UserFriendlyException(SignTokenInvalid)`) → resolve source → collect `*.pdf` → create output
     folder (`VietnamNow`) → emit **manifest** → start the **token-monitor loop** → process → emit **summary**.
   - **Monitor**: polls `IsTokenPresent` on an interval; on false it cancels the run, every unprocessed file is
     reported failed with a token-removed reason, and `ClearCache()` is called.
   - Document failures are **per file**; never fail-fast. `ClearCache()` in `finally`.
   - After writing each file, run `ISignatureVerifier.Verify` and carry the verdict in the event.
   `dotnet build`.
6. **Controller + DI.** ✅ *done.* `SignController`: `GET sign/danh-sach-file`, `POST sign/kiem-tra-token` (plain
   `ApiResponse`), `POST sign/ky-thu-muc` (**SSE** — copy `CheckController`'s shape exactly). Register
   `ISignService`, `ITimestampClient`, `ISigningKey`, `IPdfSigner` as `AddScoped` in `Program.cs`. `dotnet build`.

## Expected output

- `POST sign/kiem-tra-token` returns a per-check `TokenVerifyDto`, naming *which* check failed.
- `GET sign/danh-sach-file?path=…` lists the PDFs to be signed.
- `POST sign/ky-thu-muc` streams one `SignProgressDto` per file, ending with a summary carrying `OutputFolder`,
  `SuccessCount`, `FailedCount`. An invalid token aborts before file 1; unplugging mid-run stops immediately and
  fails every remaining file. Output folder `{gốc}_đãký_{yyyyMMdd_HHmmss}`; originals untouched.
- Each signed file, re-checked by `ISignatureVerifier`, carries a **real Ban Cơ yếu timestamp** and verdict
  `ValidTrusted`. With a dev PFX the verdict is exactly `UntrustedSigner` and **nothing worse** —
  `InvalidSignature` means the writer is wrong; `NoTimestamp` / `UntrustedTsa` means the TSA path is wrong.
- The PIN dialog appears **once** per run, at the pre-flight.

> **Continue to [pdf-signing-be.decisions.md](pdf-signing-be.decisions.md)** — it explains why the pipeline is
> staged, why the cache exists and when it dies, and why the PIN is never stored.

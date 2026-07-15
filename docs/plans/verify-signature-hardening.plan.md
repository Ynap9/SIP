# Plan — Harden `SignatureVerifier` (document integrity, all-signatures rule, chain time)

> Make `ValidTrusted` prove the **document**. Today it proves only "a genuine VGCA TSA timestamped *some* signature
> value" — a tampered file, a transplanted CMS blob, and a forged block hiding behind a genuine one all pass.
> Signer **identity** is the companion plan: [signer-trust-gate.plan.md](signer-trust-gate.plan.md) — needed for the
> gate to be sound, but separable and blocked on material.
> Bugs: [../bug/verifier-never-checks-document-integrity.md](../bug/verifier-never-checks-document-integrity.md),
> [../bug/verifier-first-signature-block-wins.md](../bug/verifier-first-signature-block-wins.md),
> [../bug/verifier-chain-verification-time-defaults-to-now.md](../bug/verifier-chain-verification-time-defaults-to-now.md).

## Requirements / Input
- **Domain rule (decided):** a file may carry **any number of signatures**; **every** one is verified and **all**
  must pass. One failing signature fails the file. TT05 says nothing about signature count — searched: the only
  near-miss fields are `inforSign` ("Ký hiệu thông tin") and `autograph` ("Bút tích"), neither is a digital
  signature. This is SIPPACK policy, not a standard requirement.
- **Fixtures**: `file/H49.64.33.2015.315.{1..5}.pdf` — real PAdES-T, one signature each, signer `Ủy ban Nhân dân
  phường Hà Tu`, TSA `Máy chủ cấp dấu thời gian 02`. All 5 are `ValidTrusted` today and **must stay `ValidTrusted`
  after every step** — the regression bar. Each already holds **2 revisions** (`%%EOF` ×2): the signature *is* an
  incremental update, so incremental update is normal, not a tamper signal.
- **G2 material present + pin-verified** in `Sip.be.Api/` (`rootcag2.crt`, `cpg2.crt` — DER, `dcscag2.crt`);
  filenames are load-bearing (`File.Exists` + csproj + `Sip.BE/.gitignore:49-51`), a mismatch silently nulls the
  anchor and verdicts **every** file `UntrustedTsa`. See [../ca-trust-certificates-g2.md](../ca-trust-certificates-g2.md).
- Zero-dep constraint stands: `System.Security.Cryptography.Pkcs` only.

## Steps
1. **Verdicts + DTO.** Add blocking verdict `InvalidSignature` (signature does not match the document), distinct from
   `NoTimestamp` so "unsigned" ≠ "forged". Extend `TimestampVerifyDto` with `List<SignatureDetailDto> Signatures`
   (signer, verdict, reason) — with N signatures a file-level verdict alone is not diagnosable. Add
   `ErrorCodes.SignatureInvalid`. `dotnet build`.
2. **Verify every signature; all must pass.** Replace `if (result != null) return result;` — scan **all**
   `/ByteRange` blocks and, within each, **every** `SignerInfo` (not just `[0]`). Collect per-signature results and
   return the **worst**, fail-closed. Zero signatures ⇒ `NoTimestamp`. `dotnet build`.
   - *Decision:* explicit ranking, worst → best: `InvalidSignature` > `UntrustedSigner` (companion plan) >
     `UntrustedTsa` > `NoTimestamp` > `TimeMismatch` > `ValidTrusted`. "First match wins" is attacker-controlled —
     they choose byte order; measured: swapping two blocks flips the file verdict between `NoTimestamp` and
     `ValidTrusted`.
   - *Decision:* skip blocks that do not decode to a `SignedCms` rather than failing the file — `ByteRangeRegex`
     scans raw bytes, so a literal `/ByteRange` inside a content stream must not be mistaken for a signature.
3. **Fix document integrity (critical).** Per signature: extract the signed ranges `[start1,start1+len1)` +
   `[start2,start2+len2)` (use `Groups[4]` — today unused), build `new SignedCms(new ContentInfo(content),
   detached: true)`, `Decode(der)`, `CheckSignature(verifySignatureOnly: true)` → throws ⇒ `InvalidSignature`.
   Assert the ranges cannot lie: `start1 == 0` and the gap holds exactly this `/Contents`; and **the outermost
   signature (largest `start2+len2`) must reach EOF**. `dotnet build`.
   - *Decision (measured):* do **not** require every signature to reach EOF. Under incremental update — how a second
     signature is added — signature #1 legitimately ends at its own revision: on `…315.1.pdf`, appending 572 bytes
     leaves `start2+len2 = 718921` vs `fileLength = 719493` while `CheckSignature` still passes. A blanket EOF rule
     would reject every genuine multi-signature file. Requiring it of the **outermost** signature still blocks an
     appended unsigned revision and guarantees every byte is covered by at least one signature.
4. **Fix TSA chain verification time.** Compute `trustedUtc` (with the +7h VGCA correction) **before** the chain
   build and pass it into `ChainsToRoot`, which sets `chain.ChainPolicy.VerificationTime`. Keep the Root G2 anchor
   and the `ChainElements[^1]` pin check unchanged. `dotnet build`.
   - *Decision:* be explicit about `DateTimeKind` — this exact code already carries a 7h timezone quirk; pass an
     unambiguous instant and assert it in a test rather than relying on implicit `DateTime` conversion.
5. **FE surfacing.** Extend `models/check.models.ts` with the new verdict + `signatures[]`; render **one row per
   signature** in the "Dấu thời gian" cell of `check.html` and `dong-goi/multi` (badge + signer + reason each), so a
   file failing on 1 of 3 signatures shows *which* one instead of a single opaque badge. `npm run build`.
6. **Regression fixtures.** Port the PoC: (a) 5/5 real files stay `ValidTrusted`; (b) 1-byte tamper inside a signed
   range ⇒ `InvalidSignature`; (c) genuine CMS transplanted into an unrelated file ⇒ `InvalidSignature`; (d) bytes
   appended after the outermost signature ⇒ blocking; (e) two-block file ⇒ same verdict **in both orders**
   (`[genuine, forged]` / `[forged, genuine]`) — order must not matter; (f) expired TSA leaf valid at stamp time ⇒
   passes. `dotnet build`.
7. **Update the spec.** Rewrite [../verify-signature-timestamp.md](../verify-signature-timestamp.md) §4–§5 and §7:
   the all-signatures rule, the integrity step, the outermost-EOF rule, the new verdict, and `VerificationTime`.

## Expected output
- The 5 real files stay `ValidTrusted` — no genuine file is rejected.
- Tampering a byte, transplanting a CMS blob, appending an unsigned revision, and hiding a forged signature behind a
  genuine one all become **blocking**. The PoC's fake 12 KB "TÀI LIỆU BỊA ĐẶT" file no longer passes.
- `ValidTrusted` then means: **every** signature is unmodified since signing, at a time proven by the Ban Cơ yếu TSA,
  with no byte left unsigned. It does **not** yet mean the signer is trusted — that is the companion plan.

## Out of scope / debt
- **Signer identity** → [signer-trust-gate.plan.md](signer-trust-gate.plan.md). Without it a self-signed cert with a
  genuinely obtained timestamp still passes every step here. The two plans are two legs of one stool.
- **DEBT — multi-signature aggregation is unverified.** Step 2's fix cannot be proven by the PoC's synthetic
  two-block file: after step 3 both blocks fail integrity, so fixture (e) would pass for the wrong reason. Proving
  "all must pass" needs a **real** two-signature file. ToolSign can generate one — re-signing preserves sig#1 and
  adds an untimestamped sig#2 — but needs the token + PIN. **Highest-value missing fixture.**
- **PAdES-LTV `/DocTimeStamp`** — documented but unimplemented
  ([../bug/verifier-ltv-doctimestamp-not-implemented.md](../bug/verifier-ltv-doctimestamp-not-implemented.md)).
  Still a real false negative; deferred only because no LTV fixture exists.
- **Revocation (OCSP/CRL)** — out of scope per the spec; unchanged.
- **Root G2 load failure is silent** (`Lazy` → `null`, no log) — worth a log line when touching the loader.

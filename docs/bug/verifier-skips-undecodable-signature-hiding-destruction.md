# BUG: a destroyed signature is silently SKIPPED, so the file verdict looks healthy

> **Status: FIXED 2026-07-20** — see [§6 Verification](#6-verification-after-the-fix) for the measured result.
> **Severity: HIGH — the verifier reported `TimeMismatch` on a file whose original signature had just been
> destroyed.** Runtime-confirmed 2026-07-20, discovered while testing the signing feature.
> Companion: [pdfsharp-rewrites-document-destroying-existing-signature.md](pdfsharp-rewrites-document-destroying-existing-signature.md).

## 1. Summary
`SignatureVerifier.Verify` skips any `/ByteRange` block that fails to decode into a `SignedCms`. That rule exists
for a good reason — the literal string `/ByteRange` can appear inside a content stream and is not a signature —
but it does not distinguish *"this was never a signature"* from *"this was a signature and something broke it"*.

Measured consequence: a file whose pre-existing signature had been invalidated (99.6 % of its signed range
rewritten) came back **`TimeMismatch`**, a *time* complaint about the **new** signature, with no mention that the
old one was gone. `InvalidSignature` never appeared.

## 2. Evidence (runtime)
The output of the first successful SIPPACK signing run, verified by `ISignatureVerifier`:

```
verdict      : 3   (TimeMismatch)
verifyReason : "Giờ ký tự khai lệch 25443s so với dấu thời gian tin cậy."
```

The same file, inspected directly: **two** `/ByteRange` arrays are present, and the first one's covered bytes no
longer match what it was signed over (see the companion bug for the byte counts). A destroyed signature should
rank `InvalidSignature` (6) — the worst verdict, which under worst-wins would have decided the file.

It did not, so the block was skipped before `CheckSignature` ever ran: the rewrite mangled the `/Contents` blob
badly enough that `SignedCms.Decode` threw, and the `catch { return null; }` in `ParseBlock` swallowed it.

## 3. Root cause
`SignatureVerifier.ParseBlock` returns `null` — meaning "not a signature, ignore" — for **every** failure path:
malformed `/ByteRange` numbers, an unreadable gap, a `Convert.FromHexString` failure, **and** a `SignedCms.Decode`
exception. Only the last one implies "there is something here that claims to be a signature and it is broken".

The skip is invisible: no verdict, no reason, no entry in `Signatures[]`.

## 4. Fix
Separate *"not a signature"* from *"a broken signature"*:

- A block whose gap does not hold a well-formed hex string, or whose hex does not parse → genuinely not a
  signature → keep skipping.
- A block that yields DER bytes but where **`SignedCms.Decode` throws** → this is a corrupt/destroyed signature →
  emit `InvalidSignature` with a reason, so worst-wins picks it up.

Also report skipped blocks in the result (a count is enough) so "we ignored something that looked like a
signature" is never invisible to the caller.

## 5. What was changed (2026-07-20)
- `SignatureVerifier.ParseBlock` now returns `null` **only** for "not a signature" (unparsable/negative/insane
  `/ByteRange`, gap without a well-formed hex string, `Convert.FromHexString` failure). A block that yields DER but
  whose `SignedCms.Decode` throws — or that decodes with zero `SignerInfos` — comes back as a `SignatureBlock` with
  `Cms = null` and a `CorruptReason`; `Verify` turns it into an `InvalidSignature` result, which worst-wins picks up.
- `outermostEnd` is computed over decodable blocks only, so a corrupt block cannot loosen the EOF check.
- `TimestampVerifyDto` gained `SignatureBlockCount`, `SkippedBlocks`, `ReadFailed`.
- `SignService.ListPdfFiles` sets `AlreadySigned` from `ReadFailed || SignatureBlockCount > 0 || Signatures.Count > 0`
  instead of `Signatures.Count > 0` — a file with a destroyed signature, or one the verifier cannot read, no longer
  reports itself as unsigned (that was fail-open).

## 6. Verification (after the fix)
| Fixture | Before | After |
|---|---|---|
| `file/*.pdf` (13 real signed files) | `ValidTrusted` ×13 | `ValidTrusted` ×13, `SkippedBlocks=0` — unchanged |
| `filetestky/H49.59.24.2015.440.133.pdf` (iText original) | `TimeMismatch` (378 s) | identical — unchanged |
| `filetestky_đãký_.../H49.59.24.2015.440.133.pdf` (SIPPACK over-signed) | `TimeMismatch` (25443 s) | **`InvalidSignature`** — "Khối chữ ký trong file đã hỏng, không giải mã được (CMS lỗi: ASN1 corrupted data.)", `Signatures` now lists both blocks |

## 7. How to prevent
- **A "skip the noise" rule in a security check must be justified per failure mode, not per call site.** Collapsing
  four different failures into one `return null` was what made a destroyed signature indistinguishable from a
  false positive in a content stream.
- **When a verdict is milder than expected, treat it as a finding, not a relief.** `TimeMismatch` on a file we had
  just proven was structurally damaged was the clue; it would have been easy to read it as "only a clock issue".
- Every `catch { }` on a decode path in a verifier deserves the question: *what real-world event produces this
  exception, and is silence the right answer for it?*

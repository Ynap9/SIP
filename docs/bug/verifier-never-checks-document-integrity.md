# BUG: `SignatureVerifier` never checks document integrity — any content can be swapped under a genuine timestamp

> **Severity: CRITICAL — the `ValidTrusted` verdict is forgeable by copy-paste.** Runtime-confirmed 2026-07-15 on
> the real sample files + real G2 certs. Covers the "tamper" and the "transplant" variants (same root cause).
> Spec: [../verify-signature-timestamp.md](../verify-signature-timestamp.md).
> Fix plan: [../plans/verify-signature-hardening.plan.md](../plans/verify-signature-hardening.plan.md).

## 1. Summary
`SignatureVerifier.Verify` proves only that **a genuine Ban Cơ yếu TSA timestamped some signature value at time T**.
It never proves the signature matches **this document**, because the signed `/ByteRange` bytes are never read and
`SignedCms.CheckSignature` is never called (zero hits across the whole BE). Consequences:

- **Tamper**: edit any byte of a genuinely signed PDF, keep the CMS blob → still `ValidTrusted`.
- **Transplant**: copy a genuine `/ByteRange … /Contents <…>` block into an unrelated, unsigned file → `ValidTrusted`.

The verdict name promises document authenticity; the code delivers "a VGCA TSA existed".

## 2. Root cause
`SignatureVerifier.cs` decodes the CMS **detached with no content attached**:

```csharp
SignedCms cms;
try { cms = new SignedCms(); cms.Decode(der); } catch { continue; }   // <- no ContentInfo => content absent
var result = VerifySigner(cms.SignerInfos[0]);                        // <- only reads attrs + timestamp
```

`VerifySigner` then calls `token.VerifySignatureForSignerInfo(signer, out tsaCert)`, which binds the token to
`signerInfo`'s **signature value** — an opaque blob — not to any document bytes. So nothing in the path touches the
document. `ExtractContents` parses the `/ByteRange` array **only to locate the `/Contents` gap**: `Groups[4]` (`len2`)
is never used, and the signed ranges `[start1, start1+len1)` + `[start2, start2+len2)` are never extracted.

## 3. Evidence (runtime, real material)
PoC ran the **real** `SignatureVerifier` against `file/H49.64.33.2015.315.*.pdf` with the pinned G2 certs present.

Baseline — 5/5 `ValidTrusted`, signer `Ủy ban Nhân dân phường Hà Tu`, TSA `Máy chủ cấp dấu thời gian 02`.

**Tamper** (`H49.64.33.2015.315.1.pdf`, `/ByteRange [0 648109 681263 37658]`), flip one byte at offset 324054 —
in the middle of signed range 1, far from the signature dict — `0xED → 0x12`:

| Check | Original | Tampered |
|---|---|---|
| `SignatureVerifier.Verify` | `ValidTrusted` | **`ValidTrusted`** ← not detected |
| Correct check (`SignedCms(content, detached:true)` + `CheckSignature`) | `True` | `False` — `CryptographicException: The hash value is not correct.` |

The tamper is **fully detectable** with the BCL already referenced. The verifier simply does not look.

**Transplant**: a hand-built 12 KB file whose body is the literal text *"TÀI LIỆU BỊA ĐẶT — không ai ký"*, carrying
only the 5946-byte CMS blob copied out of `…315.1.pdf`, with `/ByteRange` offsets pointing at it →
`Verify` → **`ValidTrusted`**, `tsa=Máy chủ cấp dấu thời gian 02`, `trusted=16/04/2026 12:25:08`. No key, no token,
no TSA request — pure copy-paste.

## 4. Fix
Bind the CMS to the actual signed bytes and verify it, per signature block:

```csharp
// Noi dung duoc ky = dai 1 + dai 2 cua /ByteRange (thu hien tai KHONG BAO GIO doc).
var content = new byte[len1 + len2];
Buffer.BlockCopy(bytes, (int)start1, content, 0, (int)len1);
Buffer.BlockCopy(bytes, (int)start2, content, (int)len1, (int)len2);

var cms = new SignedCms(new ContentInfo(content), detached: true);
cms.Decode(der);
cms.CheckSignature(verifySignatureOnly: true);   // throws => tampered/transplanted => fail-closed
```

Also required so the ranges cannot lie: assert the `/ByteRange` **covers the whole file except the `/Contents` gap**
(`start1 == 0` && `start2 + len2 == fileLength` && the gap holds exactly this `/Contents`). Without that, an attacker
picks ranges that exclude the bytes they edited. Add a blocking verdict (e.g. `InvalidSignature`) distinct from
`NoTimestamp` so the FE can tell "unsigned" from "forged".

## 5. How to prevent
- **A timestamp binds to a signature value, not to a document.** `VerifySignatureForSignerInfo` answers "was this
  signature timestamped?", never "is this signature valid for this file?". The two checks are independent — always
  do both.
- **A detached `SignedCms` decoded without a `ContentInfo` can never detect tampering** — it has nothing to compare
  against, and it will not throw to tell you. `Decode` succeeding proves nothing about content.
- **Verify what a verdict name claims.** `ValidTrusted` implied authenticity while the code only checked TSA trust;
  name verdicts after what is actually proven, or prove what the name says.
- When adding a check to a security path, grep that the decisive API is really called (`CheckSignature` was absent
  from the entire BE) — reviewing the surrounding code is not the same as confirming the call exists.

# BUG: PAdES-LTV `/DocTimeStamp` is documented as supported but is not implemented

> **Severity: MEDIUM — false negative; a legitimately timestamped file is blocked from packing.**
> Confirmed by code reading 2026-07-15 (no LTV sample available to run against — see §3).
> Doc/code drift against [../verify-signature-timestamp.md](../verify-signature-timestamp.md) §4 step 3.
> Fix plan: [../plans/verify-signature-hardening.plan.md](../plans/verify-signature-hardening.plan.md).

## 1. Summary
The spec states the timestamp token is located in **either** of two places:

> 3. Locate the timestamp token:
>    - **PAdES-T**: unsigned attribute OID `1.2.840.113549.1.9.16.2.14` on the SignerInfo, or
>    - **PAdES-LTV**: a separate `/Type /DocTimeStamp` signature (`/SubFilter /ETSI.RFC3161`) whose `/Contents` is the token.

`SignatureVerifier` implements **only the first**. A PAdES-LTV file — where the signature carries no embedded
timestamp attribute and the trusted time lives in a separate document-timestamp signature — is rated `NoTimestamp`
and **blocked from packing**, despite carrying a genuine Ban Cơ yếu timestamp.

## 2. Root cause
`VerifySigner` looks in exactly one place:

```csharp
// token RFC 3161 trong unsigned attributes (PAdES-T)
foreach (var a in signer.UnsignedAttributes)
{
    if (a.Oid?.Value == SignatureConstants.OidSignatureTimeStampToken && a.Values.Count > 0)
    { tokenBytes = a.Values[0].RawData; break; }
}
if (tokenBytes == null && signingUtc == null) return null;   // -> khoi khac
...
if (tokenBytes == null) { dto.Verdict = NoTimestamp; ... }   // -> chan
```

There is no `/DocTimeStamp` handling anywhere: `grep -E "DocTimeStamp|ETSI\.RFC3161"` over `Sip.BE/**/*.cs` returns
**zero hits**. A `/DocTimeStamp` block, when the scan loop reaches it, has neither `signingTime` nor an embedded
timestamp attribute, so `VerifySigner` returns `null` and it is skipped — the token inside it is never read.

The spec was written ahead of the implementation and no test exercised the second branch, so the gap went unnoticed.

## 3. Evidence
- Code reading + grep as above; the second bullet of §4 step 3 has no corresponding code path.
- **Not runtime-reproduced**: all 5 sample files (`file/H49.64.33.2015.315.*.pdf`) are plain PAdES-T —
  `ByteRange=1`, `/SubFilter /ETSI.CAdES.detached`, `DocTimeStamp=0`. None exercises the LTV branch, which is
  precisely why the gap survived. Producing an LTV fixture requires a signer that applies a document timestamp.
- Impact is therefore **unquantified**: it depends on whether any real supplier ships LTV files. Worth confirming
  before ranking this against the other verifier bugs.

## 4. Fix
Handle the `/DocTimeStamp` case in the scan loop, keeping fail-closed behaviour:
- While scanning `/ByteRange` blocks, read the surrounding signature dict (as `PdfSignatureReader.ParseDictString`
  already does with a look-back window) to detect `/Type /DocTimeStamp` + `/SubFilter /ETSI.RFC3161`.
- For such a block, `/Contents` **is** the RFC 3161 token: `Rfc3161TimestampToken.TryDecode` it directly, then verify
  it binds the *document signature* it covers, chain the TSA to the pinned Root G2 at the raw `genTime` —
  the same trust rules as PAdES-T, only the token's location differs.
- Reuse the same verdict table; do not add an LTV-specific verdict.

Sequencing note: this interacts with
[verifier-first-signature-block-wins.md](verifier-first-signature-block-wins.md) — an LTV file has ≥2 blocks, so the
"verify every block, all must pass" fix must land **first**, or the `/DocTimeStamp` block stays unreachable. It also
interacts with [verifier-never-checks-document-integrity.md](verifier-never-checks-document-integrity.md): a
`/DocTimeStamp` covers a byte range too, and that range must be verified rather than trusted. Deferred in
[../plans/verify-signature-hardening.plan.md](../plans/verify-signature-hardening.plan.md) only because no LTV
fixture exists — not because the gap is closed.

## 5. How to prevent
- **A spec branch with no fixture is a branch with no implementation.** Each alternative in a documented "either/or"
  needs its own test file; otherwise the untested branch silently does not exist.
- When a doc lists N ways to find something, grep for N distinct markers in the code before believing it — here one
  grep (`DocTimeStamp`) would have exposed the gap immediately.
- Fail-closed hides false negatives: `NoTimestamp` looks like a normal, plausible outcome, so an unimplemented branch
  produces no error, no log, and no signal — only a rejected file that someone blames on the file.

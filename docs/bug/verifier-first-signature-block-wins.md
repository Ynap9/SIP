# BUG: `SignatureVerifier` returns on the first signature block — later blocks are never examined

> **Status: FIXED** — verified in code 2026-07-20: `Verify` collects every `/ByteRange` block, verifies every
> `SignerInfo` of every block, and the file verdict is the **worst** one (`Rank`), not the first.
> **Severity: HIGH — a forged signature hides behind a genuine one in a multi-signature PDF.**
> Runtime-confirmed 2026-07-15. Contradicts [../verify-signature-timestamp.md](../verify-signature-timestamp.md) §7
> ("Multi-signature PDFs: verify each signer independently"), which the code does not implement.
> Fix plan: [../plans/verify-signature-hardening.plan.md](../plans/verify-signature-hardening.plan.md).

## 1. Summary
`Verify` iterates the `/ByteRange` matches in **file byte order** and returns the verdict of the **first** block that
yields a result. Every later signature is ignored. So in a PDF with two signatures, the one that happens to sit
earlier in the file decides the verdict for the whole file — a genuine first signature makes a forged second
signature invisible.

This also **invalidates a claim in [../toolsign-integration.md](../toolsign-integration.md) §7**: ToolSign output
"can never pass the gate" is only true for a *freshly signed* PDF. Re-signing an **already-signed** PDF (the exact
case documented there: sig#1 = UBND Hà Tu with a timestamp, sig#2 = ToolSign without one) yields a file that SIPPACK
rates `ValidTrusted` on sig#1's merit.

## 2. Root cause
`SignatureVerifier.cs`:

```csharp
foreach (Match m in ByteRangeRegex.Matches(latin1))
{
    ...
    var result = VerifySigner(cms.SignerInfos[0]);   // chi signer dau tien cua khoi
    if (result != null) return result;               // <- ket thuc o KHOI DAU TIEN co ket qua
}
```

Two separate narrowings:
- `if (result != null) return result;` — first **block** with a verdict wins; later blocks unreachable.
- `cms.SignerInfos[0]` — first **signer** within a block; co-signers unreachable.

`VerifySigner` returns `null` only when a block has neither `signingTime` nor a timestamp, so any block carrying a
plain `signingTime` is enough to terminate the loop.

## 3. Evidence (runtime)
Two CMS blocks, both with correct `/ByteRange` offsets, in one hand-built file — **only the order differs**:
- *genuine* = the real 5946-byte CMS lifted from `file/H49.64.33.2015.315.1.pdf` (timestamped by the VGCA TSA).
- *forged* = self-signed `CN=Ke gia mao`, carries `signingTime`, **no** timestamp.

| File | Verdict | Reason / TSA |
|---|---|---|
| `[forged, genuine]` | `NoTimestamp` | `Chữ ký không có dấu thời gian tin cậy (RFC 3161).` |
| `[genuine, forged]` | **`ValidTrusted`** | `tsa=Máy chủ cấp dấu thời gian 02` |

The reason string on row 1 comes from `VerifySigner`, not from the end-of-method fallback — proving the first block
decided and the loop exited. Row 2 is the dangerous direction: **the forged block is never examined.**

> *Note on a discarded PoC:* an earlier attempt planted the decoy into a real PDF by **inserting** bytes. That shifted
> every offset and broke the real signature's `/ByteRange`, so the verdict changed for the wrong reason and proved
> nothing. Proving order-dependence requires both blocks to be independently well-formed — hence the two-block file.

## 4. Fix
**Domain rule: a file may carry any number of signatures; every one is verified and all must pass.** So verify each
block and each signer, then aggregate fail-closed — the **worst** verdict wins, never the first:

```csharp
var results = new List<TimestampVerifyDto>();
foreach (Match m in ByteRangeRegex.Matches(latin1))
{
    var der = ExtractContents(latin1, m);
    if (der == null) continue;
    SignedCms cms;
    try { cms = new SignedCms(); cms.Decode(der); } catch { continue; }  // khong phai chu ky -> bo qua
    foreach (SignerInfo si in cms.SignerInfos)        // moi signer, khong chi [0]
    {
        var r = VerifySigner(si);
        if (r != null) results.Add(r);
    }
}
// Fail-closed: bat ky chu ky nao khong dat => ca file khong dat.
return results.Count == 0 ? NoSignature() : results.OrderBy(RankWorstFirst).First();
```

Ranking, worst → best: `InvalidSignature` > `UntrustedSigner` > `UntrustedTsa` > `NoTimestamp` > `TimeMismatch` >
`ValidTrusted`. Skip blocks that fail to decode (a literal `/ByteRange` inside a content stream is not a signature)
rather than failing the file. Report per-signature detail (signer + verdict), not one verdict per file — with N
signatures a single badge is not diagnosable.
Plan: [../plans/verify-signature-hardening.plan.md](../plans/verify-signature-hardening.plan.md) step 2.

## 5. How to prevent
- **A file-level verdict aggregated from multiple signatures must be fail-closed** — take the worst, not the first.
  "First match wins" is a search idiom; in a security check it is an attacker-chosen selector, because the attacker
  controls byte order.
- When a doc says "verify each X independently", that is a **testable claim** — a two-item fixture where the items
  disagree catches this instantly. A single-signature fixture (all 5 samples here) can never catch it.
- Beware `[0]` on a collection in a verification path (`SignerInfos[0]`): it silently assumes "there is only one".

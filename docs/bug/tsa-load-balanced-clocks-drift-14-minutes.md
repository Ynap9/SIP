# BUG: the Ban Cơ yếu TSA runs two clocks ~14 min apart — a distance-based time check cannot stand on it

> **Status: FIXED on our side 2026-07-21** — the verifier no longer measures the *distance* between `signingTime`
> and `genTime`; it only checks their **order**. The TSA defect itself is upstream and out of our control.
> **Severity: HIGH** — it rejected genuine files, and the failure rate was growing month over month.
> Related: [genTime-offset-compensates-on-the-wrong-side.md](genTime-offset-compensates-on-the-wrong-side.md).

## 1. Summary
`SignatureVerifier` accepted a signature only when `|signingTime − genTime| ≤ 300 s`. That treats the TSA clock as an
accurate reference. It is not: the TSA sits behind a load balancer fronting (at least) **two backend servers whose
clocks differ by ~14 minutes**, and **both run fast**. The 300 s threshold therefore rejected genuine documents
depending on which backend happened to answer — pure coin-flip.

The threshold had "worked for months" only because the drift used to be smaller than it. It was not a safety margin;
it was a countdown.

## 2. Evidence — direct TSA calls
Four requests in ~10 s, network round-trip 0.0 s:

| # | request sent (UTC) | `genTime` returned | offset |
|---|---|---|---|
| 1 | 02:07:34 | 02:25:42 | **+1087 s** |
| 2 | 02:07:38 | 02:11:35 | **+237 s** |
| 3 | 02:07:41 | 02:25:48 | **+1087 s** |
| 4 | 02:07:44 | 02:11:41 | **+237 s** |

Perfect alternation ⇒ round-robin across two backends. The local clock was cross-checked against Google, Cloudflare
and the TSA's own web server: within 1 s. **Our clock is right; the TSA's is not.**

### The drift is widening
Residuals measured on real signatures over time:

| Date | backend A | backend B |
|---|---|---|
| 15/04 | ~0 s | ~128 s |
| 03/06 | ~180 s | ~378 s |
| 21/07 | ~237 s | ~1087 s |

## 3. Why a distance check was the wrong instrument
- **Regulation.** QCVN 138:2025/BKHCN § 2.2 (in force 01/07/2026) normatively references **ETSI EN 319 421 V1.2.1**,
  which requires declared accuracy of **1 second or better** and that the clock **shall not drift outside** it. The
  observed 237–1087 s exceeds that by two to three orders of magnitude.
- **The official tool does not do this check.** VGCA's own `vSignPDF 3.1.8` reports `NotSigned`, `NotTimestamped`,
  `InvalidTimestamp`, `InvalidTimestampImprint`, `InvalidSigningCertificate`, `InvalidTSACertificate`,
  `NonCoversWholeDocument`, `NonCheckingRevoked*` — **no time-skew state at all**, and no `TimeSpan` constant in the
  assembly. `SigningTime`/`UtcSigningTime` exist only to be displayed.
- **Files in the wild already violate it.** ToolSign ships documents with 128 s and 378 s residuals and treats them
  as valid.

## 4. Fix — check order, not distance
A valid signing sequence is always *compute signature → send to TSA → TSA stamps*, so `signingTime` must precede
`genTime`. Claiming a signing time **after** the stamp is what a backdating/forgery attempt looks like, and it is the
one property a fast TSA clock cannot break — a fast clock only widens the gap in the safe direction.

```csharp
if (delta > SignatureConstants.TsaClockSkewGraceSeconds) // delta = signingTime − genTime, quirk-normalised
{
    dto.Verdict = TimestampVerdict.TimeMismatch;
}
```

Two details that matter:
- **Normalise the 7h local-time quirk first.** Legacy files carry `signingTime` 7h *ahead*; applying the order rule
  before subtracting it fails every one of them.
- **`>` with a small grace, never `>=`.** `signingTime` is second-precision, so a TSA answering within the same
  second is normal and `==` must pass. The grace guards a *slightly slow* TSA clock — it is not an anti-forgery
  margin and must stay small.

`TimestampVerdict` gains no new member; only the semantics and the `Reason` text change.

## 5. How to prevent
- **A check whose reference input comes from a non-conformant system must not have blocking power.** The TSA clock
  was never fit to measure distance against; the only sound question to ask it was a relative one (ordering).
- **A threshold that "has worked for months" may be masking a defect that is still growing.** Track the *residual*,
  not just the pass/fail — a margin shrinking over releases is a leading indicator. Here the residual grew from
  ~0 s to ~1087 s while the check kept reporting success.
- **Probe an external service repeatedly, not once.** A single call would have shown a plausible ~237 s offset and
  hidden the second clock entirely. The alternation only appears across consecutive calls.
- **Check what the ecosystem's reference implementation actually enforces** before enforcing something stricter.
  Being stricter than the official validator means rejecting documents the issuing authority considers valid.

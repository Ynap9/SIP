# BUG: the +7h correction was applied to the TSA's `genTime` — compensating on the wrong side

> **Status: FIXED** — 2026-07-21. `trustedUtc` is now the **raw** `genTime`; the 7h offset is only used to *recognise*
> a signer-side mislabelling when comparing `signingTime`.
> **Severity: HIGH** — it silently rejected every correctly-produced signature, including SIPPACK's own.
> Spec: [../verify-signature-timestamp.md](../verify-signature-timestamp.md).

## 1. Summary
`SignatureVerifier` computed `trustedUtc = genTime + 7h`, on the belief that the Ban Cơ yếu TSA stores `genTime` 7h
behind true UTC. That belief was wrong. `genTime` is correct UTC; the 7h discrepancy lives in the **signer's**
`signingTime`, which some signing tools write as **local time while tagging it `Kind=Utc`**.

The correction therefore fixed a defect on the side that did not have it, and broke the side that was fine:

- Any signature whose `signingTime` is genuine UTC — **including every signature SIPPACK itself produces** — was
  pushed 7h away from `trustedUtc` and came out `TimeMismatch`.
- `trustedUtc` is also the instant every certificate chain is built at, so the whole chain evaluation ran **7h in the
  future** of the real stamping moment.

Two wrongs cancelled on the legacy corpus, which is exactly why it went unnoticed.

## 2. Root cause
```csharp
// truoc khi sua
var trustedUtc = token.TokenInfo.Timestamp.UtcDateTime.AddHours(SignatureConstants.VgcaGenTimeOffsetHours);
var delta = Math.Abs((signingUtc.Value - trustedUtc).TotalSeconds);
if (delta > SignatureConstants.ToleranceSeconds) { /* TimeMismatch */ }
```

The diagnosis was drawn from a corpus of 13 sample files **all produced by the same family of signing tools**. Within
that corpus `signingTime − genTime ≈ +25200 s` uniformly. Two hypotheses explain that equally well:

| Hypothesis | Fix it implies |
|---|---|
| TSA stores `genTime` 7h behind UTC | `genTime + 7h` |
| Signer writes local time into `signingTime` and tags it UTC | compare `signingTime` against `genTime + 7h` |

The corpus **cannot distinguish them** — it contains no signature from a tool that gets `signingTime` right. The first
hypothesis was picked, and it happens to produce identical results on that corpus.

## 3. Evidence — two independent sources
**(a) A signature produced by SIPPACK** (real USB token, `signingTime` written as true `DateTime.UtcNow`):

| | `signingTime` (UTC) | `genTime` (raw UTC) | delta vs raw | delta vs +7h |
|---|---|---|---|---|
| SIPPACK-signed | 2026-07-20 17:55:16 | 2026-07-20 17:59:09 | **−233 s** | −25433 s |
| legacy tool A | 2026-04-16 05:25:06 | 2026-04-15 22:25:08 | +25198 s | −2 s |
| legacy tool B | 2026-06-03 14:15:20 | 2026-06-03 07:21:38 | +24822 s | −378 s |

233 s is exactly PIN entry + signing + the TSA round-trip. Same TSA, same policy OID `2.16.704.1.1.1.2` in all rows —
so the TSA cannot be both correct and 7h off at once. It is correct.

**(b) A production file contradicting itself.** In `file/H49.64.33.2015.315.1.pdf` the *visible appearance block*
records `2026-04-16T05:25:06.2686779+07:00` (ISO 8601 **with** offset ⇒ 22:25:06 UTC), while the CMS `signingTime`
carries the *same digits* tagged `Kind=Utc`. The TSA stamped 22:25:08 UTC — 2 s after the appearance. The appearance
is right; the CMS field is wrong. The file disagrees with itself, and `genTime` is the tie-breaker.

**Standards note.** PKCS#9 `signingTime` (RFC 5652) is ASN.1 `UTCTime` — absolute UTC, no local-time option. So these
files are genuinely **non-conformant**; SIPPACK accepts them as **backward compatibility**, not as a second standard.
TT05 governs package metadata (`YYYY-MM-DDThh:mm:ss.sTZD`), not fields inside the CMS blob.

## 4. Fix
`trustedUtc` = raw `genTime`. The comparison gets an explicit second branch instead of a silent correction:

| `signingTime − genTime` | Verdict |
|---|---|
| `\|delta\| ≤ 300 s` | `ValidTrusted` |
| `\|delta − 7h\| ≤ 300 s` | `ValidTrusted` + `Reason` naming the mislabelling |
| otherwise | `TimeMismatch` |

The quirk branch **states itself in `Reason`** rather than passing silently — a tool writing bad timestamps stays
visible. `signingTime` is self-asserted and not covered by the TSA signature, so it is only ever a cross-check;
`genTime` remains the sole trusted instant.

## 5. How to prevent
- **Compensate on the side that has the defect.** Before adding an offset, ask which of the two values is
  independently verifiable. Here one is cryptographically signed by a third party (`genTime`) and the other is
  self-asserted (`signingTime`) — that asymmetry alone points at the culprit.
- **A corpus produced by one family of tools cannot localise a discrepancy between two parties.** It shows the
  *difference*, never which side moved. Getting a known-good artifact — generating one yourself is the cheapest way —
  is what breaks the tie. This bug survived until SIPPACK could sign for itself.
- **Beware a fudge factor that also feeds something else.** `trustedUtc` was both the comparison operand and the chain
  verification instant; "tuning" it for the comparison silently moved every chain build 7h.
- `DateTime.Kind` is metadata a producer can set wrongly. When a value is tagged `Utc`, that is a *claim*, not proof.

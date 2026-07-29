# Bug — file-level signing time came from the OLDEST signature

> Found 2026-07-28 while signing files that already carried a signature. Fixed the same day.

## Symptom
Sign a PDF that **already has a signature**. SIPPACK adds its own (incremental update, so the file now holds two).
The sign step then shows **"Time ký"** and **"Dấu thời gian"** of the *pre-existing* signature — not the one just
created. Same in the pack-check screens.

## Root cause
`SignatureVerifier.Verify` returns **one** `TimestampVerifyDto`: the element picked by

```csharp
var worst = results.OrderByDescending(r => Rank(r.Verdict)).First();
```

That object carries *both* the verdict *and* the times. When every signature is `ValidTrusted` the ranks tie, and
`OrderByDescending` is **stable**, so `First()` returns the element that came first in `results` — which is filled
in `/ByteRange` match order, i.e. the **oldest** signature. `SignService` then copies `verify.SigningTime` /
`verify.TrustedTime` into `SignFileResultDto`, so the UI faithfully displays the wrong signature's clock.

The defect is **conflating two questions in one object**:

| Question | Correct answer |
|---|---|
| Is the file acceptable? | the **worst** signature (one bad signature spoils the file — deliberate, fail-closed) |
| When was it signed? | the **newest** signature (the operation that just happened) |

## Fix
1. Keep each block's `SignedEnd` alongside its result. Incremental signing only ever **appends**, so the block
   covering the most bytes is the last one applied — that is the only reliable "newest" ordering.
2. Sort the per-signature results by `SignedEnd` ascending, so `Signatures` reads as signature #1, #2, … newest last.
3. Verdict/reason still come from the worst signature; `SigningTime` / `TrustedTime` are **overwritten from the
   newest** one.
4. Report per signature instead of relying on one file-level pair: `SignatureDetailDto` gained `SigningTime`,
   `TimestampVerifyDto` gained `ValidSignatureCount`, `SignFileResultDto` gained `SignatureCount` /
   `ValidSignatureCount` / `Signatures`. Both the sign table and the pack-check tables now show
   "n/m chữ ký hợp lệ" plus each signature's own two times.

## How to prevent
- A file-level field that silently belongs to *one* of several signatures is a bug waiting to happen. When a file
  can hold N of something, the API should return the N items and only aggregate with a **stated rule** (worst
  verdict, newest time) — never "whichever element the sort happened to leave first".
- `OrderBy`/`OrderByDescending` in .NET are **stable**. Ties therefore resolve to input order, which here is byte
  order in the file — attacker-influenced and semantically meaningless. Any tie that matters needs its own key.
- Related: [verifier-first-signature-block-wins.md](verifier-first-signature-block-wins.md) fixed the same class of
  mistake for the *verdict*; this one shows the times were still carrying it.

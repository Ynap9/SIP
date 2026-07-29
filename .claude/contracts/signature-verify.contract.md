
# Contract — signature verification result (`TimestampVerifyDto`)

> Additive change, 2026-07-20. `TimestampVerifyDto` gained three fields. **No field was renamed or removed**, so
> existing FE code keeps working; the new fields are optional to consume.

## Where the FE receives it
`TimestampVerifyDto` is nested as `timestamp` inside `SignatureInfoDto`, which the **check** flow streams over SSE
(`pages/dong-goi/check`).

It is **also** returned directly by a standalone route (added 2026-07-21, runtime-verified):

| Method | Route | Request | Response |
|---|---|---|---|
| POST | `api/core/document/sign/kiem-tra-chu-ky` | `SignatureCheckRequestDto` | `ApiResponse.data` = `TimestampVerifyDto` |

```json
{ "filePath": "C:\\path\\to\\file.pdf" }
```

Checks **one file**, no hồ sơ folder structure required — use it to inspect a file just signed. A folder path is
rejected. A non-PDF is **not** rejected: it comes back `verdict = 4 (NotApplicable)`, same as in the check flow.
Missing file ⇒ `status = 0`, `ErrorCodes.SignSourceNotFound`.

## New fields (camelCase, as received)
```json
{
  "verdict": 6,
  "signatureBlockCount": 2,
  "skippedBlocks": 0,
  "readFailed": false
}
```

| Field | Type | Meaning |
|---|---|---|
| `signatureBlockCount` | `number` | `/ByteRange` blocks read as a signature, **including broken ones**. Use this — not `signatures.length` — to answer "does this file have a signature?". |
| `skippedBlocks` | `number` | `/ByteRange` matches that were **not** signatures (the literal string inside a content stream). Normally `0`; a non-zero value is worth surfacing as a note, not an error. |
| `readFailed` | `boolean` | The verifier could not read the file at all. The verdict is fail-closed and untrustworthy; never conclude "unsigned" from an empty `signatures` array when this is `true`. |

## Unchanged
`verdict`, `hasTimestamp`, `timestampAuthority`, `signedBy`, `trustedTime`, `signingTime`, `reason`, `signatures[]`,
`isValid`, `blocks` keep their names, types and semantics.

## Enums (serialised as NUMBERS)
`TimestampVerdict`: 0 ValidTrusted · 1 NoTimestamp · 2 UntrustedTsa · 3 TimeMismatch · 4 NotApplicable ·
5 UntrustedSigner · 6 InvalidSignature

## Behaviour changes the FE may notice
No field changed name or type in either change below — only which verdict a given file produces.

1. A PDF whose earlier signature was destroyed now reports `verdict = 6 (InvalidSignature)` instead of the milder
   `3 (TimeMismatch)`, and its `signatures[]` includes the broken block (`signedBy` empty, `reason` explains the CMS
   decode failure). See [../../docs/bug/verifier-skips-undecodable-signature-hiding-destruction.md](../../docs/bug/verifier-skips-undecodable-signature-hiding-destruction.md).
2. **2026-07-21 — `3 (TimeMismatch)` changed meaning and became much rarer.** It no longer fires on a *distance*
   between `signingTime` and `genTime` (the TSA clock is too unreliable to measure against); it fires only when
   `signingTime` falls **after** the TSA stamp, which is an impossible signing order. Files that previously came
   back `3` now come back `0 (ValidTrusted)`.
   `reason` may now be populated **on a passing verdict** to note that the signer wrote local time into
   `signingTime` — so **do not treat a non-empty `reason` as failure**; branch on `verdict` / `isValid` only.
   See [../../docs/bug/tsa-load-balanced-clocks-drift-14-minutes.md](../../docs/bug/tsa-load-balanced-clocks-drift-14-minutes.md).

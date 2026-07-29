# Bug — displayed signing time ignored the 7h quirk correction

> Found 2026-07-29 on `243/243.*.pdf` (files pre-signed by another tool, then re-signed by SIPPACK). Fixed same day.

## Symptom
A file with two signatures showed, in the sign table:

| | Time ký | Dấu thời gian (TSA) |
|---|---|---|
| #1 (other tool) | 27/07/2026 **18:43:06** | 27/07/2026 11:47:16 |
| #2 (SIPPACK) | 29/07/2026 00:56:56 | 29/07/2026 01:01:08 |

Signature #1 reads as if it was signed **7 hours after** its own timestamp was issued — impossible, and it made
SIPPACK's own signature (#2, correct) look like the odd one out.

## Evidence from the file
`243.1.pdf` carries `/M (D:20260727114306)` — **11:43:06, no timezone offset**. Its CMS `signingTime` holds the
same 11:43:06 but tagged as UTC, while the TSA `genTime` is 04:47:16 UTC. So the signer really signed at
**11:43:06 Vietnam time** (04:43 UTC) and the TSA stamped it 4 minutes later — the tool just wrote local time into
a field that ASN.1 defines as absolute UTC.

## Root cause
`SignatureVerifier.VerifySigner` already knows about this: it detects the ~7h gap
(`SignerLocalTimeQuirkHours`) and subtracts it before the ordering check, so the verdict is right. But
`dto.SigningTime` is formatted **earlier in the method**, straight from the raw attribute, and never revisited:

1. raw `signingTime` = 11:43:06, read as UTC
2. display converts UTC → Vietnam (+7) ⇒ **18:43:06**
3. the quirk correction runs afterwards and only fixes `delta`

The value was already Vietnam time; converting it again added the offset twice.

## Fix
Inside the `isLocalTimeQuirk` branch, re-format the display string from the corrected instant
(`signingUtc − 7h`), so it prints exactly the wall clock the signer wrote (11:43:06) and lands before its own
TSA time.

## How to prevent
- When a value is **normalised for a decision**, every derived output must come from the normalised value.
  Formatting a display string before the normalisation runs leaves the UI telling a different story from the
  verdict — here the verdict said "valid, backwards-compatible quirk" while the table said "signed after it was
  timestamped".
- Related: [verifier-file-time-comes-from-oldest-signature.md](verifier-file-time-comes-from-oldest-signature.md)
  and `genTime-offset-compensates-on-the-wrong-side.md` — the same 7h keeps causing bugs because it is a
  **signer-side** mislabelling, never a TSA one. Compensate on the signer side only, and only for display/ordering.

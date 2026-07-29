# Verify digital-signature trusted timestamp (Ban Cơ yếu Chính phủ TSA)

> How SIPPACK proves a PDF's signing time is genuine — bound to an RFC 3161 trusted timestamp issued by the
> Ban Cơ yếu Chính phủ TSA — instead of a self-claimed clock value.
> **Implemented** (BE in `Sip.be.External`, FE "Dấu thời gian" column; runtime-verified 13/13 `ValidTrusted`). Covers
> **document integrity**, the **timestamp** and the **signer's chain**; every signature is verified, all must pass.
> Vietnamese companion: [verify-chu-ky-so-dau-thoi-gian.md](verify-chu-ky-so-dau-thoi-gian.md).

## 1. Problem
`signingTime` (PKCS#9 `1.2.840.113549.1.9.5`) is the signer's **self-claimed** clock, forgeable by changing the PC
time — and it sits in the same file as the token, so **comparing the two numbers proves nothing**: a forger sets
them equal. Trustworthy time comes only from an RFC 3161 token whose **TSA signature chains to a pinned root**.

## 2. Trust anchors (pin these — the feature hinges on them)
A timestamp token embeds its full TSA chain, so verification is **fully offline** once the roots are pinned.
**Two branches, two jobs:** Root G2 anchors the **TSA** chain (and G2 signers); Root G1 + Sub-CA G1 anchor the
**signer** chain — production signer certs are G1, the TSA is always G2. G1/G2 is the PKI *generation*, not
agency-vs-individual. The shipped set, pins, download paths and provenance live in
**[ca-trust-certificates.md](ca-trust-certificates.md)** — canonical, do not duplicate here. The Windows cert store
is **not** a source of trust (gov root not installed on the test machine).

## 3. Input
A PDF path, or a hồ sơ `attachment/` folder → verify every `.pdf` (non-PDF → `N/A`); the pinned CA set (G1 + G2)
shipped beside the app; `TsaClockSkewGraceSeconds` (60) — how far `signingTime` may exceed `genTime`.

## 4. Steps (per PDF, all offline)
> **Every** signature in the file is verified and **all** must pass; the file verdict is the **worst** one. Blocks
> that do not decode to a `SignedCms` are skipped — `/ByteRange` inside a content stream is not a signature.

1. Scan `/ByteRange` + `/Contents` for each signature (reuse `PdfSignatureReader` logic).
2. **Document integrity.** Concatenate the two signed ranges `[start1,start1+len1) + [start2,start2+len2)`, build
   `new SignedCms(new ContentInfo(content), detached: true)`, `Decode(der)`, then per signer
   `CheckSignature(verifySignatureOnly: true)` → throws ⇒ `InvalidSignature`. Then assert `/ByteRange` cannot lie:
   `start1 == 0`, the gap holds exactly this `/Contents`, and the **outermost** signature (largest `start2+len2`)
   reaches EOF. Read `signingTime` (claimed) if present.
3. Locate the timestamp token — **PAdES-T only**: unsigned attribute OID `1.2.840.113549.1.9.16.2.14` on the
   SignerInfo. **PAdES-LTV is NOT implemented**: a separate `/Type /DocTimeStamp` signature
   (`/SubFilter /ETSI.RFC3161`) carrying the token is never read, so such a file is wrongly rated `NoTimestamp` and
   blocked. Open bug: [bug/verifier-ltv-doctimestamp-not-implemented.md](bug/verifier-ltv-doctimestamp-not-implemented.md).
4. `Rfc3161TimestampToken.TryDecode(tokenBytes, out token, out _)`.
5. `token.VerifySignatureForSignerInfo(signerInfo, out tsaCert)` — verifies the TSA signature, that the token's
   `messageImprint` binds **this** signature, and TSA EKU `id-kp-timeStamping` (`1.3.6.1.5.5.7.3.8`).
6. Build `X509Chain` for `tsaCert` with **custom trust anchor = pinned Root G2 only** (token certs in ExtraStore,
   revocation offline), at `VerificationTime = trustedTime` — **not** `DateTime.Now`; require the chain root's
   SHA-256 == pinned Root G2. An expired TSA cert does not invalidate an old timestamp — valid *at stamping* is what
   counts, and `Now` would reject every file at once the day the TSA cert expires (§7).
7. `trustedTime = token.TokenInfo.Timestamp` (genTime) — **raw UTC for every check** (§7); only the display string is Vietnam time (via `DateTimeConstants.VietnamTimeZone`, never `ToLocalTime()`).
8. **Signer-chain gate.** Build `X509Chain` for the signer cert with `VerificationTime = trustedTime` — **not**
   `DateTime.Now`. Accept when **either** branch builds to its pinned root: G1 (anchor Root G1, `cp.crt` in
   `ExtraStore`) **or** G2 (anchor Root G2). Not G1 ⇒ try G2; neither ⇒ `UntrustedSigner`. G1 is tried first
   (production is G1). Anchoring at the trusted time is mandatory: the real signer cert expires **2026-09-25**, and
   `Now` would reject every genuine historical file from that day on.
9. `delta = signingTimeUtc − trustedTime`; if `|delta − 7h| ≤ 1h`, subtract 7h (signer local-time mislabelling, §7 —
   reported in `Reason`). Then check **order only**: `delta > TsaClockSkewGraceSeconds` ⇒ `TimeMismatch`, else pass.

## 5. Verdict
| Verdict | Condition | Meaning |
|---|---|---|
| `VALID_TRUSTED` | token present, steps 5–6 pass, time within tolerance | genuine Ban Cơ yếu timestamp |
| `InvalidSignature` | step 2 fails — signature ≠ content, `/ByteRange` lies, or bytes outside every signed range | document modified after signing → **fake** |
| `NO_TIMESTAMP` | no RFC 3161 token | time unprovable → **suspect/fake** (policy) |
| `UNTRUSTED_TSA` | token present but step 5 or 6 fails | forged / foreign TSA → **fake** |
| `UntrustedSigner` | step 8 fails — signer cert chains to neither Root G1 nor Root G2 | not vouched for by Ban Cơ yếu → **fake** |
| `TIME_MISMATCH` | steps 5–6 pass but `signingTime` is **after** `genTime` | impossible signing order → **suspect** |
| `NotApplicable` | non-PDF file | signature check under development → **warn** (contact admin), does not block; pack on confirm |

No `signingTime` ⇒ `trustedTime` is authoritative. `Signatures[]` carries every signature (`SignedBy`, `Verdict`,
`Reason`, `TrustedTime`, `SigningTime`) oldest → newest, plus `ValidSignatureCount`. **Verdict and clock come from
different signatures**: file `Verdict`/`Reason` from the **worst**, file `TrustedTime`/`SigningTime` from the
**newest** (largest `SignedEnd` — incremental signing only appends). Taking both from one object made a re-signed
file report the *old* clock: [bug/…-oldest-signature.md](bug/verifier-file-time-comes-from-oldest-signature.md).

## 6. Output
`TimestampVerifyDto` per file (`Verdict`, `HasTimestamp`, `TimestampAuthority`, `SignedBy`, `TrustedTime`,
`SigningTime`, `Reason`, `Signatures[]`, plus computed `IsValid`/`Blocks`); surfaced as the FE "Dấu thời gian"
column. `Blocks` is a whitelist (`!= ValidTrusted && != NotApplicable`), so a new blocking verdict needs no service
change. **Policy (decided):** a non-`VALID_TRUSTED` **PDF** blocks packing (`ErrorCodes.TimestampInvalid`, or
`SignatureInvalid` for a modified document); a non-PDF is `NotApplicable` → warn ("…liên hệ admin"), packing still
allowed on user confirm.

## 7. Decisions & pitfalls
- **Mandatory:** keep steps 5–6 (TSA signature + chain). Dropping them makes "match ⇒ genuine" unsound.
- **What `ValidTrusted` means.** Every signature is unmodified since signing (step 2), by a certificate Ban Cơ yếu
  vouched for (step 8), at a time the Ban Cơ yếu TSA proves (steps 5–7), with no byte left unsigned.
- **Certs are judged at `trustedTime`, never `Now`** (TSA *and* signer; they die 2034 and 2026-09-25 — `Now` turns
  each date into the day every historical file is rejected at once). Measured: `VerificationTime` is honoured and
  `Kind=Utc` read as UTC — at `NotBefore`, `+1h` builds and `−1h` does not.
- **Never require *every* signature to reach EOF (measured).** Adding a signature *is* an incremental update, so an
  earlier one legitimately ends at its own revision while `CheckSignature` passes; a blanket rule rejects every
  genuine multi-signature file. Requiring it of the **outermost** signature still blocks an appended revision and
  covers every byte.
- **Worst verdict wins, ranked explicitly** (`InvalidSignature` > `UntrustedSigner` > `UntrustedTsa` >
  `NoTimestamp` > `TimeMismatch` > `ValidTrusted`). "First block wins" is attacker-controlled — they choose byte
  order; measured: swapping two blocks used to flip the file verdict.
- **A chain must `Build()`, not merely contain the pinned CA.** `ChainElements` is the *attempted* path, including
  elements whose signature failed; ignoring `Build`'s result accepts forgeries — see
  [bug/signer-chain-pinned-ca-presence-is-not-enough.md](bug/signer-chain-pinned-ca-presence-is-not-enough.md).
- **The 7h offset belongs to the SIGNER, not the TSA (measured).** `genTime` is correct UTC; some tools write **local
  time** into PKCS#9 `signingTime` while tagging it `Kind=Utc`. **Never add it to `genTime`** — wrong side, and it
  drags every chain-build instant with it. Such files break RFC 5652, so accepting them is backward compatibility,
  reported in `Reason`. [bug/genTime-offset-compensates-on-the-wrong-side.md](bug/genTime-offset-compensates-on-the-wrong-side.md)
- **Order, never distance (measured).** The TSA load-balances two backends whose clocks differ by **~14 min**, both
  fast, drift widening (≈0/128 s in April → 237/1087 s in July) — *any* distance threshold rejects genuine files at
  random. QCVN 138:2025/BKHCN § 2.2 → ETSI EN 319 421 demands 1 s accuracy; VGCA's own `vSignPDF` enforces **no**
  time-skew check. Only `signingTime ≤ genTime` is sound — a fast TSA clock widens the gap the safe way.
  [bug/tsa-load-balanced-clocks-drift-14-minutes.md](bug/tsa-load-balanced-clocks-drift-14-minutes.md)
- Zero-dep: `System.Security.Cryptography.Pkcs`, offline, no OCSP/CRL. Related:
  `Sip.be.External/Documents/{PdfSignatureReader,SignatureVerifier}.cs`, [ca-trust-certificates.md](ca-trust-certificates.md).

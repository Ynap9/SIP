# Verify digital-signature trusted timestamp (Ban Cơ yếu Chính phủ TSA)

> How SIPPACK proves a PDF's signing time is genuine — bound to an RFC 3161 trusted timestamp issued by the
> Ban Cơ yếu Chính phủ TSA — instead of a self-claimed clock value.
> **Implemented** (BE in `Sip.be.External`, FE "Dấu thời gian" column; runtime-verified: 13/13 real files `ValidTrusted`).
> Vietnamese companion: [verify-chu-ky-so-dau-thoi-gian.md](verify-chu-ky-so-dau-thoi-gian.md).

## 1. Problem
`PdfSignatureReader` today reads only `signingTime` (PKCS#9 `1.2.840.113549.1.9.5`) — the signer's
**self-claimed** clock, forgeable by changing the PC time. Genuine time must come from an **RFC 3161 timestamp
token** cryptographically signed by the TSA. Both `signingTime` and the token live inside the same file, so
**comparing the two numbers alone proves nothing** — a forger sets them equal. Only the token's **TSA signature +
chain to a pinned root** makes the time trustworthy.

## 2. Trust anchor (pin this — the feature hinges on it)
A timestamp token embeds its full TSA chain, so verification is **fully offline** once the root is pinned.

| Role | Subject | SHA-256 |
|---|---|---|
| Root (anchor, pin) | `RootCA chuyên dùng Chính phủ G2` (self-signed, 2018→2048) | `F04BA4B459A7C9A1B971AD9B1CB833E695A80C79AEA63B174B66A70EDFE2FDB8` |
| TSA (example) | `Máy chủ cấp dấu thời gian 02, Ban Cơ yếu CP` | `EAE46D68…CE954BD6` |

- Get the pinned root from the CA's own AIA URL **`http://ca.gov.vn/pki/pub/crt/rootcag2.crt`** (HTTP, not the flaky
  HTTPS site). **Independently verified:** this official download and the copy embedded in real files agree on
  `F04BA4B4…` — rules out circular trust. Sub-CA G1 (signer issuer): `http://ca.gov.vn/pki/pub/cert/cp.crt`
  (`75E969C0…`); revocation: `http://ca.gov.vn/pki/pub/crl/rootcag2.crl`, `http://ocsp.ca.gov.vn`.
- The Windows cert store is **not** a reliable source here (gov root not installed on the test machine).
- Old signer certs chain to Sub-CA `Co quan chung thuc so Chinh phu` (G1 era). For **timestamp** verification only
  Root G2 is needed (the TSA chains to G2); the signer's own chain is only needed to verify the *signature* itself.

## 3. Input
- A PDF path, or a hồ sơ `attachment/` folder → verify every `.pdf` (non-PDF → `N/A`).
- The pinned CA set (Root G2) shipped in the app.
- `toleranceSeconds` (default 300) — max allowed gap between trusted time and claimed time.

## 4. Steps (per PDF, all offline)
1. Scan `/ByteRange` + `/Contents` for each signature (reuse `PdfSignatureReader` logic).
2. `SignedCms.Decode(contents)` → `SignerInfo`; read `signingTime` (claimed local time) if present.
3. Locate the timestamp token:
   - **PAdES-T**: unsigned attribute OID `1.2.840.113549.1.9.16.2.14` on the SignerInfo, or
   - **PAdES-LTV**: a separate `/Type /DocTimeStamp` signature (`/SubFilter /ETSI.RFC3161`) whose `/Contents` is the token.
4. `Rfc3161TimestampToken.TryDecode(tokenBytes, out token, out _)`.
5. `token.VerifySignatureForSignerInfo(signerInfo, out tsaCert)` — verifies the TSA signature, that the token's
   `messageImprint` binds **this** signature, and TSA EKU `id-kp-timeStamping` (`1.3.6.1.5.5.7.3.8`).
6. Build `X509Chain` for `tsaCert` with **custom trust anchor = pinned Root G2 only** (token certs in ExtraStore,
   revocation offline). Require the chain root's SHA-256 == pinned Root G2.
7. `trustedTime = token.TokenInfo.Timestamp` (genTime, UTC); check it falls within the signer cert validity.
8. Compare with the **VGCA +7h correction** (see §7): `|signingTimeUtc − (genTime + 7h)| ≤ toleranceSeconds`.

## 5. Verdict
| Verdict | Condition | Meaning |
|---|---|---|
| `VALID_TRUSTED` | token present, steps 5–6 pass, time within tolerance | genuine Ban Cơ yếu timestamp |
| `NO_TIMESTAMP` | no RFC 3161 token | time unprovable → **suspect/fake** (policy) |
| `UNTRUSTED_TSA` | token present but step 5 or 6 fails | forged / foreign TSA → **fake** |
| `TIME_MISMATCH` | steps 5–6 pass but gap > tolerance | claimed date inconsistent → **suspect** |
| `NotApplicable` | non-PDF file | signature check under development → **warn** (contact admin), does not block; pack on confirm |

If `signingTime` is absent, `trustedTime` is authoritative → `VALID_TRUSTED` when steps 5–6 pass.

## 6. Expected output
Extend `SignatureInfoDto` (or a new `TimestampVerifyDto`) per file:
```csharp
bool IsSigned; string? Signer; string? SigningTimeLocal;                    // claimed
bool HasTrustedTimestamp; string? TimestampAuthority; string? TrustedTime;  // genTime
bool ChainToRootG2; bool TimeMatch; string Verdict; string? Reason;
```
Aggregate per hồ sơ; surface a "Dấu thời gian" column on the FE check table.
**Policy (decided):** a non-`VALID_TRUSTED` **PDF** blocks packing (`ErrorCodes.TimestampInvalid`); a non-PDF is
`NotApplicable` → warn ("…liên hệ admin"), packing still allowed on user confirm.

## 7. Decisions & pitfalls
- **Mandatory:** keep steps 5–6 (TSA signature + chain). Dropping them makes "match ⇒ genuine" unsound.
- **VGCA +7h quirk (measured, load-bearing):** in real VGCA tokens `genTime` is stored **7h behind true UTC**
  (UTC+7 mislabeled `Z`). Across 13 sample files `signingTime − genTime ≈ 25200 s − delay`; after `genTime + 7h`
  the residual is the TSA round-trip **delay of 2 s–131 s** (timestamp just after signing). A naive tight-tolerance
  **UTC compare rejects every genuine file** — always apply +7h; treat TSA crypto-validity + Root-G2 chain as the
  primary trust signal (the time delta only guards gross backdating).
- **Tech:** native `System.Security.Cryptography.Pkcs` (`.NET 8`, `Rfc3161TimestampToken` available) — zero-dep,
  matching `PdfSignatureReader`. Offline only; OCSP/CRL revocation is optional/out of scope.
- **Multi-signature** PDFs: verify each signer independently.
- Related: `Sip.be.Application/Document/Implements/PdfSignatureReader.cs`, [chuan-dong-goi-sip-tt05.md](chuan-dong-goi-sip-tt05.md).

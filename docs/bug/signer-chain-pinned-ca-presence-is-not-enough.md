# BUG (pre-empted): "pinned CA appears in the chain" does not prove the CA issued the cert

> **Severity: CRITICAL if shipped — it would accept forged signer certificates.** Caught in a PoC while designing the
> signer-trust gate, before any code was written. Read before implementing signer-chain validation.
> Plan: [../plans/verify-signature-hardening.plan.md](../plans/verify-signature-hardening.plan.md) step 5.

## 1. Summary
The signer-trust gate must prove *"this signer certificate was issued by the pinned Ban Cơ yếu Sub-CA"*. The obvious
implementation — build an `X509Chain` with the pinned CA in `ExtraStore`, then check the pinned CA appears in
`chain.ChainElements` — is **unsound**. A certificate that merely **claims** the pinned CA as its issuer, while being
signed with the attacker's own key, still puts the pinned CA into `ChainElements`. The check passes; the certificate
is a forgery.

The accept condition must be **both**: `chain.Build(...) == true` **and** the pinned CA is the issuer element.
`Build`'s return value is what actually enforces the signature link; `ChainElements` alone only reflects the path
the chain *attempted* to construct.

## 2. Root cause
`X509Chain` populates `ChainElements` with the path it built **including elements whose signature failed to
verify**; the failure surfaces in `ChainStatus` (`NotSignatureValid`) and in `Build` returning `false`, not by
omitting the element. Reading `ChainElements` without consulting `Build` therefore reads an *unvalidated* structure.

This is easy to get wrong here specifically because the design deliberately sets
`VerificationFlags = AllowUnknownCertificateAuthority` (to work without a Root G1 anchor, see §4). Having already
decided to ignore one chain error, it is tempting to treat `Build`'s result as noise and rely on `ChainElements` —
which is exactly the mistake. `AllowUnknownCertificateAuthority` suppresses only the **trust** error; signature
validity is still enforced and still shows up in `Build`.

## 3. Evidence (runtime, synthetic PKI)
Root → Sub-CA → leaf, plus two forgeries, all pinning the Sub-CA and checking `ChainElements[1]`:

| Certificate | `Build` | `len` | pinned CA at `[1]` | Naive check | Correct check (`Build && pinned`) |
|---|---|---|---|---|---|
| Real leaf, issued by Sub-CA | `true` | 2 | `true` | accept ✅ | accept ✅ |
| Self-signed, subject copied verbatim | `true` | 1 | `false` | reject ✅ | reject ✅ |
| **Claims `Issuer=Sub-CA`, signed with attacker key** | **`false`** | **2** | **`true`** | **accept ❌** | reject ✅ |

Row 3 is the trap: `ChainElements` contains the pinned Sub-CA, so a `pinnedPresent`-only check accepts a certificate
whose signature was never made by that CA.

Related, measured in the same PoC: subject fields prove nothing on their own — a self-signed cert reproducing the
real organisational subject (`CN=Ủy ban Nhân dân phường Hà Tu`, `O=Tỉnh Quảng Ninh`, `OU=Thành phố Hạ Long`, and the
`MST` tax code) is byte-identical in every displayed field. Any rule reading `CN`/`O`/`OU`/`MST` — "does this person
belong to this agency?" — is worthless until a pinned CA vouches for those fields.

## 4. Fix
```csharp
using var chain = new X509Chain();
chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;      // trust store de TRONG
chain.ChainPolicy.ExtraStore.Add(pinnedSubCaG1);
chain.ChainPolicy.VerificationTime = trustedUtc;                       // KHONG phai DateTime.Now
// Chi bo qua loi TRUST (khong co anchor); chu ky doc duong VAN duoc chain kiem.
chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

var built = chain.Build(signerCert);
var issuedByPinned = chain.ChainElements.Count >= 2 &&
    string.Equals(chain.ChainElements[1].Certificate.Thumbprint, pinnedSubCaG1.Thumbprint,
                  StringComparison.OrdinalIgnoreCase);

// CA HAI dieu kien — thieu `built` la nhan cert gia (§3 hang 3).
return built && issuedByPinned;
```

## 5. How to prevent
- **`ChainElements` is the attempted path, not a verdict.** Never read it without also honouring `Build`'s result.
  The same applies to `ChainStatus`: it explains a failure, it does not define success.
- **When you suppress a chain error, name exactly which one and why.** `AllowUnknownCertificateAuthority` suppresses
  the missing-anchor error only. Reaching for a broader flag (or ignoring `Build`) to "make it work" silently
  disables the signature check — the one thing the gate exists to do.
- **Test a forgery that gets the structure right and the crypto wrong.** A self-signed impostor is the easy case and
  every naive check rejects it; the cert that *claims* the right issuer is the one that separates a real check from
  a decorative one. A gate tested only against the easy forgery is untested.
- Certificate **subject fields are attacker-supplied text**. Identity means something only when a pinned CA has
  signed the binding — organisation-membership rules cannot substitute for chain validation, they depend on it.

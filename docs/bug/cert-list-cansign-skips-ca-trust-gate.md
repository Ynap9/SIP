# BUG: certificate list reports `canSign` without applying the CA trust gate

> **Severity: HIGH — the picker promises a certificate can sign, then the sign step rejects it.**
> **Status: FIXED 2026-07-29.**
> Affects the **Sign** flow step 1 (`pages/ky-so/cert-step`) and `CertificateProvider`.
> A must-read for any agent touching certificate enumeration or the pre-flight checks.

---

## 1. Summary

`SignCertDto.CanSign` was defined as `HasPrivateKey && !IsExpired && AllowsSigning`. The pre-flight run before
an actual signing batch (`StoreSigningKey`) applies **one more check** the list did not: the certificate must
chain to a pinned Ban Cơ yếu root (G1 **or** G2). Any certificate outside that PKI — a self-signed test
certificate, a corporate certificate, anything with a usable private key — was therefore listed as
**"Ký được"**. Selecting it produced a failure only later, and the user had no way to tell in advance which
certificate was actually usable.

---

## 2. Root cause

The same rule was expressed in two places and only one of them was complete:

| Where | Checks applied |
|---|---|
| `SignCertDto.CanSign` (list) | private key · not expired · KeyUsage |
| `StoreSigningKey.Verify` (pre-flight) | private key · not expired · KeyUsage · **test-sign** · **`IsTrustedSignerCert`** |

`SignatureVerifier.IsTrustedSignerCert` is `internal` and lives in the same assembly as `CertificateProvider`,
so the list had access to it all along — it simply never called it. The test-sign step legitimately belongs to
pre-flight only (it touches the private key and raises the token PIN prompt), but the chain check does not:
it is an offline chain build with `RevocationMode.NoCheck` and never touches the key.

The consequence is worse than a bad label. Signing with an untrusted certificate **succeeds**, and the
resulting files are then flagged `UntrustedSigner` by the packaging gate — so the cost of the wrong label is a
whole batch signed with a certificate that can never be packaged.

---

## 3. The fix

1. `SignCertDto` — new `IsTrusted` flag, folded into the verdict:
   ```csharp
   public bool IsTrusted { get; set; }
   public bool CanSign => HasPrivateKey && !IsExpired && AllowsSigning && IsTrusted;
   ```
2. `CertificateProvider.GetCertificates` — populate it with the *same* predicate the sign step uses, evaluated
   at `DateTime.UtcNow` to match how `SignatureVerifier` builds chains:
   ```csharp
   IsTrusted = SignatureVerifier.IsTrustedSignerCert(cert, DateTime.UtcNow),
   ```
   and extend the `Reason` chain with the matching explanation, so the "Ghi chú" column tells the user the
   certificate does not belong to Ban Cơ yếu instead of leaving the row unexplained.
3. `SignService.GetCertificates` — log `trusted=` alongside the other per-certificate flags, and widen the
   `CertificateNotSignable` message to name the new cause.
4. `Sip.FE/src/app/models/sign.models.ts` — add `isTrusted` to `ISignCert` to keep the DTO in sync.

No FE logic change was required: `cert-step` already derives the status tag, the selection guard and the
"pick the only signable certificate" shortcut from `canSign` + `reason`, so correcting the flag on the backend
corrected every one of them at once.

---

## 4. How to prevent

- **A list that advertises an outcome must run the same predicate that produces the outcome.** If a screen
  says "ký được", the check behind that word has to be the check the sign step performs — not a subset of it.
- When a rule exists in two layers, keep the shared half in one callable place. Here `IsTrustedSignerCert` was
  already factored out for exactly this reason (see `docs/bug/signer-chain-pinned-ca-presence-is-not-enough.md`);
  the bug was not missing infrastructure, it was a caller that never used it.
- Only the checks that **touch the private key** (the 32-byte test-sign, and therefore the PIN prompt) may be
  deferred to pre-flight. Everything offline — validity window, KeyUsage, chain building — belongs in the list
  as well, because it costs nothing and it is what the user is choosing on.
- The CA trust gate is **fail-closed**: if the pinned `.crt` files are missing from `certs/`, every certificate
  now correctly reports "không ký được". `build-desktop.ps1` step 5 already verifies all five are present.

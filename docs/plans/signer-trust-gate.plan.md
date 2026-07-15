# Plan — Gate signer-certificate trust (Ban Cơ yếu G1 chain)

> Prove **who signed**, not just that the document is intact. Companion to
> [verify-signature-hardening.plan.md](verify-signature-hardening.plan.md), which proves the document but leaves
> identity unchecked: a self-signed certificate carrying a genuinely obtained TSA timestamp passes every step there.
> The two plans are two legs of one stool — the gate is not sound until both land.
> Pitfall to read first: [../bug/signer-chain-pinned-ca-presence-is-not-enough.md](../bug/signer-chain-pinned-ca-presence-is-not-enough.md).

## Requirements / Input
- **Depends on** the hardening plan's steps 1–2 (verdict plumbing + per-signature loop). Every signature in a file
  must pass this gate, same fail-closed aggregation.
- **✅ Material present + authenticated.** `Sip.be.Api/cp.crt` (Sub-CA G1, PEM, gitignored) downloaded 2026-07-15
  from `http://ca.gov.vn/pki/pub/cert/cp.crt` — the AIA `caIssuers` URL inside the real signer certs.
  `CN=Co quan chung thuc so Chinh phu, O=Ban Co yeu Chinh phu, C=VN`, valid `2010-03-25 → 2030-03-04`, SHA-256
  `75E969C07BA35C29C72BBC1BFB880F20F3891A619B00B77EB02CA6160736A1FE` — matches the pin in
  [../verify-signature-timestamp.md](../verify-signature-timestamp.md) §2.
  - *Non-circular check (measured):* matching a pin the docs took from the same download would prove only "same file
    as before". Stronger evidence was used instead — **`cp.crt` cryptographically verifies as the issuer of all 5
    real signer certs** (`build=true`, pinned CA at `ChainElements[1]`, 5/5). Only the genuine Sub-CA's private key
    could have produced those signatures. The G2 docs broke circularity by comparing against a copy embedded in real
    files; that is unavailable here because the CMS embeds **only the leaf**.
- **Root G1 is NOT required** (see step 2's Decision). For the record — no doc previously named it — the signer's
  Authority Key Identifier gives Root G1 as
  `CN=Co quan chung thuc so chuyen dung Chinh phu (RootCA), O=Ban Co yeu Chinh phu, C=VN`, Sub-CA serial `04`;
  confirmed by `cp.crt`'s own issuer field.
- **⏰ Time-critical (measured).** The fixture signer cert is valid `2016-09-27 → 2026-09-25`. Simulating one check
  run on 2026-12-01 against the real cert: `VerificationTime = Now` ⇒ `build=false` (**rejects a genuine file**);
  `VerificationTime = trustedTime` (16/04/2026) ⇒ `build=true`. Today both still pass — the cliff is invisible until
  it fires, then fires for **every** historical file at once. Same class as
  [../bug/verifier-chain-verification-time-defaults-to-now.md](../bug/verifier-chain-verification-time-defaults-to-now.md),
  here with a known date. `VerificationTime = trustedUtc` is mandatory, not an optimisation.
- **Production tokens are G1**, so this anchor set is the one that matters operationally — not an edge case.
- Zero-dep constraint stands: `System.Security.Cryptography.Pkcs` only.

## Steps
1. **Pin Sub-CA G1.** Add `SubCaG1FileName = "cp.crt"` + its SHA-256 to `SignatureConstants`, reusing
   `LoadPinnedCert` (PEM **or** DER, pin by cert DER fingerprint, fail-closed on mismatch — see
   [../bug/ca-trust-cert-pem-vs-der-loader.md](../bug/ca-trust-cert-pem-vs-der-loader.md)). Add `cp.crt` to
   `Sip.be.Api.csproj` `<Content>` and to `Sip.BE/.gitignore`. `dotnet build`.
   - *Decision:* filename must match the constant exactly — `LoadPinnedCert` does `File.Exists(name)` and the csproj
     copies by literal name, so material arriving as `CP_G1.cer` must be renamed or the gate silently fails closed.
2. **Verify the signer chain by manual pinning.** Add `TimestampVerdict.UntrustedSigner` (blocking). Per signature:
   ```csharp
   using var chain = new X509Chain();
   chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
   chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;   // trust store de TRONG
   chain.ChainPolicy.ExtraStore.Add(pinnedSubCaG1);
   chain.ChainPolicy.VerificationTime = trustedUtc;                    // KHONG phai DateTime.Now
   chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
   var built = chain.Build(signerCert);
   var issuedByPinned = chain.ChainElements.Count >= 2 && string.Equals(
       chain.ChainElements[1].Certificate.Thumbprint, pinnedSubCaG1.Thumbprint, StringComparison.OrdinalIgnoreCase);
   accept = built && issuedByPinned;      // CA HAI — thieu `built` la nhan cert gia
   ```
   `dotnet build`.
   - *Decision — manual pinning, not a Root G1 anchor (measured):* .NET's `CustomRootTrust` **refuses a
     non-self-signed anchor** — anchoring at Sub-CA G1 always yields `PartialChain` (control: anchoring at a
     self-signed root with the Sub-CA in `ExtraStore` returns `true`). Manual pinning gives the same guarantee
     without Root G1, whose SHA-256 is published nowhere we can verify against — pinning an unverifiable anchor
     would be worse than not pinning one.
   - *Decision — `AllowUnknownCertificateAuthority` suppresses only the **trust** error.* Signature validity along
     the path is still enforced and still surfaces in `built`. Do not widen the flag set.
   - *Decision — both accept conditions are mandatory.* A cert that merely *claims* the pinned CA as issuer still
     appears in `ChainElements`; only `built` catches the bad signature. Verified against three forgeries:
     [../bug/signer-chain-pinned-ca-presence-is-not-enough.md](../bug/signer-chain-pinned-ca-presence-is-not-enough.md).
3. **FE surfacing.** Add `UntrustedSigner` to `models/check.models.ts` and to the per-signature rows introduced by
   the hardening plan (badge + signer + reason). Wording must distinguish "chữ ký không tin cậy" from "không có dấu
   thời gian" — they are different failures with different remedies. `npm run build`.
4. **Regression fixtures.** (a) 5/5 real files still `ValidTrusted` — the gate must not reject genuine signers;
   (b) self-signed cert reproducing the real subject verbatim ⇒ `UntrustedSigner`; (c) cert claiming
   `Issuer=Sub-CA G1` but signed with another key ⇒ `UntrustedSigner`; (d) signer cert evaluated at `trustedTime`,
   not `DateTime.Now` — guards the 2026-09-25 cliff; (e) `cp.crt` missing or pin mismatch ⇒ fail-closed, not
   accept-all. `dotnet build`.
5. **Update the docs.** Extend [../ca-trust-certificates-g2.md](../ca-trust-certificates-g2.md) with the G1 set
   (it currently documents only G2 — retitle or add a G1 section) and
   [../verify-signature-timestamp.md](../verify-signature-timestamp.md) §2/§4/§5 with the signer-chain gate and the
   G1/G2 anchor split. Per `CLAUDE.md`, in this same task.

## Expected output
- The 5 real files stay `ValidTrusted`, now and after 2026-09-25.
- A self-signed cert — including one reproducing `CN=Ủy ban Nhân dân phường Hà Tu`, `O`, `OU` and the `MST` tax code
  verbatim — is rejected `UntrustedSigner`, as is a cert falsely claiming the pinned Sub-CA as issuer.
- Together with the hardening plan, `ValidTrusted` finally means: unmodified since signing, **signed by a
  certificate the Ban Cơ yếu vouched for**, at a time proven by the Ban Cơ yếu TSA.

## Out of scope
- **`organName` cross-check.** Comparing the signer's `O`/`CN` against the document's EAD_doc `organName` catches a
  validly-signed document from the **wrong agency** — a business rule worth having, but *not* a security control and
  meaningless before this gate: subject fields are attacker-controlled text until a CA vouches for them (measured —
  a self-signed forgery reproduced every field identically). Propose separately once this lands.
- **Revocation (OCSP/CRL)** — `http://ocsp.ca.gov.vn/`, `http://ca.gov.vn/pki/pub/crl/cp.crl` exist in the certs'
  extensions, but the verifier is offline-by-design; unchanged here.

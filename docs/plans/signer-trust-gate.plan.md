# Plan — Gate signer-certificate trust (Ban Cơ yếu G1 + G2 chains)

> **STATUS (2026-07-17): steps 1–3 + 5 implemented and runtime-verified; step 4 is outstanding and the main gap** —
> the gate is proven to run and to fail closed, not yet proven to reject a cert that *claims* the pinned CA. Two
> changes vs the plan below: Root G1 was obtained so the G1 branch anchors normally (see Requirements), and the gate
> accepts **G1 ∨ G2**, not G1 alone. Shipped design: [../ca-trust-certificates.md](../ca-trust-certificates.md).
>
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
- **✅ Root G1 obtained + authenticated (2026-07-17) — this OVERTURNS step 2's "no Root G1 anchor" decision.**
  `Sip.be.Api/rootca.crt` (PEM, gitignored) from `http://ca.gov.vn/pki/pub/cert/rootca.crt` — found via the CRL URL
  in `cp.crt` (`.../crl/rootca.crl`), as `cp.crt` carries no `caIssuers`. `CN=Co quan chung thuc so chuyen dung
  Chinh phu (RootCA)`, self-signed `sha1RSA`, valid `2010-03-10 → 2030-03-05`, SHA-256 `F7BC8905…3F90`.
  - *Non-circular check (measured):* serial `00FF303AE4E90AC1ED` + SKI `D9171B51…9D5C` match the AKI `cp.crt` states
    for its issuer, and anchoring at it makes `cp.crt` validate (`build=true`, `NoError`, 2 elements). Control:
    anchoring at Root G2 ⇒ `PartialChain`. The plan's premise that Root G1's pin "is published nowhere we can verify
    against" was wrong — it is verifiable *against `cp.crt` itself*, which real signer certs already authenticate.
- **⏰ Time-critical (measured).** The fixture signer cert is valid `2016-09-27 → 2026-09-25`. Simulating one check
  run on 2026-12-01 against the real cert: `VerificationTime = Now` ⇒ `build=false` (**rejects a genuine file**);
  `VerificationTime = trustedTime` (16/04/2026) ⇒ `build=true`. Today both still pass — the cliff is invisible until
  it fires, then fires for **every** historical file at once. Same class as
  [../bug/verifier-chain-verification-time-defaults-to-now.md](../bug/verifier-chain-verification-time-defaults-to-now.md),
  here with a known date. `VerificationTime = trustedUtc` is mandatory, not an optimisation.
- **Production tokens are G1**, so this anchor set is the one that matters operationally — not an edge case.
- Zero-dep constraint stands: `System.Security.Cryptography.Pkcs` only.

## Steps
1. ✅ **Pin the G1 branch.** `RootG1FileName`/`RootG1Sha256` + `SubCaG1FileName`/`SubCaG1Sha256` in
   `SignatureConstants`, reusing `LoadPinnedCert` (PEM **or** DER, pin by cert DER fingerprint, fail-closed on
   mismatch — see [../bug/ca-trust-cert-pem-vs-der-loader.md](../bug/ca-trust-cert-pem-vs-der-loader.md)). Add
   `rootca.crt` + `cp.crt` to `Sip.be.Api.csproj` `<Content>` and to `Sip.BE/.gitignore`. `dotnet build`.
   - *Decision:* filename must match the constant exactly — `LoadPinnedCert` does `File.Exists(name)` and the csproj
     copies by literal name, so material arriving as `CP_G1.cer` must be renamed or the gate silently fails closed.
2. ✅ **Verify the signer chain — G1 ∨ G2.** Add `TimestampVerdict.UntrustedSigner` (blocking; `Blocks` is a
   whitelist so it blocks with no service change). Per signature, try each branch until one builds to its pinned
   self-signed root via the existing `ChainsToRoot`, with `VerificationTime = trustedUtc`:
   ```csharp
   // G1 truoc (token production la G1): anchor Root G1, cp.crt vao ExtraStore vi CMS chi nhung leaf.
   if (rootG1 != null && subCaG1 != null && ChainsToRoot(signerCert, cmsCerts + subCaG1, rootG1, trustedUtc))
       return true;
   // G2: cpg2/dcscag2 da nam san trong ExtraStore cua ChainsToRoot.
   return ChainsToRoot(signerCert, cmsCerts, rootG2, trustedUtc);
   ```
   `dotnet build`.
   - *Decision — accept G1 **or** G2, never require both.* G1/G2 is the PKI **generation**, not agency-vs-individual;
     both hierarchies issue legitimate signer certs. Not G1 ⇒ try G2; only when neither builds is it `UntrustedSigner`.
   - *Decision — supersedes the original "manual pinning, not a Root G1 anchor".* That decision rested on Root G1
     being unobtainable/unverifiable. It is neither (see Requirements), so the branch now uses a plain
     `CustomRootTrust` anchor exactly like G2 — which drops the hand-rolled `ChainElements[1]` comparison **and** the
     `AllowUnknownCertificateAuthority` flag. `ChainsToRoot` already enforces `Build()` **and** a root-thumbprint
     check, so the §3 forgery (a cert *claiming* the pinned CA) is still rejected by construction.
   - *Decision — `Build()` is what enforces the signature link.* A cert that merely *claims* the pinned CA as issuer
     still appears in `ChainElements`; only `Build`'s result catches the bad signature. Verified against three
     forgeries: [../bug/signer-chain-pinned-ca-presence-is-not-enough.md](../bug/signer-chain-pinned-ca-presence-is-not-enough.md).
3. ✅ **FE surfacing.** Add `UntrustedSigner = 5` to `models/check.models.ts` and a badge case in `check.html` +
   `dong-goi/multi`. Wording must distinguish "chữ ký không tin cậy" from "không có dấu thời gian" — different
   failures, different remedies. Shipped: "Người ký không tin cậy" vs "TSA không tin cậy" vs "Không có".
   `npm run build`.
4. ⏳ **Regression fixtures — OUTSTANDING, the main gap.** (a) ✅ 13/13 real files still `ValidTrusted`;
   (b) self-signed cert reproducing the real subject verbatim ⇒ `UntrustedSigner`; (c) cert claiming
   `Issuer=Sub-CA G1` but signed with another key ⇒ `UntrustedSigner`; (d) signer cert evaluated at `trustedTime`,
   not `DateTime.Now` — guards the 2026-09-25 cliff; (e) ✅ `rootca.crt`/`cp.crt` missing or pin mismatch ⇒
   fail-closed (measured: removing either ⇒ 13/13 `UntrustedSigner`); (f) a **G2**-signed PDF ⇒ `ValidTrusted` —
   the G2 branch runs today but has never returned `true` for lack of a fixture. `dotnet build`.
   - *Decision:* (b) and (c) need a synthetic PKI; without them the gate is proven to *run*, not to *reject*.
5. ✅ **Update the docs.** [../ca-trust-certificates.md](../ca-trust-certificates.md) (renamed from
   `-g2`, now covers both generations + pin provenance) and
   [../verify-signature-timestamp.md](../verify-signature-timestamp.md) §2/§4/§5/§7. Per `CLAUDE.md`, same task.

## Expected output
- The real files stay `ValidTrusted`, now and after 2026-09-25. ✅ (13/13 measured)
- A self-signed cert — including one reproducing `CN=Ủy ban Nhân dân phường Hà Tu`, `O`, `OU` and the `MST` tax code
  verbatim — is rejected `UntrustedSigner`, as is a cert falsely claiming the pinned Sub-CA as issuer. ⏳ (step 4)
- Together with the hardening plan, `ValidTrusted` finally means: unmodified since signing, **signed by a
  certificate the Ban Cơ yếu vouched for**, at a time proven by the Ban Cơ yếu TSA. ⏳ (hardening plan not started)

## Out of scope
- **`organName` cross-check.** Comparing the signer's `O`/`CN` against the document's EAD_doc `organName` catches a
  validly-signed document from the **wrong agency** — a business rule worth having, but *not* a security control and
  meaningless before this gate: subject fields are attacker-controlled text until a CA vouches for them (measured —
  a self-signed forgery reproduced every field identically). Propose separately once this lands.
- **Revocation (OCSP/CRL)** — `http://ocsp.ca.gov.vn/`, `http://ca.gov.vn/pki/pub/crl/cp.crl` exist in the certs'
  extensions, but the verifier is offline-by-design; unchanged here.

# PDF signing — readiness assessment

> Can SIPPACK sign PDFs to the standard process — **trusted timestamp**, **tamper-evidence**, and an
> **authentic, unforgeable signer identity**? Assessed 2026-07-20 against the code actually in the tree.
> Plan being assessed: [plans/pdf-signing.plan.md](plans/pdf-signing.plan.md).

## 1. Verdict

**Not yet — but the hard half is done, and it is the half that is usually done badly.**

Everything needed to *judge* a signature exists and is hardened. Nothing that *produces* one exists yet. The
remaining work is three components (§3), and one **unvalidated assumption** (§4) that can invalidate the design.

## 2. What is ready

| Capability | State | Evidence |
|---|---|---|
| **Tamper-evidence** (verify) | ✅ | `SignatureVerifier` binds the CMS to the real `/ByteRange` bytes, `CheckSignature(verifySignatureOnly:true)`, asserts `start1==0`, gap holds only `/Contents`, outermost signature reaches EOF |
| **Trusted timestamp** (verify) | ✅ | `VerifySignatureForSignerInfo` (binds token to *this* signature + TSA EKU) → chain to pinned Root G2 at `trustedUtc` |
| **Anti-forgery signer identity** (verify) | ✅ | `IsTrustedSigner`: chain must `Build()` to pinned Root **G1 ∨ G2**; `ChainElements` alone is never trusted |
| **Multi-signature safety** | ✅ | every block × every `SignerInfo` verified, **worst verdict wins** (`Rank`) |
| **Time correctness** | ✅ | every chain built at the raw `genTime`, never `DateTime.Now` |
| **Trust material** | ✅ | 5 pinned CAs, provenance non-circular (real PDFs → signer → `cp.crt` → `rootca.crt`) |
| **Certificate enumeration** | ✅ | `CertificateProvider` reads `CurrentUser\My` + `LocalMachine\My`, classifies USB token by CSP/KSP provider, reports `canSign` + `reason` |
| **TSA reachability** | ✅ | `tsa.ca.gov.vn` answers TSP (GET 403 / POST 400) |

This matters more than it looks: the verifier is the **acceptance oracle** for the signer. We can prove whether our
own output is genuine, using the same rules that block a forgery.

## 3. What is missing

| # | Component | Risk | Note |
|---|---|---|---|
| 1 | `TimestampClient` (RFC 3161 over HTTP) | **Low** | `Rfc3161TimestampRequest.CreateFromSignerInfo` + `HttpClient`; entirely BCL |
| 2 | `CmsPdfSigner` (detached CAdES) | **Medium** | `signingCertificateV2` must be hand-built with `AsnWriter` — .NET does not add it |
| 3 | `PdfIncrementalSigner` (incremental update writer) | **High** | hand-rolled sig dict + padded `/ByteRange` slots + xref + trailer `/Prev`, then patch `/Contents` in place |
| 4 | `SignService` + SSE + FE page | Low | mirrors the existing Check flow |
| 5 | Visible signature appearance | High | deferred to plan step 8, correctly staged last |

**#3 is the crux.** A byte-offset mistake produces a file that *looks* signed and fails verification silently. It
must be developed against the verifier from the first commit, not at the end.

## 4. The blocking unknown — token access via CNG

The plan assumes the USB token can be driven through **Windows CNG** (`RSACng`), because the middleware bridges it
into the cert store. **This has never been measured.** `CertificateProvider` deliberately reads key *metadata* only
and never touches the private key, precisely so it does not raise a PIN prompt — so nothing in SIPPACK has ever
asked this token to actually sign.

Contrary evidence: **ToolSign did not use CNG.** It drove the token through **PKCS#11** (`bit4xpki.dll`) with
BouncyCastle. That is not proof CNG fails, but it is the one working reference we have, and it took a different path.

**If CNG cannot sign with this token, plan steps 4–5 change shape** (PKCS#11 + BouncyCastle instead of BCL CMS) —
i.e. the assumption sits *underneath* the most expensive component. Spike it first: load the token cert, call
`RSACng.SignData` on 32 bytes, confirm the PIN prompt appears and the signature verifies. Half a day; it either
confirms the architecture or saves rewriting the writer.

## 5. Gaps that do not block v1, but must be stated

- **No regression fixtures.** [plans/verify-signature-hardening.plan.md](plans/verify-signature-hardening.plan.md)
  step 6 and [plans/signer-trust-gate.plan.md](plans/signer-trust-gate.plan.md) step 4 are unfinished — missing a
  real **2-signature** file and a **self-signed impostor** cert. The oracle in §2 is manually validated only.
  Building a signer without them means a regression in the verifier would be invisible.
- **"Passes our verifier" is a weaker bar than "is standards-conformant."** Our verifier does not check
  `signingCertificateV2`, so a CAdES-non-conformant file could still pass it. Acceptance must include **Adobe
  Reader / Foxit**, not only SIPPACK.
- **PAdES-T only, not LT/LTA.** `/DocTimeStamp` is unimplemented on the verify side
  ([bug/verifier-ltv-doctimestamp-not-implemented.md](bug/verifier-ltv-doctimestamp-not-implemented.md)) and no DSS
  (OCSP/CRL) is embedded when signing. PAdES-T proves *when* it was signed; **LTA** is what keeps it verifiable
  decades later without the CA online — arguably the point of an archival product. Acceptable for v1, but it is a
  deliberate deferral, not a solved problem.
- **Revocation is never checked** (`RevocationMode.NoCheck`, offline by design). A revoked-but-unexpired signer cert
  still passes. Fine for an offline desktop tool; state it, do not discover it later.

## 6. Recommended order

1. **Spike CNG token signing** (§4) — gate on the result before anything else.
2. Commit the missing verifier fixtures (§5) — the oracle must be trustworthy first.
3. Plan steps 1→3→4→5, verifying each output with `ISignatureVerifier` from the first file.
4. Validate in Adobe Reader before calling it done.

# Ban Cơ yếu Chính phủ trust certificates

> The five pinned CA certificates SIPPACK ships to validate digital signatures / trusted timestamps
> offline, and how they are wired into `SignatureVerifier`.
> Companion: [verify-signature-timestamp.md](verify-signature-timestamp.md).

## 1. Two generations — G1 and G2

**G1/G2 is the PKI *generation*, not the audience.** Both generations serve *cơ quan*, and each issues certificates
to organisations **and** to the individuals inside them — G2 says so in the CA's own name (*"phục vụ các cơ quan Nhà
nước / các cơ quan Đảng"*). The audience split lives in the **branch** (`cpg2` = State agencies, `dcscag2` = Party
agencies) and in the certificate **subject** (CN = an organisation vs a person's name), never in G1 vs G2.

| Generation | Validity | Root (anchor) | Sub-CA | Anchors the chain of |
|---|---|---|---|---|
| **G1** | 2010 → 2030 | `rootca.crt` | `cp.crt` | **signer** certs (production tokens are G1) |
| **G2** | 2018 → 2048 | `rootcag2.crt` | `cpg2.crt`, `dcscag2.crt` | **TSA** timestamp certs; also accepted for signers |

Measured: all 13 real fixture PDFs carry a **G1** signer (`CN=Ủy ban Nhân dân phường Hà Tu`, an organisation, issued
by `cp.crt`), while the USB token cert from the spike is **G2** (`CN=Phạm Ngọc Chính`, an individual, issued by
`cpg2`). Two samples that happen to differ on both axes — which is exactly why the generation is easy to mistake for
the audience.

## 2. The certificates

All are issued by **Ban Cơ yếu Chính phủ**. They live beside the app in `Sip.be.Api/` (gitignored, supplied
out-of-band from `ca.gov.vn`) and are copied to the build output so `SignatureVerifier` can load them.

| File | Subject (CN) | Role | Issuer | SHA-256 pin (DER) |
|---|---|---|---|---|
| `rootca.crt` | Co quan chung thuc so chuyen dung Chinh phu (RootCA) | **Root G1** (anchor, self-signed) | itself | `F7BC8905…3F90` |
| `cp.crt` | Co quan chung thuc so Chinh phu | **Sub-CA G1** (issues signer certs) | Root G1 | `75E969C0…A1FE` |
| `rootcag2.crt` | RootCA chuyên dùng Chính phủ G2 | **Root G2** (anchor, self-signed) | itself | `F04BA4B4…FDB8` |
| `dcscag2.crt` | CA phục vụ các cơ quan Đảng G2 | **Intermediate** (Party agencies) | Root G2 | `E4B402E3…1A81` |
| `cpg2.crt` | CA phục vụ các cơ quan Nhà nước G2 | **Intermediate** (State agencies) | Root G2 | `D8CAB7A4…AE4B` |

**Download paths differ per generation** — G2 lives under `/pki/pub/crt/`, G1 under `/pki/pub/cert/`
(`http://ca.gov.vn/pki/pub/cert/rootca.crt`, `.../cert/cp.crt`). Guessing the wrong one returns 404.

## 3. How they are used

- **Each Root is the *only* trust anchor of its branch**, pinned into `X509Chain.CustomTrustStore`. A chain is
  trusted only when its top element's SHA-256 equals that Root's pin — nothing from the Windows store is trusted.
- **Sub-CAs / intermediates go into `X509Chain.ExtraStore`.** They let a leaf resolve up to its Root even when the
  timestamp token / PDF does not embed the intermediate — real PDFs embed **only the leaf**, so `cp.crt` in
  `ExtraStore` is what makes the G1 signer chain buildable at all. They can only *enable* paths, never become
  anchors, so the "must end at the pinned Root" rule is preserved.
- **The signer gate accepts either generation.** Not G1 ⇒ try G2, and vice versa; only when **neither** chain builds
  is the verdict `UntrustedSigner` (blocking). G1 is tried first because production tokens are G1.
- **The TSA gate is G2-only** — timestamp tokens come from the Ban Cơ yếu TSA, which lives under Root G2.
- **The signer chain is verified at `trustedUtc`** (the token's raw `genTime`), never at `DateTime.Now`: the real
  signer cert expires **2026-09-25**, and `Now` would reject every genuine historical file from that day on. See
  [plans/signer-trust-gate.plan.md](plans/signer-trust-gate.plan.md).
- Every cert is **pinned by its DER fingerprint** (`GetCertHashString(SHA256)`), not by file bytes — so PEM↔DER
  re-encoding is safe and a swapped file fails closed. See
  [bug/ca-trust-cert-pem-vs-der-loader.md](bug/ca-trust-cert-pem-vs-der-loader.md).

## 4. Provenance — why these pins can be trusted

A pin taken from the same download it is meant to authenticate proves only "same file as before". Each pin here is
anchored in evidence that does **not** come from the download:

- **Root G2 + intermediates** — compared against copies embedded in real signed files.
- **Sub-CA G1 (`cp.crt`)** — cryptographically verifies as the issuer of all real signer certs (`build=true`, pinned
  CA at `ChainElements[1]`). Only the genuine Sub-CA's private key could have produced those signatures.
- **Root G1 (`rootca.crt`)** — its serial (`00FF303AE4E90AC1ED`) and SKI (`D9171B51…9D5C`) match the Authority Key
  Identifier that `cp.crt` states for its issuer, and anchoring at it makes `cp.crt` validate (`build=true`,
  `NoError`, 2-element chain). Control: anchoring at Root G2 yields `PartialChain`.

So trust runs **real PDFs → signer certs → `cp.crt` → `rootca.crt`**, with no circular step.

## 5. Where in code

| Concern | Location |
|---|---|
| Pins + filenames | `Sip.be.Shared/Constants/Documents/SignatureConstants.cs` (`RootG1*`, `SubCaG1*`, `RootG2*`, `IntermediateCaPins`) |
| Load (PEM **or** DER) + pin check | `SignatureVerifier.LoadPinnedCert` (`Sip.be.External/Documents/`) |
| Anchors, loaded once lazily | `SignatureVerifier.RootG1` / `SubCaG1` / `RootG2` / `IntermediateCAs` |
| Chain build (anchor + ExtraStore + `VerificationTime`) | `SignatureVerifier.ChainsToRoot` |
| Signer gate (G1 ∨ G2) | `SignatureVerifier.IsTrustedSigner` |
| Copy certs to output | `Sip.be.Api/Sip.be.Api.csproj` `<Content Include="*.crt">` (flows to `Sip.Desktop` publish transitively) |

## 6. File format & pinning notes

- `rootca.crt`, `cp.crt`, `rootcag2.crt`, `dcscag2.crt` are **PEM**; `cpg2` arrived as DER `.cer` and was converted
  to a PEM `cpg2.crt` (same cert, same fingerprint). `LoadPinnedCert` accepts both.
- Root G1 is signed **`sha1RSA`** (a 2010-era root); .NET still builds the chain (`NoError`) — no policy change needed.
- **Filenames are load-bearing.** `LoadPinnedCert` does `File.Exists(name)` and the csproj copies by literal name, so
  material arriving as `CP_G1.cer` must be renamed or the gate silently fails closed.
- A cert that is missing or whose fingerprint ≠ its pin is **skipped** (intermediate) or nulls its anchor —
  fail-closed, verified by removing each in turn: no `rootca.crt` **or** no `cp.crt` ⇒ every G1 signer becomes
  `UntrustedSigner`; no `rootcag2.crt` ⇒ every verdict `UntrustedTsa`.
- Adding a new G2 intermediate later = drop its `.crt` into `Sip.be.Api/`, add one row to `IntermediateCaPins`, one
  `<Content>` line in the csproj, and one line in `Sip.BE/.gitignore`.

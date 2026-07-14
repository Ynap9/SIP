# Ban Cơ yếu Chính phủ G2 trust certificates

> The three pinned CA certificates SIPPACK ships to validate digital signatures / trusted timestamps
> offline, and how they are wired into `SignatureVerifier`.
> Companion: [verify-signature-timestamp.md](verify-signature-timestamp.md).

## 1. The three certificates

All three are issued by **Ban Cơ yếu Chính phủ** under the G2 hierarchy (valid 2018 → 2048). They live beside the
app in `Sip.be.Api/` (gitignored, supplied out-of-band from `ca.gov.vn`) and are copied to the build output so
`SignatureVerifier` can load them.

| File | Subject (CN) | Role | Issuer | SHA-256 pin (DER) |
|---|---|---|---|---|
| `rootcag2.crt` | RootCA chuyên dùng Chính phủ G2 | **Root** (trust anchor, self-signed) | itself | `F04BA4B4…FDB8` |
| `dcscag2.crt` | CA phục vụ các cơ quan Đảng G2 | **Intermediate** (issues Party-agency certs) | Root G2 | `E4B402E3…1A81` |
| `cpg2.crt` | CA phục vụ các cơ quan Nhà nước G2 | **Intermediate** (issues State-agency certs) | Root G2 | `D8CAB7A4…AE4B` |

Hierarchy: `Root G2 → { dcscag2, cpg2 } → end-entity signer certs`. Both intermediates chain **directly** to
Root G2 (verified: 2-element `CustomRootTrust` chain ending at the Root G2 thumbprint).

## 2. How they are used

- **Root G2** is the *only* trust anchor: pinned into `X509Chain.CustomTrustStore`. A built chain is trusted only
  when its top element's SHA-256 equals the Root G2 pin — nothing from the Windows store is trusted.
- **The two intermediates** are added to `X509Chain.ExtraStore` during chain building. This lets a leaf cert
  (TSA or signer) resolve up to Root G2 even when the timestamp token / PDF does **not** embed its intermediate CA.
  They can only *enable* more chains to build; they never become trust anchors, so the "must end at Root G2" rule
  is preserved.
- Every cert is **pinned by its DER fingerprint** (`GetCertHashString(SHA256)`), not by file bytes — so PEM↔DER
  re-encoding is safe and a swapped file fails closed. See
  [bug/ca-trust-cert-pem-vs-der-loader.md](bug/ca-trust-cert-pem-vs-der-loader.md).

## 3. Where in code

| Concern | Location |
|---|---|
| Pins + filenames | `Sip.be.Shared/Constants/Documents/SignatureConstants.cs` (`RootG2Sha256`, `RootG2FileName`, `IntermediateCaPins`) |
| Load (PEM **or** DER) + pin check | `SignatureVerifier.LoadPinnedCert` (`Sip.be.External/Documents/`) |
| Root anchor / intermediate collection | `SignatureVerifier.RootG2` / `IntermediateCAs` (loaded once, lazily) |
| Chain build (anchor + ExtraStore) | `SignatureVerifier.ChainsToRoot` |
| Copy certs to output | `Sip.be.Api/Sip.be.Api.csproj` `<Content Include="*.crt">` (flows to `Sip.Desktop` publish transitively) |

## 4. File format & pinning notes

- `rootcag2.crt` and `dcscag2.crt` are **PEM** (`-----BEGIN CERTIFICATE-----`); `cpg2` arrived as DER `.cer` and was
  converted to a PEM `cpg2.crt` (same cert, same fingerprint) for consistency. `LoadPinnedCert` accepts both.
- A cert whose file is missing or whose fingerprint ≠ its pin is **skipped** (intermediate) or makes the anchor
  `null` (Root G2 → every verdict `UntrustedTsa`, fail-closed).
- Adding a new G2 intermediate later = drop its `.crt` into `Sip.be.Api/`, add one row to `IntermediateCaPins`, and
  add one `<Content>` line in the csproj.

# BUG (pre-empted): pinned Root CA loader must accept PEM, not only DER

> **Severity: MEDIUM — would fail-closed the whole timestamp check.** Caught during planning, before writing
> `SignatureVerifier`. Read before coding the trust-anchor loader for the signature-timestamp feature.
> Spec: [../verify-signature-timestamp.md](../verify-signature-timestamp.md); plan:
> [../plans/verify-signature-timestamp.plan.md](../plans/verify-signature-timestamp.plan.md).

## 1. Summary
The pinned trust anchor `Sip.be.Api/rootcag2.crt` downloaded from `http://ca.gov.vn/pki/pub/crt/rootcag2.crt` is
**PEM** (Base64 text, `-----BEGIN CERTIFICATE-----`), not DER. The loader originally planned —
`X509CertificateLoader.LoadCertificateFromFile` (net9) — accepts **DER only** and throws on PEM. That would make
`LoadRootG2()` return null → every file verdict becomes `UntrustedTsa` → **all attachments blocked** (feature dead).

## 2. Root cause
- net9's `X509CertificateLoader.LoadCertificateFromFile` is DER/PKCS12-only; PEM needs
  `X509Certificate2.CreateFromPemFile` (or the legacy auto-detecting `new X509Certificate2(path)`).
- The CA's AIA `caIssuers` endpoint serves the root as PEM; the file is committed/placed as-is (gitignored).

## 3. Evidence (measured on the file in repo)
- First bytes `2D 2D 2D 2D 2D 42 45 47 49 4E` = `-----BEGIN`, 2021 bytes → **PEM**.
- `certutil -hashfile rootcag2.crt SHA256` = `6e1ee702…c296` — this is the **hash of the PEM text**, NOT the cert.
- `certutil -dump rootcag2.crt` → `Cert Hash(sha256): f04ba4b459a7…fdb8` — the real cert fingerprint (= the pin).
- `X509Certificate2.GetCertHashString(SHA256)` = `F04BA4B459A7…FDB8` (matches the pin, format-independent).

## 4. Fix
Loader must handle both formats and pin by the **cert DER fingerprint**, not the file bytes:
```csharp
static X509Certificate2 Load(string path)
{
    var text = File.ReadAllText(path);
    return text.Contains("BEGIN CERTIFICATE")
        ? X509Certificate2.CreateFromPem(text)                   // PEM cert-only (ca.gov.vn download)
        : X509CertificateLoader.LoadCertificateFromFile(path);   // DER
}
// then: cert.GetCertHashString(HashAlgorithmName.SHA256) == SignatureConstants.RootG2Sha256
```

## 5. How to prevent
- **Pin a certificate by its DER fingerprint** (`GetCertHashString(SHA256)` / OpenSSL `-fingerprint -sha256` /
  certutil `Cert Hash(sha256)`), **never** by a file-bytes hash — the latter changes with PEM↔DER re-encoding.
- Any code loading a `.crt`/`.cer` from disk must accept **both PEM and DER** (external downloads vary).
- To read a cert fingerprint with certutil use `-dump` (shows `Cert Hash(sha256)`), not `-hashfile` (hashes the file).
- Use `X509Certificate2.CreateFromPem(text)` for a **cert-only** PEM. `CreateFromPemFile(path)` expects a cert **+
  private key** and throws `CryptographicException` on a cert-only PEM — verified at runtime it fail-closed every file
  to `UntrustedTsa`. (Runtime-tested: after the fix, all 13 real files → `ValidTrusted`.)

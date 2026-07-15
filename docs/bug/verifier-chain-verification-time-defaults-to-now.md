# BUG: TSA chain is validated at `DateTime.Now` — every archived file breaks when the TSA cert expires

> **Severity: HIGH — a dormant, self-triggering failure in an archival product.** No attacker needed: it fires on a
> calendar date. Runtime-confirmed 2026-07-15.
> Spec: [../verify-signature-timestamp.md](../verify-signature-timestamp.md).
> Fix plan: [../plans/verify-signature-hardening.plan.md](../plans/verify-signature-hardening.plan.md).

## 1. Summary
`SignatureVerifier.ChainsToRoot` never sets `X509ChainPolicy.VerificationTime`, so the TSA certificate's validity is
checked against **now** instead of against the moment the timestamp was issued. TSA leaf certificates are short-lived
(a few years); Root G2 runs to 2048. When a TSA cert expires, every document it ever timestamped flips from
`ValidTrusted` to `UntrustedTsa` — reason *"Dấu thời gian không thuộc TSA Ban Cơ yếu (chain không về Root G2)"* — and
**packing is blocked**, even though the timestamps are perfectly genuine.

The whole point of an RFC 3161 timestamp is to prove *"this signature existed at time T"* so it stays verifiable after
the signing cert expires. Validating the TSA chain at `now` throws that property away. For an archival product,
where old files are the normal case rather than the exception, this is backwards.

## 2. Root cause
`SignatureVerifier.cs`:

```csharp
using var chain = new X509Chain();
chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
chain.ChainPolicy.CustomTrustStore.Add(root);
chain.ChainPolicy.ExtraStore.AddRange(extra);
// VerificationTime khong duoc set -> mac dinh DateTime.Now
if (!chain.Build(leaf)) return false;
```

`X509ChainPolicy.VerificationTime` defaults to `DateTime.Now`, and `VerificationFlags` is `NoFlag`, so
`X509ChainStatusFlags.NotTimeValid` on an expired leaf makes `Build` return `false`. The caller maps that to
`UntrustedTsa` — a verdict that says "forged", when the truth is "the TSA cert aged out". The message misdirects
whoever investigates.

`ChainsToRoot` is also used for no other purpose, so the bug is confined to this one call site.

## 3. Evidence (runtime)
Self-signed root (2010→2048) + leaf `CN=PoC TSA 02` with EKU `1.3.6.1.5.5.7.3.8`, valid **2015-01-01 → 2018-01-01**
(expired; run date 2026-07-15). Chain built with the **same policy** as `ChainsToRoot`:

| `VerificationTime` | `chain.Build(leaf)` |
|---|---|
| not set → `DateTime.Now` (current behaviour) | **`False`** → `UntrustedTsa` → packing blocked |
| set to the timestamp's own time (2016-06-01) | `True` → correct |

The only difference is one policy line. Nothing about the certificates changed.

Not yet visible in production because the sample TSA cert (`Máy chủ cấp dấu thời gian 02`) is still inside its
validity window — the failure is latent and will appear **all at once**, for every historical file, on expiry day.

## 4. Fix
Validate the TSA chain **as of the timestamp**, not as of now:

```csharp
// trustedUtc = token.TokenInfo.Timestamp.UtcDateTime.AddHours(VgcaGenTimeOffsetHours)
chain.ChainPolicy.VerificationTime = trustedUtc.ToLocalTime();
```

`ChainsToRoot` must therefore take the trusted time as a parameter — compute `trustedUtc` **before** the chain build
and pass it in (today it is computed after). Two ordering notes:
- Use the **+7h-corrected** time (the VGCA quirk), otherwise the verification instant is 7h off and can fall outside
  a narrow validity window.
- The chain-of-trust is not weakened: the anchor is still the pinned Root G2 and the pin check on
  `chain.ChainElements[^1]` still runs. Only the *instant* of evaluation changes.

Distinguish the verdicts too: an expired-at-now-but-valid-at-stamp chain must not be reported as `UntrustedTsa`.

## 5. How to prevent
- **In any long-term-preservation check, validate a chain at the time the artifact was created, not at `now`.**
  Default `VerificationTime` = `DateTime.Now` is the wrong default for archival software; treat every `X509Chain`
  build as needing an explicit instant.
- **Test with an expired certificate.** Fixtures made from currently-valid certs cannot catch time bugs; certificate
  validity windows must themselves be test parameters. Cheap to do — `CertificateRequest.Create` mints an expired
  leaf in five lines, no CA material required.
- A verdict that blames the wrong cause ("not the Ban Cơ yếu TSA" for an expired cert) costs debugging hours later —
  map chain-status flags to distinct reasons instead of collapsing every `Build == false` into one message.

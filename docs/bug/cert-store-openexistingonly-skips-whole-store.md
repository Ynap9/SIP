# `OpenExistingOnly` silently skips the whole certificate store

> Reported from a deployment: "đã cài được bit4id nhưng vẫn không load được các chứng thư số của máy"
> — the Sign wizard listed no certificate at all, even for certificates that do not live on the token.

## Symptom
Step 1 of the Sign wizard fails with `CertificateNotFound`:
*"Không đọc được chứng thư số nào. Kiểm tra USB token đã cắm và middleware ... đã chạy chưa."*
The message points at the token, so the operator reinstalls the middleware — which changes nothing,
because the token was never the cause.

## Root cause
`CertificateProvider.GetCertificates()` opened each store with:

```csharp
store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
```

`OpenExistingOnly` makes `Open()` **throw** when the store does not exist yet. The Windows personal
store `CurrentUser\My` is created lazily — on a fresh Windows profile that has never held a personal
certificate, it simply is not there. The throw landed in

```csharp
catch { continue; }
```

which skipped **the entire store**, not just the missing certificate. The method then returned an empty
list, indistinguishable from "this machine genuinely has no certificates".

Two independent faults compounded it: the flag turned a normal state into an exception, and the bare
`catch` discarded the reason.

## Evidence
Measured directly, same store name, same machine:

```text
Open('ReadOnly,OpenExistingOnly') on a non-existent store -> throws
    CryptographicException: The system cannot find the file specified.
Open('ReadOnly')                  on a non-existent store -> succeeds, Certificates.Count = 0
```

## The fix
1. Drop `OpenExistingOnly` — an empty store must read as *empty*, not as *broken*.
2. Stop discarding the reason. `GetCertificates()` now returns `CertScanResultDto`, carrying
   `StoreDiagnostics` — one line per store, either `CurrentUser\My: 12 chứng thư` or
   `LocalMachine\My: không mở được (<exception>)`.
3. `SignService.GetCertificates` logs that summary and appends it to the `CertificateNotFound`
   message, so the operator can read what was actually scanned without a debugger or a log file.

## How to prevent
- A "not found / not yet created" condition is **not** an error — do not use API flags that promote it
  to one. `OpenExistingOnly` exists for callers that must distinguish the two; this caller does not.
- Never write `catch { continue; }` around an I/O probe whose failure changes the result. If a branch
  can silently remove data from an answer, the reason it fired has to survive into the answer.
- Any "nothing found" message shown to an operator must state **what was searched**. Without it the
  message steers them toward the wrong fix — here, reinstalling middleware that was already installed.
- The related failure mode is the same shape as the pinned-CA loader: see
  [ca-trust-cert-pem-vs-der-loader.md](ca-trust-cert-pem-vs-der-loader.md) — a missing file that fails
  closed and quietly makes every result wrong.

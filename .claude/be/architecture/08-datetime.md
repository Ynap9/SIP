# Date & Time (BE)

> **Rule: never call `DateTime.Now` / `DateTime.Today`.** Always take the current time from the single
> Vietnam-time source. SIPPACK is a **desktop** app running on the user's machine, so `Now` follows whatever
> time zone that machine is set to — a misconfigured machine silently shifts folder names, package names and
> timestamps, and nothing fails loudly.

## 1. The single source
`Sip.be.Shared/Constants/DateTimeConstants.cs`:

| Member | Meaning |
|---|---|
| `VietnamNow` | Current Vietnam time (UTC+7). Derived from `DateTime.UtcNow`, so it is independent of the host time zone. |
| `VietnamTimeZone` | `TimeZoneInfo` for Vietnam. Resolves `SE Asia Standard Time` (Windows) → `Asia/Ho_Chi_Minh` (IANA) → fixed UTC+7. |
| `VietnamTimeZoneId` / `VietnamTimeZoneIanaId` | The two zone ids, kept as constants rather than inline strings. |

Vietnam has **no DST**, so the offset is a constant +7 — but resolve through `TimeZoneInfo` anyway rather than
adding 7 hours by hand, so the intent stays readable and a future zone change is one edit.

## 2. How each layer gets the time
- **Application services** — use the inherited `BaseService.VietnamNow` property. Nothing to inject, nothing to pass.
  ```csharp
  var folder = Path.Combine(baseDir, $"chưa ký số_{VietnamNow:yyyyMMdd_HHmmss}");
  ```
- **Other layers** (`Sip.be.External`, `Sip.be.Api`, `Sip.be.Infratructure`) — they do not extend `BaseService`,
  so call the Shared source directly:
  ```csharp
  var now = DateTimeConstants.VietnamNow;
  ```

## 3. What this rule does NOT cover
Cryptographic verification time is a **different concern** and must not use `VietnamNow`:

- `SignatureVerifier` builds every certificate chain at `trustedUtc` = the RFC 3161 token's **raw** `genTime`
  (already true UTC — apply no offset), never at the current time. Validating an archived file against "now" makes
  every historical document fail the day a certificate expires — see
  [`docs/bug/verifier-chain-verification-time-defaults-to-now.md`](../../../docs/bug/verifier-chain-verification-time-defaults-to-now.md).
- Anything compared against a certificate's `NotBefore`/`NotAfter` for a **past** event must use that event's time,
  not the present.

Rule of thumb: **"when is it now?" → `VietnamNow`. "was this valid back then?" → the artifact's own timestamp.**

## 4. Known debt (not fixed in this task)
These call sites still use `DateTime.Now` and should migrate to `VietnamNow`:

| File | Line | Use |
|---|---|---|
| `Sip.be.Application/Document/Implements/PackService.cs` | 679, 1077 | package folder timestamp |
| `Sip.be.Application/Document/Implements/CheckService.cs` | 2022 | `chưa ký số_{date}` folder name |
| `Sip.be.Api/Controllers/WeatherForecastController.cs` | 26 | scaffold sample, delete when convenient |

They are left untouched here to keep this change scoped; migrate them in a dedicated task.

## 5. Don'ts
- Don't call `DateTime.Now` / `DateTime.Today` anywhere in BE code.
- Don't hardcode `AddHours(7)` to "make it Vietnam time". The one legitimate `+7` is
  `SignatureConstants.SignerLocalTimeQuirkHours`, and it is **never added to `genTime`** — it only normalises a
  `signingTime` that a signing tool wrote in local time while tagging it `Kind=Utc`. Adding it to `genTime` (the old
  behaviour) corrected the wrong side and shifted every chain build 7h — see
  [`docs/bug/genTime-offset-compensates-on-the-wrong-side.md`](../../../docs/bug/genTime-offset-compensates-on-the-wrong-side.md).
- Don't compare a self-asserted time against the TSA clock by **distance** — that clock is load-balanced across
  backends ~14 min apart and drifting. Only the **order** `signingTime ≤ genTime` is sound; see
  [`docs/bug/tsa-load-balanced-clocks-drift-14-minutes.md`](../../../docs/bug/tsa-load-balanced-clocks-drift-14-minutes.md).
- Don't store display-formatted time in the DB; format at the edge (DTO/response) only.

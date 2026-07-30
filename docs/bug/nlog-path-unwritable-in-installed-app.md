# Log file path is unwritable in the installed desktop app

> Found while trying to diagnose a certificate problem on a user's machine: there were no logs to read.

## Symptom
The dev API writes logs normally. The installed desktop app writes **none** — so any defect that only
reproduces on a user's machine has to be diagnosed blind.

## Root cause
`nlog.config` pointed the file target at a path relative to the binary:

```xml
fileName="${basedir}/../../../Log/Logs/Sip.be.Api_${date:format=ddMMyyyy}.log"
```

The three `..` segments were written for the dev layout, where `${basedir}` is
`Sip.be.Api/bin/Debug/net9.0` and climbing three levels lands on the project root. In the packaged app
`${basedir}` is the install directory itself, so the path resolves somewhere near the drive root —
and under `C:\Program Files\SIPPACK` a standard user cannot create it anyway.

NLog swallows target-initialisation failures for the file target, so the app kept running and nobody
noticed the logs were gone.

## The fix
Write to a per-user location that is always writable and does not depend on where the binary sits —
the same folder the app already uses for its SQLite database:

```xml
fileName="${specialfolder:folder=ApplicationData}/SipPack/logs/Sip.be.Api_${date:format=ddMMyyyy}.log"
```

Archive files moved alongside it. This works unchanged in dev and in the installed app.

## How to prevent
- Never resolve a **writable** path from `${basedir}` / `AppContext.BaseDirectory`. The install
  directory is read-only for normal users by design; only read-only assets (`certs/`, `drivers/`,
  `Templates/`, `wwwroot/`) belong there. Anything the app writes goes under `%AppData%\SipPack`.
- A path built by counting `..` segments encodes one particular build layout. It will be wrong in every
  other layout, and it fails silently rather than loudly.
- When adding a diagnostic feature, verify it on the **packaged** artifact, not just under `dotnet run`
  — this path had been wrong in every desktop build shipped so far.

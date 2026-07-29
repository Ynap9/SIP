# Sip.BE — Backend Architecture (read before coding)

> Canonical doc for humans & agents. **Read this folder before adding/changing BE code** so you follow the project's real structure and rules. Do not change the architecture on your own.

## Business goal
A **desktop** app (Photino host + Angular SPA + ASP.NET Core API in one process) to **check**, **pack**, **unpack** and **digitally sign** archival documents per the **TT05 / E-ARK CSIP** standard. Local DB is **SQLite**, storing only two config tables (`ConfigHoSo`, `ConfigVanBan`).

## Overall architecture — Clean Architecture (.NET 8+)

```
Sip.be.Api            → Controllers, Program.cs (composition root), BaseController
Sip.be.Application    → Business logic: Interfaces + Implements (Services), Dtos, Base, MappingProfile
Sip.be.Domain         → Entities (POCO): ConfigHoSo, ConfigVanBan
Sip.be.Infratructure  → SipDbContext (EF Core SQLite) + Migrations   [note: folder name is misspelled, keep it]
Sip.be.Shared         → ApiResponse, Exceptions, ErrorCodes, Constants, Interfaces, Templates/schemas
Sip.be.External       → 3rd-party/low-level work, SELF-CONTAINED (own Documents/{Interfaces,Implements,Dtos}):
                         PDF signature read + verify, PDF signing (incremental update), signature placement,
                         RFC 3161 TSA client, certificate store access, PDF/A inspection
Sip.Desktop           → Desktop host (Photino) that self-hosts the WebApplication in-process
```

**Dependency direction:** Api → Application → Domain; **Application → External**; Application/Infra/Api/External → Shared; Infra → Domain. Domain depends on **nothing**.

> `External` owns its own interfaces **and** DTOs (`Documents/Interfaces`, `Documents/Dtos`) and does **not** reference Application. Application only *calls* those contracts — never re-declares them. Wire/API DTOs stay in Application and are mapped onto the External DTOs at the service boundary.

## Document index
| File | Content |
|------|---------|
| [01-api-controllers.md](01-api-controllers.md) | Controllers, BaseController, routing, ApiResponse, SSE |
| [02-application-services.md](02-application-services.md) | Interface + Implements + BaseService + DI |
| [03-dtos-mapping.md](03-dtos-mapping.md) | DTO conventions & AutoMapper |
| [04-domain.md](04-domain.md) | Entities + soft-delete |
| [05-infrastructure.md](05-infrastructure.md) | DbContext, migrations, SQLite |
| [06-shared.md](06-shared.md) | Response, Exceptions, ErrorCodes, Constants, schemas |
| [07-conventions.md](07-conventions.md) | Coding rules, naming, error handling |
| [08-datetime.md](08-datetime.md) | Vietnam-time rule: never `DateTime.Now`, use `VietnamNow` |

## The business flows
- **Config** — import an Excel file (ClosedXML) into `ConfigHoSo`/`ConfigVanBan`; list them.
- **Check** — validate one record folder across 3 stages, **streamed in realtime via SSE**; `multi` validates a parent folder of many hồ sơ.
- **Pack** — pack a folder into a SIP package (EAD.xml + METS.xml + ZIP); nothing persisted to DB. `multi` packs a parent folder (SSE).
- **Unpack** — reverse a SIP `.zip`/folder back into a hồ sơ folder + Excel (`UnpackController`).
- **Sign** — list certificates (incl. USB token), gate PDF/A, sign a folder with a Ban Cơ yếu RFC 3161 timestamp (SSE) (`SignController`).

Multi + Unpack business rules: [../../../docs/multi-unpack-flows.md](../../../docs/multi-unpack-flows.md). Signing: [../../../docs/verify-signature-timestamp.md](../../../docs/verify-signature-timestamp.md).

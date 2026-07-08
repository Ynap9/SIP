# Sip.BE — Backend Architecture (read before coding)

> Canonical doc for humans & agents. **Read this folder before adding/changing BE code** so you follow the project's real structure and rules. Do not change the architecture on your own.

## Business goal
A **desktop** app (Photino host + Angular SPA + ASP.NET Core API in one process) to **check** and **pack** archival documents into SIP packages per the **TT05 / E-ARK CSIP** standard. Local DB is **SQLite**, storing only two config tables (`ConfigHoSo`, `ConfigVanBan`).

## Overall architecture — Clean Architecture (.NET 8+)

```
Sip.be.Api            → Controllers, Program.cs (composition root), BaseController
Sip.be.Application    → Business logic: Interfaces + Implements (Services), Dtos, Base, MappingProfile
Sip.be.Domain         → Entities (POCO): ConfigHoSo, ConfigVanBan
Sip.be.Infratructure  → SipDbContext (EF Core SQLite) + Migrations   [note: folder name is misspelled, keep it]
Sip.be.Shared         → ApiResponse, Exceptions, ErrorCodes, Constants, Interfaces, Templates/schemas
Sip.be.External       → (empty — reserved for future 3rd-party integrations)
Sip.Desktop           → Desktop host (Photino) that self-hosts the WebApplication in-process
```

**Dependency direction:** Api → Application → Domain; Application/Infra/Api → Shared; Infra → Domain. Domain depends on **nothing**.

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

## The three business flows
- **Config** — import an Excel file (ClosedXML) into `ConfigHoSo`/`ConfigVanBan`; list them.
- **Check** — validate one record folder across 3 stages, **streamed in realtime via SSE**.
- **Pack** — pack a folder into a SIP package (EAD.xml + METS.xml + ZIP); nothing persisted to DB.

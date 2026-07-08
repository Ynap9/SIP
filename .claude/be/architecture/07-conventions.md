# Coding Conventions (BE)

## Naming
- **Projects/namespaces**: `Sip.be.<Layer>`. Keep the existing quirks: `Infratructure` (misspelled), lowercase `Sip.be.infratructure.Persistence`, and the `Domain.Document` vs `Domain.Documents` namespace split.
- **Interfaces**: `I<Feature>Service`. **Implementations**: `<Feature>Service`.
- **DTOs**: `Add*Dto` / `View*Dto` / `*RequestDto` / `*ResultDto` / `*ProgressDto`.
- **Async methods**: suffix `Async`, return `Task`/`Task<T>`/`IAsyncEnumerable<T>`.
- **Routes**: kebab-case Vietnamese (`ho-so`, `van-ban`, `ho-so-folder`).
- **Domain field names** are Vietnamese (`TruongThongTin`, `MoTa`, `BatBuoc`) — match the business/Excel vocabulary.

## Language & comments
- Code comments and XML `<summary>` docs are written in **Vietnamese** in this codebase (domain is Vietnamese archival law). Match the surrounding style when editing existing files.
- Write dense `<summary>` docs on service interface methods explaining the business steps — future readers rely on them.

## Layering rules (enforce the dependency direction)
- Controllers do **no** business logic — delegate to services.
- Services depend on `SipDbContext` directly (no repository layer).
- Domain references nothing but `Sip.be.Shared` interfaces.
- Shared holds no business logic.

## Error handling pattern
1. Service throws `UserFriendlyException(ErrorCodes.X, "msg")` for expected failures.
2. Non-streaming controller action wraps the call in `try/catch` and returns `OkException(ex)` on error.
3. Every response is an `ApiResponse`; HTTP stays 200, real status is in the envelope.
4. Logging happens in `BaseController.OkException` and at the start of each service method.

## DI & registration
- All services are `AddScoped<IX, X>()` in `Program.cs` under the `#region services` block.
- AutoMapper via one `MappingProfile`; DbContext via `AddDbContext` (SQLite).
- CORS uses `ProgramExtensions.CorsPolicy`; origins come from `AllowedHosts` config (`;`-separated; `*` = any).

## Excel handling
- Use **ClosedXML** (`XLWorkbook`). Look up sheets with `TryGetWorksheet`; never assume a sheet exists.
- Map columns by header text into a case-insensitive dictionary; read data rows with `RowsUsed().Skip(1)`; drop empty rows.
- Sheet/column names come from `CheckConstants` / service `const`s — never inline magic strings.

## Hosting note
`Program.BuildApp(args)` builds the `WebApplication` (services + pipeline) and is shared by the web entry (`Program.Main`) and the desktop host (`Sip.Desktop`). Controllers are registered via `AddApplicationPart(typeof(Program).Assembly)` so they resolve when the entry assembly is `Sip.Desktop`. The SPA is served from `wwwroot` with `MapFallbackToFile("index.html")`.

## Don'ts
- Don't rename the misspelled `Infratructure` project or namespaces.
- Don't add a repository/unit-of-work layer unless asked.
- Don't hand-build error responses — use `OkException`.
- Don't hardcode strings that already live in a `*Constants` class.

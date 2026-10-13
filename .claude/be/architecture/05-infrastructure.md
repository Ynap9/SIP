# Infrastructure (`Sip.be.Infratructure`)

> The project folder name is misspelled (`Infratructure`) and the namespace is lowercase `Sip.be.infratructure.Persistence`. **Keep both as-is** — renaming ripples through every `using`.

## Layout
```
Sip.be.Infratructure/
├── Persistence/
│   ├── SipDbContext.cs                 # config tables            → sip.db
│   ├── SignTemplateDbContext.cs        # signing presets          → sign-templates.db
│   ├── SignTemplateDbContextFactory.cs # IDesignTimeDbContextFactory (dotnet ef only)
│   ├── SignTemplateMachineBinding.cs   # binds sign-templates.db to this Windows machine
│   └── SignTemplateMigrations/         # migrations for SignTemplateDbContext
└── Migrations/                         # migrations for SipDbContext (Initial + snapshot)
```

## Two DbContexts, two database files
**SQLite**, local files on the machine running the app (desktop-first), both under `%AppData%\SipPack`.

| Context | File | Connection comes from | Migrations |
|---|---|---|---|
| `SipDbContext` | `sip.db` | `ConnectionStrings:DefaultConnection` (Desktop overrides it with an env var) | `Migrations/` |
| `SignTemplateDbContext` | `sign-templates.db` | **hard-coded** `SignTemplateConstants.DatabasePath` | `Persistence/SignTemplateMigrations/` |

> **Why a separate file rather than two more tables in `sip.db`:** `build-desktop.ps1` freezes the dev machine's
> `sip.db` into `seed/sip.db` and ships it to **every** installation. Templates living in there would travel with the
> installer to other people's machines. A separate file makes that structurally impossible instead of depending on
> one script step. Its path is fixed in code, not configuration, so even dev-mode writes to `%AppData%` and never
> drops a DB into the repo.

> **Templates are bound to one machine (2026-10-13).** Before `AddDbContext<SignTemplateDbContext>`, `Program.cs`
> calls `SignTemplateMachineBinding.Ensure()`: it hashes `SipPack|<MachineGuid>` (SHA-256, `HKLM\SOFTWARE\Microsoft\
> Cryptography\MachineGuid`, fallback `Environment.MachineName`) and compares it with `%AppData%\SipPack\
> sign-templates.machine`. **Different machine → `sign-templates.db` (+ `-wal`/`-shm`/`-journal`) and
> `template-assets\` are deleted**, the file is rewritten, migrations recreate an empty DB and a warning is logged.
> **Missing file → current machine adopted, data kept** (so upgrading from an older build does not wipe templates).
> Never copy `sign-templates.db` between machines expecting it to survive.

- Registered in `Program.cs`:
  ```csharp
  builder.Services.AddDbContext<SipDbContext>(o =>
      o.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
  builder.Services.AddDbContext<SignTemplateDbContext>(o =>
      o.UseSqlite($"Data Source={SignTemplateConstants.DatabasePath}"));
  ```
- **Both** are migrated automatically at startup so a fresh install self-creates its DBs:
  ```csharp
  using var scope = app.Services.CreateScope();
  scope.ServiceProvider.GetRequiredService<SipDbContext>().Database.Migrate();
  scope.ServiceProvider.GetRequiredService<SignTemplateDbContext>().Database.Migrate();
  ```

## SipDbContext
```csharp
public class SipDbContext : DbContext {
    public DbSet<ConfigHoSo> ConfigHoSo { get; set; }
    public DbSet<ConfigVanBan> ConfigVanBan { get; set; }
    protected override void OnModelCreating(ModelBuilder mb) {
        mb.Entity<ConfigHoSo>(e => {
            e.Property(x => x.Deleted).HasDefaultValue(false);
            e.Property(x => x.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        // ConfigVanBan configured the same way
        base.OnModelCreating(mb);
    }
}
```
Defaults are set in the model (`Deleted=false`, `CreatedDate=CURRENT_TIMESTAMP`) rather than in code.

## SignTemplateDbContext
`DbSet<SignTemplate>` + `DbSet<SignTemplatePosition>`. `OnModelCreating` sets max lengths, **cascade delete** on
`Positions`, an index on `(SignTemplateId, Kind)` and `PageNumber` default 1. `SignTemplateDbContextFactory`
exists **only** so `dotnet ef` can build the context at design time — it is never used at runtime.

## Repository pattern?
There is **none**. Services depend on `SipDbContext` directly (via `BaseService`) and use EF LINQ. Don't introduce a  repository layer unless asked.

## Adding a table / changing schema
1. Add/modify the entity in `Sip.be.Domain`.
2. Add a `DbSet<T>` + `OnModelCreating` block in the **right** context.
3. Create a migration (from the API or Infra project), naming the context and its own output folder:
   ```bash
   dotnet ef migrations add <Name> -p Sip.be.Infratructure -s Sip.be.Api
   dotnet ef migrations add <Name> -p Sip.be.Infratructure -s Sip.be.Api \
       -c SignTemplateDbContext -o Persistence/SignTemplateMigrations
   ```
   Omitting `-c`/`-o` on the second context writes into `Migrations/` and corrupts the other context's snapshot.
4. No manual `Database.Update` needed — startup `Migrate()` applies it.

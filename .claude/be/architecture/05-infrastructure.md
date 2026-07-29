# Infrastructure (`Sip.be.Infratructure`)

> The project folder name is misspelled (`Infratructure`) and the namespace is lowercase `Sip.be.infratructure.Persistence`. **Keep both as-is** — renaming ripples through every `using`.

## Layout
```
Sip.be.Infratructure/
├── Persistence/SipDbContext.cs   # the single EF Core DbContext
└── Migrations/                   # EF Core migrations (Initial + snapshot)
```

## Database
- **SQLite**, local file on the machine running the app (desktop-first).
- Registered in `Program.cs`:
  ```csharp
  builder.Services.AddDbContext<SipDbContext>(o =>
      o.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
  ```
- Migrations are **applied automatically at startup** so a fresh install self-creates the DB:
  ```csharp
  using var scope = app.Services.CreateScope();
  scope.ServiceProvider.GetRequiredService<SipDbContext>().Database.Migrate();
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

## Repository pattern?
There is **none**. Services depend on `SipDbContext` directly (via `BaseService`) and use EF LINQ. Don't introduce a  repository layer unless asked.

## Adding a table / changing schema
1. Add/modify the entity in `Sip.be.Domain`.
2. Add a `DbSet<T>` + `OnModelCreating` block here.
3. Create a migration (from the API or Infra project), e.g.:
   `dotnet ef migrations add <Name> -p Sip.be.Infratructure -s Sip.be.Api`
4. No manual `Database.Update` needed — startup `Migrate()` applies it.

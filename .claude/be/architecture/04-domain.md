# Domain (`Sip.be.Domain`)

The innermost layer. **Zero dependencies** on other projects except `Sip.be.Shared` (for the audit interfaces). Contains only plain entities — no logic, no EF attributes.

## Layout
```
Sip.be.Domain/
└── Documents/
    ├── ConfigHoSo.cs     # namespace Sip.be.Domain.Document   (singular — note the mismatch)
    └── ConfigVanBan.cs   # namespace Sip.be.Domain.Documents
```
> Caveat: the two entities sit in the same folder but use **different namespaces** (`...Document` vs `...Documents`). Keep the existing namespace when editing a file; import both where needed.

## Entity convention
- POCO with auto-properties, `Id` (int) as key.
- Implement `ISoftDeleted` (from `Sip.be.Shared.Interfaces`) so rows are never hard-deleted.
- Audit/soft-delete fields are nullable dates + a `bool Deleted`.
- String fields default to `string.Empty`.

Example — the shape both current entities share:
```csharp
public class ConfigHoSo : ISoftDeleted
{
    public int Id { get; set; }
    public string TruongThongTin { get; set; } = string.Empty;  // "field name"
    public string MoTa { get; set; } = string.Empty;            // "description"
    public int BatBuoc { get; set; }                            // required flag (0/1)
    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public DateTime? DeletedDate { get; set; }
    public bool Deleted { get; set; }
}
```

## The two config tables
Both are the same shape and are the **only persisted data** in the app:
- `ConfigHoSo` — describes the fields expected in the record (Ho_so) sheet.
- `ConfigVanBan` — describes the fields expected in the document (Van_ban) sheet.

They are populated from Excel via `ConfigService` and consumed by `CheckService`/`PackService` to validate & map document metadata. `BatBuoc` uses `ConfigConstants.BatBuoc` (1) / `KhongBatBuoc` (0).

## Adding a new entity
1. Create the POCO here implementing `ISoftDeleted` (or `IFullAudited`).
2. Add a `DbSet<T>` + `OnModelCreating` config in `SipDbContext` (see [05-infrastructure.md](05-infrastructure.md)).
3. Add an EF migration.

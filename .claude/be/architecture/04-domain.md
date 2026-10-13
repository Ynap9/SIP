# Domain (`Sip.be.Domain`)

The innermost layer. **Zero dependencies** on other projects except `Sip.be.Shared` (for the audit interfaces). Contains only plain entities — no logic, no EF attributes.

## Layout
```
Sip.be.Domain/
└── Documents/
    ├── ConfigHoSo.cs             # namespace Sip.be.Domain.Document   (singular — note the mismatch)
    ├── ConfigVanBan.cs           # namespace Sip.be.Domain.Documents
    ├── SignTemplate.cs           # namespace Sip.be.Domain.Documents
    └── SignTemplatePosition.cs   # namespace Sip.be.Domain.Documents
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

## The two config tables (`sip.db`)
Both are the same shape:
- `ConfigHoSo` — describes the fields expected in the record (Ho_so) sheet.
- `ConfigVanBan` — describes the fields expected in the document (Van_ban) sheet.

They are populated from Excel via `ConfigService` and consumed by `CheckService`/`PackService` to validate & map document metadata. `BatBuoc` uses `ConfigConstants.BatBuoc` (1) / `KhongBatBuoc` (0).

## The sign-template tables (`sign-templates.db`)
A reusable signing preset — a **different database file**, see [05-infrastructure.md](05-infrastructure.md).
- `SignTemplate` — `TenTemplate`, `Thumbprint`, `TenChungThu`, `LyDoKy`, `NoiKy`, the four image-path fields
  (`AnhDauDo*`, `AnhChuKyTuoi*`), audit fields, and `List<SignTemplatePosition> Positions`.
- `SignTemplatePosition` — `Kind`, `PageNumber` (default 1) and `XRatio`/`YRatio`/`WidthRatio`/`HeightRatio`
  (all 0..1, origin **top-left**, Y down — same convention as the sign flow).

> `enum SignTemplatePositionKind { ChuKy = 0, DauDo = 1, ChuKyTuoi = 2 }` is **explicitly numbered**. Those numbers
> are already stored in users' databases: append new members at the end, **never insert or reorder**.

Check/pack/sign payloads themselves are still **never persisted** — these four tables are all the app stores.

## Adding a new entity
1. Create the POCO here implementing `ISoftDeleted` (or `IFullAudited`).
2. Add a `DbSet<T>` + `OnModelCreating` config in the **right** context — `SipDbContext` or `SignTemplateDbContext`
   (see [05-infrastructure.md](05-infrastructure.md)).
3. Add an EF migration **into that context's own migrations folder**.

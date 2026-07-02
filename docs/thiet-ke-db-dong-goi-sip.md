# DB design for SIP packaging (per TT05) — proposal for review

> Proposed table/entity structure to support checking & packaging SIP_tailieu per `chuan-dong-goi-sip-tt05.md`.
> No migration generated / no code changed yet — this is only to review whether it is sound. Entities follow the
> existing style (`ISoftDeleted`, int `Id`, audit fields).
>
> **Updated after review (2 agents + cross-check against the source PDF, pages 239–291).** Added: main↔attachment
> document relationship, `FileCreated` for `file/@CREATED`, real navigation properties + FKs, unique indexes,
> a `mimetype` group in `DanhMuc`, a submission-source flag, conditional constraints, and notes on the Circular's
> own internal conflicts. Changes are marked 🆕.

## 1. Principles
- **Dynamic lookups** (typeName, language, maintenance, mode, confidenceLevel, recordStatus, mimetype...) → a single
  lookup table `DanhMuc` (no hardcoding, editable when the Circular changes).
- **Package data** → `HoSo` (SIP package = EAD.xml + metsHdr) and `TaiLieu` (each document in `rep1/data` =
  EAD_doc/pic/media + `<file>`). Enough to **generate METS.xml / EAD.xml** and re-validate.
- **Keep** the existing `ConfigHoSo` / `ConfigVanBan` (used to validate Excel content) — they coexist, not replaced.
  These two tables are a **validation config dictionary** (each row = one field: name + description/enum + required
  flag), quite different from `HoSo`/`TaiLieu` (actual package data).
- 🆕 **Source of truth for code lookups (to decide):** currently `CheckService` derives the valid-code list by
  **parsing the `ConfigVanBan.MoTa` column** (regex like `"01: Nghị quyết"`). Adding `DanhMuc` must avoid **two
  parallel sources** for the same code set. → Proposal: `DanhMuc` is the canonical source, and `CheckService` reads
  codes from `DanhMuc` instead of parsing `MoTa` (a consolidation path, no parallel sources).
- **Hardcode (constants, NOT in the DB)**: E-ARK PROFILE, METS namespaces, `CHECKSUMTYPE=SHA-256`,
  `OAISPACKAGETYPE=SIP`, the software `agent` info (**`agent/name`=`SipPack`**), fixed folder names (`metadata`,
  `representations/rep1`, `schemas`, `data`). 🆕 Per the real package: `mets/@TYPE="Mixed"`, rep1
  `mets/@OBJID="rep1"` (fixed string), an empty `amdSec` element, `dmdSec/mdRef`: `MDTYPE="DC"` +
  `MDTYPEVERSION="1.0.0"` + `MIMETYPE="application/xml"`, schema `<file>` uses `application/octet-stream`.
  (No `documentation/` in the real package.)

## 2. Table list
| Table | Role |
|---|---|
| `DanhMuc` | Lookup for all TT05 coded categories (grouped by `Nhom`, including `mimetype`) |
| `HoSo` | One SIP_tailieu package (package metadata = EAD.xml + metsHdr info) |
| `TaiLieu` | One document in the package (EAD_doc/pic/media + `<file>` info from fileSec) |
| `ConfigHoSo` / `ConfigVanBan` | (existing) field description/validation config |
| 🆕 `GoiSipLog` (optional) | Packaging history: `Objid`, SIP sequence in the submission, ZIP file name, time, result |

Relationships:
- `HoSo (1) — (N) TaiLieu` (with navigation property + real FK, see section 6).
- 🆕 `TaiLieu (1) — (N) TaiLieu` self-reference: main ↔ attached document (`TaiLieuChinhId`) — supports
  `structMap/div[AttachmentFile]`.
- `TaiLieu.*` / `HoSo.RecordStatus` reference codes in `DanhMuc` by `Nhom` (store the code as string, validate at
  the app layer by `(Nhom, Ma)`).

## 3. Entities

### 3.1. DanhMuc (shared lookup)
```csharp
using Sip.be.Shared.Interfaces;

namespace Sip.be.Domain.Documents
{
    public class DanhMuc : ISoftDeleted
    {
        public int Id { get; set; }

        // Category group: typeName | language | maintenance | mode | confidenceLevel
        //               | recordStatus | typePic | typeMedia | colour | source | mimetype
        // 🆕 Prefer group-name constants (Sip.be.Shared/Constants) over free-form strings to avoid typos.
        public string Nhom { get; set; } = string.Empty;

        public string Ma { get; set; } = string.Empty;   // 01, 02, ... or NEW, SIP...
        public string Ten { get; set; } = string.Empty;  // Nghị quyết, Tiếng Việt, Vĩnh viễn...
        public string? GhiChu { get; set; }
        public int ThuTu { get; set; }

        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? DeletedDate { get; set; }
        public bool Deleted { get; set; }
    }
}
```

### 3.2. HoSo (record/SIP_tailieu package — EAD.xml + metsHdr)
> ⚠️ **Updated per the real sample package** (the package `ead.xsd` has **18 fields**, NO `source`; see
> `chuan-...md` section 8.2). The real record EAD is richer than TT05 table 3.1.
```csharp
using Sip.be.Shared.Interfaces;

namespace Sip.be.Domain.Documents
{
    public class HoSo : ISoftDeleted
    {
        public int Id { get; set; }

        // ----- Package identity (METS) -----
        public string Objid { get; set; } = string.Empty;        // OBJID = uuid-{UUIDs} (root METS)
        public string FileCode { get; set; } = string.Empty;     // record code, e.g. "H49.64.33.2001.160" (NOT hard 2/7 chars)

        // Optional parts to build/compare FileCode — in reality fileCode does NOT always follow the 2/7-char formula.
        public string? MaDinhDanh { get; set; }                  // org/individual identifier
        public int? NamHinhThanh { get; set; }
        public string? SoLanNop { get; set; }                    // (if applicable) 2 chars
        public string? SoThuTu { get; set; }                     // (if applicable) 7 chars

        // 🆕 SIP sequence within the submission (user-assigned) — builds ZIP name = [SoThuTuSip] + [Objid].
        public int? SoThuTuSip { get; set; }

        // ----- Record metadata (EAD.xml) — per the REAL ead.xsd (18 fields) -----
        public string Title { get; set; } = string.Empty;        // title (<=1000)
        public string? Maintenance { get; set; }                 // 🆕 record retention period (01-07)
        public string? Mode { get; set; }                        // 🆕 usage mode (01-03)
        public string? Language { get; set; }                    // 🆕 language (01-11)
        public string? StartDate { get; set; }                   // 🆕 startDate - string "DD/MM/YYYY" (per the real package)
        public string? EndDate { get; set; }                     // 🆕 endDate - string "DD/MM/YYYY" (per the real package)
        public string? Keyword { get; set; }                     // 🆕 keyword
        public int TotalDoc { get; set; }                        // totalDoc (compare with COUNT(TaiLieu))
        public int? NumberOfPaper { get; set; }                  // 🆕 sheet count
        public int? NumberOfPage { get; set; }                   // 🆕 page count
        public string? Format { get; set; }                      // 🆕 physical condition
        public string? InforSign { get; set; }                   // 🆕 information notation
        public string? ConfidenceLevel { get; set; }             // 🆕 confidence level (01-03)
        public string? PaperFileCode { get; set; }               // 🆕 paper record code (paperFileCode)
        public bool? RiskRecovery { get; set; }                  // 🆕 backup mode (0/1)
        public string? RiskRecoveryStatus { get; set; }          // 🆕 backup status (01/02)
        public string? Description { get; set; }                 // note (<=2000)
        // Column `Source` REMOVED — the real ead.xsd has no source field (locked to the real package).

        // ----- metsHdr -----
        // Real: root agent TYPE=OTHER/SOFTWARE name="ArchiveNex" note IDENTIFICATIONCODE=MaPhong;
        //       rep1 agent TYPE=INDIVIDUAL name=<user> note IDENTIFICATIONCODE=MaPhong.
        public string? MaPhong { get; set; }                     // IDENTIFICATIONCODE (fonds code), e.g. "P_KHCN"
        public string? NguoiTao { get; set; }                    // 🆕 rep1 agent name (creator)
        public string? MaDangKyNop { get; set; }                 // altRecordID = SUBMISSIONAGREEMENT (in reality = FileCode)
        public string RecordStatus { get; set; } = "NEW";        // DanhMuc[recordStatus]
        public DateTime? CreateDate { get; set; }                // METS CREATEDATE (of the package)
        public DateTime? LastModDate { get; set; }               // METS LASTMODDATE

        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? DeletedDate { get; set; }
        public bool Deleted { get; set; }

        // 🆕 Navigation -> lets EF Core create a real FK + index HoSoId (see section 6)
        public ICollection<TaiLieu> TaiLieus { get; set; } = new List<TaiLieu>();
    }
}
```

### 3.3. TaiLieu (document in rep1/data — EAD_doc/pic/media + <file>)
```csharp
using Sip.be.Shared.Interfaces;

namespace Sip.be.Domain.Documents
{
    public class TaiLieu : ISoftDeleted
    {
        public int Id { get; set; }
        public int HoSoId { get; set; }                          // FK -> HoSo (with navigation -> real FK)

        // 🆕 Main ↔ attachment relationship (structMap/div[AttachmentFile]).
        //    null = main document; set = which main document this is attached to.
        public int? TaiLieuChinhId { get; set; }

        // 1 = text (EAD_doc), 2 = negative film/photo (EAD_pic), 3 = video/audio (EAD_media)
        // 🆕 prefer a LoaiMetadata enum (Sip.be.Shared/Constants) over bare 1/2/3.
        public int LoaiMetadata { get; set; }

        // ----- Identity & physical file (<file> in fileSec) -----
        public string? DocId { get; set; }                       // 🆕 nullable: only EAD_doc has docId (<=25); pic/media do not
        public string DocCode { get; set; } = string.Empty;      // document storage code -> emits tag <docCode> (locked to the real package)
        public string? TenFile { get; set; }                     // file name in data/
        public string? MimeType { get; set; }                    // DanhMuc/mimetype
        public string? Extension { get; set; }
        public long? FileSize { get; set; }                      // bytes -> file/@SIZE
        public string? Checksum { get; set; }                    // SHA-256 -> file/@CHECKSUM
        public DateTime? FileCreated { get; set; }               // 🆕 file/@CREATED (per-file creation time, distinct from HoSo.CreateDate)

        // ----- Shared across types -----
        public string? Maintenance { get; set; }                 // retention period (01-07)
        public string? Mode { get; set; }                        // usage mode (01-03)
        public string? Format { get; set; }                      // physical condition
        public bool? RiskRecovery { get; set; }                  // backup mode
        public string? RiskRecoveryStatus { get; set; }          // 01/02 (required when RiskRecovery=true)
        public string? InforSign { get; set; }                   // information notation
        public string? Description { get; set; }                 // note
        public string? Language { get; set; }                    // language (doc, media)
        public string? ArchivesNumber { get; set; }              // archives number (pic, media)
        public string? EventName { get; set; }                   // event name (pic, media)
        public bool? DocAttached { get; set; }                   // attached document (pic, media)

        // ----- Text (EAD_doc) -----
        public string? TypeName { get; set; }                    // document type (01-32)
        public string? CodeNumber { get; set; }                  // document number
        public string? CodeNotation { get; set; }                // document notation
        public string? IssuedDate { get; set; }                  // document date - string "DD/MM/YYYY" (per the real package)
        public string? OrganName { get; set; }                   // issuing organization
        public string? Subject { get; set; }                     // abstract
        public int? NumberOfPage { get; set; }                   // page count
        public string? Keyword { get; set; }                     // keyword
        public string? ConfidenceLevel { get; set; }             // confidence level (01-03)
        public string? Autograph { get; set; }                   // autograph
        public bool? Process { get; set; }                       // processing workflow

        // ----- Negative film/photo (EAD_pic) -----
        public string? TypePic { get; set; }                     // 01 negative film / 02 photo
        public string? ImageTitle { get; set; }
        public string? Photographer { get; set; }
        public string? PhotoPlace { get; set; }
        public string? PhotoTime { get; set; }                   // "DD/MM/YYYY" (per the real package)
        public string? Colour { get; set; }                      // 01 colour / 02 black & white
        public string? FilmSize { get; set; }

        // ----- Video/audio (EAD_media) -----
        public string? TypeMedia { get; set; }                   // 01 audio / 02 video
        public string? MovieTitle { get; set; }
        public string? Recorder { get; set; }
        public string? RecordPlace { get; set; }
        public string? RecordDate { get; set; }                  // "DD/MM/YYYY" (per the real package)
        public string? PlayTime { get; set; }                    // duration
        public string? Quality { get; set; }

        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? DeletedDate { get; set; }
        public bool Deleted { get; set; }

        // 🆕 Navigation
        public HoSo? HoSo { get; set; }
        public TaiLieu? TaiLieuChinh { get; set; }               // the main document (if this row is an attachment)
        public ICollection<TaiLieu> TaiLieuDinhKems { get; set; } = new List<TaiLieu>();
    }
}
```

## 4. Field ↔ real EAD/METS mapping
- `HoSo` ↔ `EAD.xml` (real ead.xsd): `FileCode→fileCode`, `Title→title`, `Maintenance→maintenance`, `Mode→mode`,
  `Language→language`, `StartDate→startDate`, `EndDate→endDate`, `Keyword→keyword`, `TotalDoc→totalDoc`,
  `NumberOfPaper→numberOfPaper`, `NumberOfPage→numberOfPage`, `Format→format`, `InforSign→inforSign`,
  `ConfidenceLevel→confidenceLevel`, `PaperFileCode→paperFileCode`, `RiskRecovery→riskRecovery`,
  `RiskRecoveryStatus→riskRecoveryStatus`, `Description→description`.
  `Objid/MaPhong/NguoiTao/MaDangKyNop/RecordStatus/CreateDate/LastModDate` ↔ METS (mets/metsHdr/agent/altRecordID).
- `TaiLieu` ↔ EAD_doc/pic/media: column name = field name (camelCase). `DocCode` = file name in `data/` (`= docId.pdf`).
  `FileSize/Checksum/MimeType/FileCreated` ↔ `<file>` in `fileSec` (`@SIZE/@CHECKSUM/@MIMETYPE/@CREATED`).
- 🆕 Metadata↔file link (real package): each `TaiLieu` ⇒ 1 `dmdSec`(mdRef→EAD_DOC_*.xml) + 1 `<file>`(data/*.pdf) +
  1 `div[MetadataLink/File]` (`@DMDID`=dmdSec, `fptr@FILEID`=data `<file>` ID). Generatable directly from one row.
- 🆕 `TaiLieuChinhId` ↔ `structMap/div[AttachmentFile]` (when the package has attachments; the sample doesn't use it).

## 5. Design choices (conclusions after review)
1. **`TaiLieu` as one "union" table** (other-type fields nullable, distinguished by `LoaiMetadata`) — **KEEP this**.
   Simple, fits the current flat style, the 3 types share many common fields. Trade-off: "required-by-type"
   constraints must be enforced at the app layer (section 7).
   - *Alternative (not needed yet)*: `TaiLieu` (common) + 3 detail tables `TaiLieuVanBan` / `TaiLieuPhimAnh` /
     `TaiLieuMedia` — more normalized but more tables & JOINs, not worth the benefit.
2. **`DanhMuc` merged into one table** (by `Nhom`) — **KEEP**. Compact, true to the dynamic-lookup spirit. In return:
   unique `(Nhom, Ma)`, use `Nhom` name constants, store code as string + validate `(Nhom, Ma)` at the app layer
   (no real FK per column, since an FK can't constrain the correct `Nhom`).
3. 🆕 **`GoiSipLog`** (recommended on): `Objid`, `SoThuTuSip`, ZIP file name, time, result — for tracing &
   rebuilding the ZIP name.

## 6. 🆕 EF Core configuration to declare (OnModelCreating)
> The codebase currently does **not** use `IEntityTypeConfiguration`/annotations; the only config is a default value
> for `Deleted`/`CreatedDate` in `SipDbContext`. New entities must declare the following, otherwise EF ignores them.

- **Real FK + index** (if you only have a `HoSoId` column without config, EF treats it as a loose int, NOT a foreign key):
  - `HoSo (1) — (N) TaiLieu` via `HasOne(t => t.HoSo).WithMany(h => h.TaiLieus).HasForeignKey(t => t.HoSoId)`.
  - `TaiLieu` self-reference: `HasOne(t => t.TaiLieuChinh).WithMany(t => t.TaiLieuDinhKems).HasForeignKey(t => t.TaiLieuChinhId)`
    — **`OnDelete(Restrict)`** to avoid a cyclic cascade.
- **Unique index**: `HoSo.Objid`, `HoSo.FileCode`, `TaiLieu (HoSoId, DocCode)`, `DanhMuc (Nhom, Ma)`.
- **Regular index**: `TaiLieu.HoSoId`, `TaiLieu.TaiLieuChinhId`.
- **Default value + soft-delete** for all 3 new entities: `Deleted = false`, `CreatedDate = CURRENT_TIMESTAMP`
  (mirroring the existing `ConfigHoSo/ConfigVanBan`).
- **Column length** (TT05): with SQLite (current provider) not mandatory, but declare `HasMaxLength` to match the
  standard & be ready for SQL Server: `fileCode 100`, `title 1000`, `description(package) 2000`, `docId 25`,
  `docCode 100`, `subject 500`, `autograph 2000`, `codeNumber 11`, `codeNotation 30`, `inforSign 30`, `keyword 100`...
  (per the tables in section 3 of `chuan-dong-goi-sip-tt05.md`).
- **Enums/constants**: `LoaiMetadata` (1/2/3), `RecordStatus` (NEW/...) → put constants in `Sip.be.Shared/Constants`
  (following the `ConfigConstants`/`CheckConstants` precedent).

## 7. 🆕 Business constraints to enforce (app layer — complex CHECKs are hard in SQLite)
- `RiskRecoveryStatus` is **required** when `RiskRecovery = true`.
- `Process` is **required** when `ConfidenceLevel ∈ {01, 03}` (electronic documents processed on the System).
- `Maintenance` only accepts `01` (Permanent) for **submission/collection sources** → a source classification flag is
  needed to check this. *(If every package is a submission source, hardcode the rule; otherwise add a source
  classification column to `HoSo`.)*
- Text must be **two-layer PDF/A**: the DB only checks up to `MimeType=application/pdf`; the "two-layer" property is
  checked at the convert/packaging layer.
- `FileCode`/`DocCode` correct structure: identifier + year + submission count (2 chars) + sequence (7 chars) →
  validate via regex; `HoSo` already has `MaDinhDanh/NamHinhThanh/SoLanNop/SoThuTu` to build & compare `FileCode`.
- `TotalDoc` compared against `COUNT(TaiLieu)` of the package on validation.
- Category codes (`TypeName/Language/Maintenance/Mode/ConfidenceLevel/TypePic/TypeMedia/Colour/RecordStatus...`)
  validated against the **value set** in `DanhMuc (Nhom, Ma)`, not just by length.

## 8. 🆕 Locked to the REAL PACKAGE (resolves the PDF's conflicts)
> Cross-checked the source PDF (pages 239–291) **and** the sample package `uuid-0F203B84...` (schema + 137 XML files).
> Take the **real package as the execution standard**.
- **`riskRecovery`/`riskRecoveryStatus`**: the PDF is contradictory (the 3.x tables have them, the `.xsd` in the PDF
  doesn't). **Real package: schemas `ead.xsd`/`ead_doc.xsd`/`ead_pic.xsd`/`ead_media.xsd` ALL have them** and all
  137 XML files write them → **KEEP both fields** (already in `TaiLieu`, added to `HoSo`).
- **Record EAD has NO `source`** → **column `Source` REMOVED**. Added the 13 real record fields
  (startDate/endDate/numberOfPaper/paperFileCode/maintenance/mode/language/format/confidenceLevel/inforSign/keyword/numberOfPage)
  to `HoSo` (section 3.2).
- **`docCode` vs `arcDocCode`**: **locked to `docCode`** (all 137 real XML files emit `<docCode>`); the generator
  emits the `<docCode>` tag.
- **EAD dates** (`startDate/endDate/issuedDate/photoTime/recordDate`): **store as `string "DD/MM/YYYY"`** exactly like
  the real package (only METS `CreateDate/LastModDate/FileCreated` are `DateTime` ISO).
- `docId` only exists in `EAD_doc`, not in pic/media → `DocId` nullable.
- Category codes: validate against the **`DanhMuc` code set** rather than by length (TT05 lengths are oddly wide:
  `typeName` 10, `maintenance` 100 for 2-char values).
- **AttachmentFile**: the sample does NOT use it (all documents independent), but TT05 has it → still keep
  `TaiLieuChinhId` so packages with attachments can be described.

> Fixed standard constants (PROFILE, namespaces, folder names, SHA-256, OAISPACKAGETYPE=SIP, software agent) live in
> `Sip.be.Shared/Constants`, not in the DB.

# Application Services (`Sip.be.Application`)

The heart of the app. Every feature = one **interface** + one **implementation** + its **DTOs**.

## Layout
```
Sip.be.Application/
├── Base/
│   ├── BaseService.cs        # shared deps for all services
│   └── MappingProfile.cs     # AutoMapper profile (see 03-dtos-mapping.md)
└── Document/
    ├── Interfaces/           # CHỈ service của Application: IConfigService, ICheckService, IPackService,
    │                         #   IUnpackService, ISignService
    ├── Implements/           # ConfigService, CheckService, PackService, UnpackService, SignService
    └── Dtos/                 # Check/, Config/, Pack/, Unpack/, Sign/ — DTO của WIRE (API), 1 type 1 file
```
Contracts của `Sip.be.External` (`IPdfSignatureReader`, `ISignatureVerifier`, `IPdfFormatInspector`,
`ICertificateProvider`, `ISigningKey`, `ITimestampClient`, `IPdfSigner`, `ISignaturePlacementResolver`) **và DTO của
chúng** nằm trong chính `Sip.be.External/Documents/{Interfaces,Dtos}` — Application chỉ `using` rồi gọi, **không**
khai lại. Service ánh xạ DTO wire → DTO External ở biên (vd `SignRequestDto` → `PdfSignOptionsDto`).

## Interface convention
- One interface per feature, named `I<Feature>Service`, in `Document/Interfaces/`.
- Document **every method with an XML `<summary>`** describing the business steps (see existing interfaces — they are detailed on purpose so callers/agents understand the flow without reading the impl).
- Method naming: async work ends with `Async` and returns `Task`/`Task<T>`; streaming returns `IAsyncEnumerable<T>` with a trailing `CancellationToken cancellationToken = default`.

## Implementation convention
- `public class <Feature>Service : BaseService, I<Feature>Service`.
- Constructor forwards to base: `: base(sipDbContext, logger, mapper)`.
- Put feature-local literals as `private const` at the top (sheet names, header names, regex patterns) — e.g. `ConfigService.SheetName`, `CheckService.OptionPattern`.
- Log the entry of each public method: `_logger.LogInformation($"{nameof(Method)}, ...")`.
- Throw `UserFriendlyException(ErrorCodes.X, "message")` for expected/business errors — the controller's `OkException` turns it into a clean response.
- Excel I/O uses **ClosedXML** (`XLWorkbook`); look up sheets with `TryGetWorksheet`, map columns by header text (case-insensitive dictionary), skip the header row, filter empty rows.

## BaseService
```csharp
public class BaseService {
    public readonly SipDbContext _sipDbContext;
    public readonly ILogger<BaseService> _logger;
    protected readonly IMapper _mapper;
    // ctor assigns all three
}
```
Every service gets DbContext + logger + AutoMapper for free. Don't re-inject these in the child.

## Dependency Injection (registered in `Program.cs`)
```csharp
builder.Services.AddScoped<IConfigService, ConfigService>();
builder.Services.AddScoped<ICheckService, CheckService>();
builder.Services.AddScoped<IPackService, PackService>();
builder.Services.AddScoped<IUnpackService, UnpackService>();
builder.Services.AddScoped<ISignService, SignService>();
// implementations from Sip.be.External
builder.Services.AddScoped<IPdfSignatureReader, PdfSignatureReader>();
builder.Services.AddScoped<ISignatureVerifier, SignatureVerifier>();
builder.Services.AddScoped<IPdfFormatInspector, PdfFormatInspector>();
builder.Services.AddScoped<ICertificateProvider, CertificateProvider>();
builder.Services.AddScoped<ISigningKey, StoreSigningKey>();      // Scoped: caches cert + key handle for one signing batch
builder.Services.AddScoped<ITimestampClient, TimestampClient>();
builder.Services.AddScoped<IPdfSigner, PdfSigner>();
builder.Services.AddScoped<ISignaturePlacementResolver, SignaturePlacementResolver>();
```
All services are **Scoped**. When adding a feature: create the interface + impl, then register the pair here.

## Feature specifics
- **ConfigService** — reads sheet `Mô tả_Ho_so`/`Mô tả_Van_ban`, `AddRangeAsync` + `SaveChangesAsync`; list queries filter `!Deleted` and project to `View*Dto`.
- **CheckService** — 3 stages, yields `CheckProgressDto` per step (folder structure → code matching → Excel content); a wrapper estimates `Total` for progress %.
  - **Chữ ký số**: mỗi file đính kèm được đọc chữ ký qua `PdfSignatureReader.Read(file)` (static, zero-dep — quét `/ByteRange`+`/Contents`, giải mã bằng `System.Security.Cryptography.Pkcs.SignedCms`, lấy CN cert + `signingTime`/`/M`). Kết quả gắn vào `CheckProgressDto.Signature` (single, event file gđ1) và `FileCheckResultDto.Signature` (multi). Thuần, chạy được trong nhánh song song. Thông tin ký số **không tính là lỗi** chặn đóng gói.
  - **Gom file chưa ký**: `CollectUnsignedFilesAsync` (single) / `CollectUnsignedMultiAsync` (multi) copy các file (theo tên FE gửi) vào `chưa ký số_{date}/{Mã hồ sơ}_chưa ký số/` (tạo cùng cấp thư mục đầu vào), trả `CollectUnsignedResultDto.ParentPath` để FE mở. Không quét lại PDF (FE đã biết file nào chưa ký từ kết quả check).
- **PackService** — builds EAD/METS XML (`System.Xml.Linq`), computes checksums, copies attachments + schemas, zips. **No DB writes.** File identity is `docId` = data file name (never `docCode`).
  - **Multi handles all 3 structures like CheckService** — `PackMultiFolderAsync` calls `ResolveSharedExcelPath(parent)`: `null` → loại 1 (per-hồ-sơ `metadata/`+`attachment/`, via `PackOneHoSo`); else loại 2/3 → `PackMultiSharedExcelAsync` (shared Excel parsed once, sliced per hồ sơ, data files taken directly from each folder, via `PackOneHoSoShared` + `BuildSimpleDcFromCells`). Single pack (`PackHoSoFolderAsync`) is loại 1 only. See [pack-multi gap bug](../../../docs/bug/pack-multi-missing-shared-excel-structure.md).
- **UnpackService** — reverses a SIP `.zip` **or** package folder into `{output}/{fileCode}/` with `Metadata/` + `Attachment/` (capitalised on purpose) + a rebuilt `{fileCode}.xlsx`; returns the file tree. Needs config rows for the Excel headers (`ErrorCodes.ConfigEmpty`). **No DB writes.**
- **SignService** — certificate listing, token pre-flight, PDF preview navigation (`GetPreviewAt`/`OpenPdfForPreview`), PDF/A gate and folder signing (SSE). Validates the signature position **once per batch** (ratios 0..1, `ErrorCodes.SignPositionInvalid`) and maps `SignRequestDto` → `PdfSignOptionsDto`; the chosen position + page count apply to **every file in the folder**. Delegates all crypto/PDF work to the `Sip.be.External` contracts above; **never touches `_sipDbContext`**. Details: [../../../docs/verify-signature-timestamp.md](../../../docs/verify-signature-timestamp.md), [../plans/pdf-signing-be.plan.md](../plans/pdf-signing-be.plan.md).

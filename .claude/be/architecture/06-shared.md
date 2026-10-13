# Shared (`Sip.be.Shared`)

Cross-cutting primitives used by every layer. No business logic.

## Layout
```
Sip.be.Shared/
├── Request/
│   ├── ApiResponse.cs                 # ApiResponse, ApiResponse<T>, enum StatusCodeE
│   ├── AppException/                  # BaseException, UserFriendlyException
│   ├── BaseRequest/                   # BaseRequestPagingDto, BaseResponsePagingDto<T>, PagingExtension
│   └── ErrorRequest/                  # ErrorCodes, ErrorMessages
├── Interfaces/IFullAudited.cs         # ISoftDeleted, ICreatedBy, IModifiedBy, IFullAudited
├── Constants/
│   ├── ProgramExtensions.cs           # CorsPolicy name
│   ├── DateTimeConstants.cs           # VietnamNow / VietnamTimeZone (see 08-datetime.md)
│   ├── Check/CheckConstants.cs        # folder/sheet/column names, allowed extensions
│   └── Documents/                     # ConfigConstants, EadFieldMapping, SipPackConstants,
│                                      #   SignatureConstants, SigningConstants, SignTemplateConstants
└── Templates/
    ├── schemas/                       # *.xsd (METS, EAD, xlink) copied into every SIP package
    └── sample/file-mau-ky-so.pdf      # sample PDF served by GET template-chu-ky/file-mau
```
> `.csproj` copies **both** `Templates/**/*.xsd` and `Templates/**/*.pdf` to the output directory, and declares
> `<FrameworkReference Include="Microsoft.AspNetCore.App" />` because `BaseRequestPagingDto` uses `[FromQuery]`.
> Drop that reference and the **whole solution** stops building.

## ApiResponse — the uniform envelope
```csharp
enum StatusCodeE { Success = 1, Error = 0 }
class ApiResponse { StatusCodeE Status; object? Data; int Code; string Message; }
class ApiResponse<T> : ApiResponse { new T Data; }
```
Constructors: `new()` = success/no data; `new(data)` = success + data (Code 200, "Ok"); `new(status, data, code, message)` = explicit (used by `OkException` for errors). Business status is carried **inside** the envelope, not via HTTP status codes.

## Exceptions
- `BaseException(int errorCode, string? messageLocalize)` — base with `ErrorCode`, optional localized message, `ListParam`.
- `UserFriendlyException : BaseException` — **the one to throw** for expected/business errors. `OkException` maps it to an error `ApiResponse` with the right code/message.
- Never throw raw `Exception` for business rules; use `UserFriendlyException(ErrorCodes.X, "msg")`.

## Error codes & messages
- `ErrorCodes` — `const int` catalog: generic (`BadRequest=400`, `NotFound=404`, `InternalServerError=500`) + domain codes, currently `1001`–`1041`: check single (`FolderNotFound=1001` … `CheckHasErrors=1016`), check multi (`1017`–`1021`), unpack (`1022`–`1023`), `FileNotBelongToHoSo=1024`, signature/timestamp (`TimestampInvalid=1025`, `SignatureInvalid=1026`), signing (`1027`–`1035`), `PdfFormatInvalid=1036`, `SignPositionInvalid=1037`, sign template (`SignTemplateNotFound=1038`, `SignTemplateNameDuplicated=1039`, `SignTemplateImageInvalid=1040`, `SignTemplateSampleMissing=1041` — **declared but never thrown**, see [../../contracts/sign-template.contract.md](../../contracts/sign-template.contract.md)).
- `ErrorMessages.GetMessage(code)` — resolves a message when the exception carries no explicit text.

## Paging (`Request/BaseRequest/`)
```csharp
class BaseRequestPagingDto  { int PageSize; int PageNumber; string? Keyword; List<string> Sort; int GetSkip(); }
class BaseResponsePagingDto<T> { List<T> Items; int TotalItems; object? CustomData; }
static IQueryable<T> Paging<T>(this IQueryable<T> q, BaseRequestPagingDto req);
```
`Keyword` self-trims. **`PageSize = -1` means "no paging — return everything"** (`PagingParameter.DefaultPageSize`).
Only `SignTemplateController` uses this today.

> Quirk: the namespace is **`traobang.be.shared.HttpRequest.BaseRequest`** — inherited verbatim from another
> project. It is referenced by live code; keep it.

## Constants (do not hardcode these elsewhere)
- `CheckConstants` — `metadata`/`attachment` folder names, `Ho_so`/`Van_ban` sheet names, column headers, `TargetType*` tags, stage display names, allowed Excel/attachment extensions.
- `ConfigConstants` — `BatBuoc=1`, `KhongBatBuoc=0`.
- `EadFieldMapping` — Vietnamese header → EAD element name maps (Ho_so 18 fields, Van_ban 21 fields).
- `SipPackConstants` — METS namespaces, schema locations, agent info for the SIP standard.
- `SignatureConstants` — verify side: pinned CA file names + SHA-256 pins, RFC 3161 / ESS OIDs, `TsaClockSkewGraceSeconds`, `SignerLocalTimeQuirkHours`.
- `SigningConstants` — sign side: TSA URL + content type + timeout, `ETSI.CAdES.detached`, appearance metrics, `_daky_` suffix, parallelism limits, token-monitor interval.
- `SignTemplateConstants` — `%AppData%\SipPack` app-data dir, `sign-templates.db`, `template-assets/`, sample PDF path, allowed image extensions + 5 MB cap, paging defaults.
- `DateTimeConstants` — `VietnamNow`, `VietnamTimeZone`. **Never `DateTime.Now`** — see [08-datetime.md](08-datetime.md).
- `ProgramExtensions.CorsPolicy` — CORS policy name string.

## Templates
- `schemas/` — `.xsd` files (METS, EAD variants, xlink) that `PackService` copies into each package's `schemas/` folder. Referenced by `SipPackConstants.SchemaLocation*`.
- `sample/file-mau-ky-so.pdf` — the sample PDF returned by `GET template-chu-ky/file-mau` so the FE can preview block placement without a real document.

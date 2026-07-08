# Shared (`Sip.be.Shared`)

Cross-cutting primitives used by every layer. No business logic.

## Layout
```
Sip.be.Shared/
├── Request/
│   ├── ApiResponse.cs                 # ApiResponse, ApiResponse<T>, enum StatusCodeE
│   ├── AppException/                  # BaseException, UserFriendlyException
│   └── ErrorRequest/                  # ErrorCodes, ErrorMessages
├── Interfaces/IFullAudited.cs         # ISoftDeleted, ICreatedBy, IModifiedBy, IFullAudited
├── Constants/
│   ├── ProgramExtensions.cs           # CorsPolicy name
│   ├── Check/CheckConstants.cs        # folder/sheet/column names, allowed extensions
│   └── Documents/                     # ConfigConstants, EadFieldMapping, SipPackConstants
└── Templates/schemas/                 # *.xsd (METS, EAD, xlink) copied into every SIP package
```

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
- `ErrorCodes` — `const int` catalog: generic (`BadRequest=400`, `NotFound=404`, `InternalServerError=500`) + domain codes for the check flow (`FolderNotFound=1001` … `CheckHasErrors=1016`).
- `ErrorMessages.GetMessage(code)` — resolves a message when the exception carries no explicit text.

## Constants (do not hardcode these elsewhere)
- `CheckConstants` — `metadata`/`attachment` folder names, `Ho_so`/`Van_ban` sheet names, column headers, `TargetType*` tags, stage display names, allowed Excel/attachment extensions.
- `ConfigConstants` — `BatBuoc=1`, `KhongBatBuoc=0`.
- `EadFieldMapping` — Vietnamese header → EAD element name maps (Ho_so 18 fields, Van_ban 21 fields).
- `SipPackConstants` — METS namespaces, schema locations, agent info for the SIP standard.
- `ProgramExtensions.CorsPolicy` — CORS policy name string.

## Templates/schemas
`.xsd` files (METS, EAD variants, xlink) that `PackService` copies into each package's `schemas/` folder. Referenced by `SipPackConstants.SchemaLocation*`.

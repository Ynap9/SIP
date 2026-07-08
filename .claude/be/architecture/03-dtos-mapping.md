# DTOs & Mapping

## Location & naming
DTOs live under `Sip.be.Application/Document/Dtos/<Feature>/`. Conventions by purpose:
- `Add<Entity>Dto` — inbound create/import payload (e.g. `AddConfigHoSoDto { IFormFile File; }`).
- `View<Entity>Dto` — outbound read model (e.g. `ViewConfigHoSoDto`).
- `<Feature>RequestDto` — inbound request for an operation (e.g. `CheckHoSoFolderRequestDto`, `PackHoSoFolderRequestDto`).
- `<Feature>ResultDto` / `<Feature>ProgressDto` — outbound result / streamed event (e.g. `PackResultDto`, `CheckProgressDto`).

## Rules
- Plain POCOs with `{ get; set; }` auto-properties.
- Initialize reference types to safe defaults: `string X { get; set; } = string.Empty;`, `List<T> X { get; set; } = new();`.
- Document non-obvious DTOs and fields with XML `<summary>` (the Check/Pack DTOs are heavily commented — follow that).
- Enums used across the wire live next to their DTO (e.g. `CheckStage` in `CheckProgressDto.cs`).
- Computed/derived values can be expression-bodied read-only props (e.g. `CheckProgressDto.Percent => Total > 0 ? ... : 0`).
- File uploads use `Microsoft.AspNetCore.Http.IFormFile`.

## AutoMapper
- Single profile: `Sip.be.Application/Base/MappingProfile.cs`.
- Registered in `Program.cs`: `builder.Services.AddAutoMapper(cfg => { }, typeof(MappingProfile));`
- Add entity→DTO maps there:
  ```csharp
  CreateMap<ConfigHoSo, ViewConfigHoSoDto>();
  CreateMap<ConfigVanBan, ViewConfigVanBanDto>();
  ```
- Note: some services (e.g. `ConfigService.GetConfigHoSo`) hand-project with LINQ `Select` instead of `_mapper` for full control. Both styles exist; prefer AutoMapper for straight 1:1 maps, manual projection when you filter/transform fields.

## Wire format
JSON is serialized **camelCase** (see the SSE options in `CheckController`). So `TruongThongTin` (C#) → `truongThongTin` (JSON) → matches the FE `IConfigItem` interface. Keep FE models in sync when DTO fields change.

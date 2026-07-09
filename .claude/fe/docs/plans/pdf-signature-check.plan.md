# Kiểm tra chữ ký số file đính kèm — Build Plan

## Requirements
- Khi CHECK (single + multi), mỗi file đính kèm được đọc chữ ký số (PDF/PAdES/PKCS#7): **đã ký hay chưa**, và nếu ký thì lấy **người ký (Signed by = CN cert)** + **thời gian ký** (signingTime CMS, fallback `/M`).
- Đọc bằng **zero-dependency**: `PdfSignatureReader` tự quét `/ByteRange` + `/Contents`, parse bằng `System.Security.Cryptography.Pkcs.SignedCms` (có sẵn .NET 9). Chỉ áp dụng `.pdf`.
- Response trả thêm `Signature` cho **từng file**: single qua `CheckProgressDto.Signature` (event file giai đoạn 1); multi qua `FileCheckResultDto.Signature`.
- FE: thêm cột **"Ký số"** ở bảng file (single `check.html`, multi bảng con `multi.html`): đã ký → hiện người ký + thời gian; chưa ký → "Chưa ký".
- Ký số là **thông tin hiển thị**, KHÔNG tính là lỗi chặn đóng gói.
- Khi đóng gói, nếu có file chưa ký → popup form hiện **1 dòng** "Xem các file chưa ký số". Bấm → BE gom file chưa ký vào cấu trúc rồi FE mở thư mục cha:
  ```
  <thư mục chứa folder đầu vào>/chưa ký số_<yyyyMMdd_HHmmss>/
      <MãHồSơ>_chưa ký số/   <các file chưa ký của hồ sơ đó>
  ```
  Single = 1 subfolder; multi = nhiều subfolder. Người dùng vẫn có thể tiếp tục đóng gói bình thường.
- FE đã biết file nào chưa ký (từ kết quả check) → gửi danh sách tên file cho BE để copy (không quét lại PDF).

## Decisions
- **D1** — `PdfSignatureReader` static (Application/Document/Implements), thuần, không DB → dùng được cả single lẫn multi song song.
- **D2** — Không xác thực toàn vẹn/cert chain, chỉ trích **danh tính người ký + thời gian** (đúng nhu cầu "đã ký + ai + khi nào"), giống Foxit hiển thị.
- **D3** — Gom file chưa ký: 2 endpoint non-SSE `collect-unsigned` (single) + `collect-unsigned-multi` (multi) trả `ApiResponse<CollectUnsignedResultDto>`; FE gọi rồi `DesktopService.openPath(parentPath)` (web-dev fallback clipboard).
- **D4** — Cảnh báo chưa ký đặt trong 2 package dialog (input `folderPath`/`parentFolderPath` + danh sách file/hồ sơ chưa ký), 1 dòng bấm được.

## Steps
1. BE DTO: `SignatureInfoDto`+`SignerInfoDto`; thêm `Signature` vào `FileCheckResultDto` + `CheckProgressDto`; `CollectUnsigned*RequestDto` + `CollectUnsignedResultDto`. `dotnet build`.
2. BE `PdfSignatureReader.cs` (quét ByteRange/Contents → SignedCms → CN + signingTime/M). `dotnet build`.
3. BE `CheckService`: gắn signature ở stream single (event file gđ1) + `InspectHoSo`/`InspectHoSoShared` (seed Files); thêm `CollectUnsignedFilesAsync` + `CollectUnsignedMultiAsync` + helper `GetDataFiles`. `ICheckService` khai báo 2 method. `dotnet build`.
4. BE `CheckController`: 2 action `collect-unsigned` + `collect-unsigned-multi` (try/catch → OkException). `dotnet build`.
5. FE models: `check.models.ts` + `multi-check.models.ts` thêm `ISignatureInfo`/`ISignerInfo`, `signature?` (progress/filerow/filecheck), `ICollectUnsignedResult`. `npm run build`.
6. FE services: `check.service.ts.collectUnsigned()`, `multi-check.service.ts.collectUnsignedMulti()`. `npm run build`.
7. FE single: `check.ts` lưu signature vào row + tính unsigned; `check.html` cột "Ký số"; `package-dialog` cảnh báo + `onViewUnsigned`. `npm run build`.
8. FE multi: `multi.ts` map signature + gom unsigned theo hồ sơ; `multi.html` cột "Ký số" bảng con; `package-multi-dialog` cảnh báo + `onViewUnsigned`. `npm run build`.
9. Cập nhật docs BE (`02-application-services.md`, `03-dtos-mapping.md`) + ghi chú FE. `npm run build` + `dotnet build` cuối.

## Expected output
- BE mới: `PdfSignatureReader.cs`, `SignatureInfoDto.cs`, `CollectUnsigned*Dto.cs`; sửa `CheckService`, `ICheckService`, `CheckController`, `FileCheckResultDto`, `CheckProgressDto`.
- FE mới cột "Ký số" + luồng "Xem file chưa ký số"; sửa models/services/pages/dialogs (single + multi).
- Giữ nguyên: luồng SSE, Pack/Unpack, DB, routes.

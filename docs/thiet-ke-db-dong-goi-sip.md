# Thiết kế DB cho đóng gói SIP (theo TT05) — bản đề xuất để review

> Đề xuất cấu trúc bảng/entity phục vụ kiểm tra & đóng gói SIP_tailieu theo `chuan-dong-goi-sip-tt05.md`.
> Chưa sinh migration / chưa sửa code — chỉ để xem có hợp lý không. Entity viết theo style sẵn có (`ISoftDeleted`, `Id` int, audit fields).
>
> **Cập nhật sau review (2 agent + đối chiếu PDF gốc trang 239–291).** Đã bổ sung: quan hệ tài liệu chính ↔ đính kèm, `FileCreated` cho `file/@CREATED`, navigation property + FK thật, unique index, nhóm `mimetype` trong `DanhMuc`, cờ nguồn nộp lưu, ràng buộc điều kiện, và ghi chú mâu thuẫn của chính Thông tư. Các thay đổi đánh dấu 🆕.

## 1. Nguyên tắc
- **Danh mục động** (typeName, language, maintenance, mode, confidenceLevel, recordStatus, mimetype...) → 1 bảng lookup `DanhMuc` (không hardcode, sửa được khi TT đổi).
- **Dữ liệu gói** → `HoSo` (gói SIP = EAD.xml + metsHdr) và `TaiLieu` (mỗi tài liệu trong `rep1/data` = EAD_doc/pic/media + `<file>`). Đủ để **sinh METS.xml / EAD.xml** và kiểm tra lại.
- **Giữ nguyên** `ConfigHoSo` / `ConfigVanBan` hiện có (dùng để validate nội dung Excel) — coexist, không thay thế. Hai bảng này là **từ điển cấu hình validate** (mỗi dòng = 1 trường: tên + mô tả/enum + cờ bắt buộc), khác hẳn `HoSo`/`TaiLieu` (dữ liệu gói thật).
- 🆕 **Nguồn sự thật của danh mục mã (cần chốt):** hiện `CheckService` rút danh sách mã hợp lệ bằng cách **parse cột `ConfigVanBan.MoTa`** (regex kiểu `"01: Nghị quyết"`). Nếu thêm `DanhMuc` thì phải tránh **2 nguồn song song** cho cùng tập mã. → Đề xuất: `DanhMuc` là nguồn chuẩn, `CheckService` đọc mã từ `DanhMuc` thay vì parse `MoTa` (lộ trình hợp nhất, không giữ song song).
- **Hardcode (constants, KHÔNG vào DB)**: PROFILE E-ARK, namespace METS, `CHECKSUMTYPE=SHA-256`, `OAISPACKAGETYPE=SIP`, thông tin `agent` phần mềm (**`agent/name`=`SipPack`**), tên thư mục cố định (`metadata`, `representations/rep1`, `schemas`, `data`). 🆕 Theo gói thật: `mets/@TYPE="Mixed"`, rep1 `mets/@OBJID="rep1"` (chuỗi cố định), có phần tử `amdSec` rỗng, `dmdSec/mdRef`: `MDTYPE="DC"` + `MDTYPEVERSION="1.0.0"` + `MIMETYPE="application/xml"`, `<file>` schema dùng `application/octet-stream`. (Không có `documentation/` trong gói thật.)

## 2. Danh sách bảng
| Bảng | Vai trò |
|---|---|
| `DanhMuc` | Lookup mọi danh mục mã hoá của TT05 (nhóm theo `Nhom`, gồm cả `mimetype`) |
| `HoSo` | 1 gói SIP_tailieu (metadata gói = EAD.xml + thông tin metsHdr) |
| `TaiLieu` | 1 tài liệu trong gói (EAD_doc/pic/media + thông tin `<file>` trong fileSec) |
| `ConfigHoSo` / `ConfigVanBan` | (đã có) cấu hình mô tả/validate trường |
| 🆕 `GoiSipLog` (tuỳ chọn) | Lịch sử đóng gói: `Objid`, STT SIP trong lần nộp, tên file ZIP, thời gian, kết quả |

Quan hệ:
- `HoSo (1) — (N) TaiLieu` (có navigation property + FK thật, xem mục 6).
- 🆕 `TaiLieu (1) — (N) TaiLieu` tự tham chiếu: tài liệu chính ↔ tài liệu đính kèm (`TaiLieuChinhId`) — phục vụ `structMap/div[AttachmentFile]`.
- `TaiLieu.*` / `HoSo.RecordStatus` tham chiếu mã trong `DanhMuc` theo `Nhom` (lưu mã string, validate tầng app theo `(Nhom, Ma)`).

## 3. Entity

### 3.1. DanhMuc (lookup chung)
```csharp
using Sip.be.Shared.Interfaces;

namespace Sip.be.Domain.Documents
{
    public class DanhMuc : ISoftDeleted
    {
        public int Id { get; set; }

        // Nhóm danh mục: typeName | language | maintenance | mode | confidenceLevel
        //               | recordStatus | typePic | typeMedia | colour | source | mimetype
        // 🆕 Nên dùng hằng tên nhóm (Sip.be.Shared/Constants) thay cho string tự do để tránh sai chính tả.
        public string Nhom { get; set; } = string.Empty;

        public string Ma { get; set; } = string.Empty;   // 01, 02, ... hoặc NEW, SIP...
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

### 3.2. HoSo (hồ sơ/gói SIP_tailieu — EAD.xml + metsHdr)
> ⚠️ **Cập nhật theo gói mẫu thực tế** (`ead.xsd` trong gói có **18 trường**, KHÔNG có `source`; xem `chuan-...md` mục 8.2). EAD hồ sơ thực tế giàu trường hơn bảng 3.1 của TT05.
```csharp
using Sip.be.Shared.Interfaces;

namespace Sip.be.Domain.Documents
{
    public class HoSo : ISoftDeleted
    {
        public int Id { get; set; }

        // ----- Định danh gói (METS) -----
        public string Objid { get; set; } = string.Empty;        // OBJID = uuid-{UUIDs} (root METS)
        public string FileCode { get; set; } = string.Empty;     // mã hồ sơ, vd "H49.64.33.2001.160" (KHÔNG cứng 2/7 ký tự)

        // Thành phần (tuỳ chọn) để dựng/đối chiếu FileCode — thực tế fileCode KHÔNG luôn theo công thức 2/7 ký tự.
        public string? MaDinhDanh { get; set; }                  // mã định danh cơ quan/tổ chức/cá nhân
        public int? NamHinhThanh { get; set; }
        public string? SoLanNop { get; set; }                    // (nếu áp dụng) 2 ký tự
        public string? SoThuTu { get; set; }                     // (nếu áp dụng) 7 ký tự

        // 🆕 STT của gói trong lần nộp (người dùng tự đánh) — dựng tên ZIP = [SoThuTuSip] + [Objid].
        public int? SoThuTuSip { get; set; }

        // ----- Metadata hồ sơ (EAD.xml) — theo ead.xsd THỰC TẾ (18 trường) -----
        public string Title { get; set; } = string.Empty;        // title (<=1000)
        public string? Maintenance { get; set; }                 // 🆕 thời hạn lưu trữ hồ sơ (01-07)
        public string? Mode { get; set; }                        // 🆕 chế độ sử dụng (01-03)
        public string? Language { get; set; }                    // 🆕 ngôn ngữ (01-11)
        public string? StartDate { get; set; }                   // 🆕 startDate - string "DD/MM/YYYY" (theo gói thật)
        public string? EndDate { get; set; }                     // 🆕 endDate - string "DD/MM/YYYY" (theo gói thật)
        public string? Keyword { get; set; }                     // 🆕 từ khóa
        public int TotalDoc { get; set; }                        // totalDoc (đối chiếu COUNT(TaiLieu))
        public int? NumberOfPaper { get; set; }                  // 🆕 số tờ
        public int? NumberOfPage { get; set; }                   // 🆕 số trang
        public string? Format { get; set; }                      // 🆕 tình trạng vật lý
        public string? InforSign { get; set; }                   // 🆕 ký hiệu thông tin
        public string? ConfidenceLevel { get; set; }             // 🆕 mức độ tin cậy (01-03)
        public string? PaperFileCode { get; set; }               // 🆕 mã hồ sơ giấy (paperFileCode)
        public bool? RiskRecovery { get; set; }                  // 🆕 chế độ dự phòng (0/1)
        public string? RiskRecoveryStatus { get; set; }          // 🆕 tình trạng dự phòng (01/02)
        public string? Description { get; set; }                 // ghi chú (<=2000)
        // Đã BỎ cột `Source` — ead.xsd thực tế không có trường source (chốt theo gói thật).

        // ----- metsHdr -----
        // Thực tế: agent root TYPE=OTHER/SOFTWARE name="ArchiveNex" note IDENTIFICATIONCODE=MaPhong;
        //          agent rep1 TYPE=INDIVIDUAL name=<user> note IDENTIFICATIONCODE=MaPhong.
        public string? MaPhong { get; set; }                     // IDENTIFICATIONCODE (mã phông), vd "P_KHCN"
        public string? NguoiTao { get; set; }                    // 🆕 name của agent rep1 (người tạo)
        public string? MaDangKyNop { get; set; }                 // altRecordID = SUBMISSIONAGREEMENT (thực tế = FileCode)
        public string RecordStatus { get; set; } = "NEW";        // DanhMuc[recordStatus]
        public DateTime? CreateDate { get; set; }                // METS CREATEDATE (của gói)
        public DateTime? LastModDate { get; set; }               // METS LASTMODDATE

        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? DeletedDate { get; set; }
        public bool Deleted { get; set; }

        // 🆕 Navigation -> để EF Core sinh FK thật + index HoSoId (xem mục 6)
        public ICollection<TaiLieu> TaiLieus { get; set; } = new List<TaiLieu>();
    }
}
```

### 3.3. TaiLieu (tài liệu trong rep1/data — EAD_doc/pic/media + <file>)
```csharp
using Sip.be.Shared.Interfaces;

namespace Sip.be.Domain.Documents
{
    public class TaiLieu : ISoftDeleted
    {
        public int Id { get; set; }
        public int HoSoId { get; set; }                          // FK -> HoSo (có navigation -> FK thật)

        // 🆕 Quan hệ tài liệu chính ↔ đính kèm (structMap/div[AttachmentFile]).
        //    null = tài liệu chính; có giá trị = đính kèm vào tài liệu chính nào.
        public int? TaiLieuChinhId { get; set; }

        // 1 = văn bản (EAD_doc), 2 = phim âm bản/ảnh (EAD_pic), 3 = phim/âm thanh (EAD_media)
        // 🆕 nên định nghĩa enum LoaiMetadata (Sip.be.Shared/Constants) thay cho 1/2/3 trần.
        public int LoaiMetadata { get; set; }

        // ----- Định danh & file vật lý (<file> trong fileSec) -----
        public string? DocId { get; set; }                       // 🆕 nullable: chỉ EAD_doc có docId (<=25); pic/media không có
        public string DocCode { get; set; } = string.Empty;      // mã lưu trữ tài liệu -> sinh tag <docCode> (chốt theo gói thật)
        public string? TenFile { get; set; }                     // tên file trong data/
        public string? MimeType { get; set; }                    // DanhMuc/mimetype
        public string? Extension { get; set; }
        public long? FileSize { get; set; }                      // bytes -> file/@SIZE
        public string? Checksum { get; set; }                    // SHA-256 -> file/@CHECKSUM
        public DateTime? FileCreated { get; set; }               // 🆕 file/@CREATED (thời điểm tạo TỪNG file, khác HoSo.CreateDate)

        // ----- Dùng chung nhiều loại -----
        public string? Maintenance { get; set; }                 // thời hạn lưu trữ (01-07)
        public string? Mode { get; set; }                        // chế độ sử dụng (01-03)
        public string? Format { get; set; }                      // tình trạng vật lý
        public bool? RiskRecovery { get; set; }                  // chế độ dự phòng
        public string? RiskRecoveryStatus { get; set; }          // 01/02 (bắt buộc khi RiskRecovery=true)
        public string? InforSign { get; set; }                   // ký hiệu thông tin
        public string? Description { get; set; }                 // ghi chú
        public string? Language { get; set; }                    // ngôn ngữ (doc, media)
        public string? ArchivesNumber { get; set; }              // số lưu trữ (pic, media)
        public string? EventName { get; set; }                   // tên sự kiện (pic, media)
        public bool? DocAttached { get; set; }                   // tài liệu đi kèm (pic, media)

        // ----- Văn bản (EAD_doc) -----
        public string? TypeName { get; set; }                    // loại tài liệu (01-32)
        public string? CodeNumber { get; set; }                  // số tài liệu
        public string? CodeNotation { get; set; }                // ký hiệu tài liệu
        public string? IssuedDate { get; set; }                  // ngày tài liệu - string "DD/MM/YYYY" (theo gói thật)
        public string? OrganName { get; set; }                   // cơ quan ban hành
        public string? Subject { get; set; }                     // trích yếu
        public int? NumberOfPage { get; set; }                   // số trang
        public string? Keyword { get; set; }                     // từ khóa
        public string? ConfidenceLevel { get; set; }             // mức độ tin cậy (01-03)
        public string? Autograph { get; set; }                   // bút tích
        public bool? Process { get; set; }                       // quy trình xử lý

        // ----- Phim âm bản/ảnh (EAD_pic) -----
        public string? TypePic { get; set; }                     // 01 phim âm bản / 02 ảnh
        public string? ImageTitle { get; set; }
        public string? Photographer { get; set; }
        public string? PhotoPlace { get; set; }
        public string? PhotoTime { get; set; }                   // "DD/MM/YYYY" (theo gói thật)
        public string? Colour { get; set; }                      // 01 màu / 02 đen trắng
        public string? FilmSize { get; set; }

        // ----- Phim/âm thanh (EAD_media) -----
        public string? TypeMedia { get; set; }                   // 01 ghi âm / 02 ghi hình
        public string? MovieTitle { get; set; }
        public string? Recorder { get; set; }
        public string? RecordPlace { get; set; }
        public string? RecordDate { get; set; }                  // "DD/MM/YYYY" (theo gói thật)
        public string? PlayTime { get; set; }                    // thời lượng
        public string? Quality { get; set; }

        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? DeletedDate { get; set; }
        public bool Deleted { get; set; }

        // 🆕 Navigation
        public HoSo? HoSo { get; set; }
        public TaiLieu? TaiLieuChinh { get; set; }               // tài liệu chính (nếu bản ghi này là đính kèm)
        public ICollection<TaiLieu> TaiLieuDinhKems { get; set; } = new List<TaiLieu>();
    }
}
```

## 4. Ánh xạ trường ↔ EAD/METS thật
- `HoSo` ↔ `EAD.xml` (ead.xsd thật): `FileCode→fileCode`, `Title→title`, `Maintenance→maintenance`, `Mode→mode`, `Language→language`, `StartDate→startDate`, `EndDate→endDate`, `Keyword→keyword`, `TotalDoc→totalDoc`, `NumberOfPaper→numberOfPaper`, `NumberOfPage→numberOfPage`, `Format→format`, `InforSign→inforSign`, `ConfidenceLevel→confidenceLevel`, `PaperFileCode→paperFileCode`, `RiskRecovery→riskRecovery`, `RiskRecoveryStatus→riskRecoveryStatus`, `Description→description`. `Objid/MaPhong/NguoiTao/MaDangKyNop/RecordStatus/CreateDate/LastModDate` ↔ METS (mets/metsHdr/agent/altRecordID).
- `TaiLieu` ↔ EAD_doc/pic/media: tên cột = tên trường (camelCase). `DocCode` = tên file trong `data/` (`= docId.pdf`). `FileSize/Checksum/MimeType/FileCreated` ↔ `<file>` trong `fileSec` (`@SIZE/@CHECKSUM/@MIMETYPE/@CREATED`).
- 🆕 Liên kết metadata↔file (gói thật): mỗi `TaiLieu` ⇒ 1 `dmdSec`(mdRef→EAD_DOC_*.xml) + 1 `<file>`(data/*.pdf) + 1 `div[MetadataLink/File]` (`@DMDID`=dmdSec, `fptr@FILEID`=ID `<file>` data). Sinh được trực tiếp từ 1 bản ghi.
- 🆕 `TaiLieuChinhId` ↔ `structMap/div[AttachmentFile]` (khi gói có tài liệu đính kèm; gói mẫu không dùng).

## 5. Lựa chọn thiết kế (kết luận sau review)
1. **`TaiLieu` 1 bảng "union"** (trường loại khác = nullable, phân biệt bằng `LoaiMetadata`) — **GIỮ phương án này**. Đơn giản, hợp style phẳng hiện tại, 3 loại chia sẻ nhiều trường chung. Đánh đổi: ràng buộc "bắt buộc theo loại" phải làm ở tầng app (mục 7).
   - *Phương án thay thế (chưa cần)*: `TaiLieu` (chung) + 3 bảng chi tiết `TaiLieuVanBan` / `TaiLieuPhimAnh` / `TaiLieuMedia` — chuẩn hoá hơn nhưng nhiều bảng & JOIN, không tương xứng lợi ích.
2. **`DanhMuc` gộp 1 bảng** (theo `Nhom`) — **GIỮ**. Gọn, đúng tinh thần danh mục động. Bù lại: unique `(Nhom, Ma)`, dùng hằng tên `Nhom`, lưu mã string + validate `(Nhom, Ma)` ở tầng app (không đặt FK thật từ từng cột vì FK không ràng buộc được đúng `Nhom`).
3. 🆕 **`GoiSipLog`** (khuyến nghị bật): `Objid`, `SoThuTuSip`, tên file ZIP, thời gian, kết quả — truy vết & dựng lại tên ZIP.

## 6. 🆕 Cấu hình EF Core cần khai báo (OnModelCreating)
> Codebase hiện **không** dùng `IEntityTypeConfiguration`/annotation; cấu hình duy nhất là default value cho `Deleted`/`CreatedDate` trong `SipDbContext`. Entity mới phải tự khai báo các điểm sau, nếu không EF bỏ qua.

- **FK thật + index** (nếu chỉ có cột `HoSoId` mà không cấu hình, EF coi là cột int rời, KHÔNG tạo khoá ngoại):
  - `HoSo (1) — (N) TaiLieu` qua `HasOne(t => t.HoSo).WithMany(h => h.TaiLieus).HasForeignKey(t => t.HoSoId)`.
  - `TaiLieu` tự tham chiếu: `HasOne(t => t.TaiLieuChinh).WithMany(t => t.TaiLieuDinhKems).HasForeignKey(t => t.TaiLieuChinhId)` — **`OnDelete(Restrict)`** để tránh cascade vòng.
- **Unique index**: `HoSo.Objid`, `HoSo.FileCode`, `TaiLieu (HoSoId, DocCode)`, `DanhMuc (Nhom, Ma)`.
- **Index thường**: `TaiLieu.HoSoId`, `TaiLieu.TaiLieuChinhId`.
- **Default value + soft-delete** cho cả 3 entity mới: `Deleted = false`, `CreatedDate = CURRENT_TIMESTAMP` (lặp như `ConfigHoSo/ConfigVanBan` hiện có).
- **Độ dài cột** (TT05): với SQLite (provider hiện tại) không bắt buộc, nhưng nên `HasMaxLength` để khớp chuẩn & sẵn sàng khi đổi SQL Server: `fileCode 100`, `title 1000`, `description(gói) 2000`, `docId 25`, `docCode 100`, `subject 500`, `autograph 2000`, `codeNumber 11`, `codeNotation 30`, `inforSign 30`, `keyword 100`... (theo bảng mục 3 của `chuan-dong-goi-sip-tt05.md`).
- **Enum/hằng**: `LoaiMetadata` (1/2/3), `RecordStatus` (NEW/...) → đặt hằng trong `Sip.be.Shared/Constants` (theo tiền lệ `ConfigConstants`/`CheckConstants`).

## 7. 🆕 Ràng buộc nghiệp vụ cần enforce (tầng app — SQLite khó CHECK phức tạp)
- `RiskRecoveryStatus` **bắt buộc** khi `RiskRecovery = true`.
- `Process` **bắt buộc** với `ConfidenceLevel ∈ {01, 03}` (tài liệu điện tử xử lý trên Hệ thống).
- `Maintenance` chỉ nhận `01` (Vĩnh viễn) với **nguồn nộp lưu/sưu tầm** → cần cờ phân loại nguồn để kiểm. *(Nếu mọi gói đều là nguồn nộp lưu thì hardcode quy tắc; nếu không, thêm cột phân loại nguồn vào `HoSo`.)*
- Văn bản phải **PDF/A hai lớp**: DB chỉ kiểm tới `MimeType=application/pdf`; thuộc tính "hai lớp" kiểm ở tầng convert/đóng gói.
- `FileCode`/`DocCode` đúng cấu trúc: mã định danh + năm + số lần nộp (2 ký tự) + STT (7 ký tự) → validate bằng regex; `HoSo` đã có `MaDinhDanh/NamHinhThanh/SoLanNop/SoThuTu` để dựng & đối chiếu `FileCode`.
- `TotalDoc` đối chiếu `COUNT(TaiLieu)` của gói khi validate.
- Mã danh mục (`TypeName/Language/Maintenance/Mode/ConfidenceLevel/TypePic/TypeMedia/Colour/RecordStatus...`) validate theo **tập giá trị** trong `DanhMuc (Nhom, Ma)`, không chỉ theo độ dài.

## 8. 🆕 Chốt theo GÓI THẬT (đã giải quyết mâu thuẫn của PDF)
> Đối chiếu PDF gốc (trang 239–291) **và** gói mẫu `uuid-0F203B84...` (schema + 137 file XML). Lấy **gói thật làm chuẩn thực thi**.
- **`riskRecovery`/`riskRecoveryStatus`**: PDF mâu thuẫn (bảng 3.x có, `.xsd` trong PDF không). **Gói thật: schema `ead.xsd`/`ead_doc.xsd`/`ead_pic.xsd`/`ead_media.xsd` ĐỀU CÓ** và 137 file XML đều ghi → **GIỮ 2 trường này** (đã có ở `TaiLieu` và bổ sung vào `HoSo`).
- **EAD hồ sơ KHÔNG có `source`** → **đã BỎ cột `Source`**. Bổ sung 13 trường hồ sơ thật (startDate/endDate/numberOfPaper/paperFileCode/maintenance/mode/language/format/confidenceLevel/inforSign/keyword/numberOfPage) vào `HoSo` (mục 3.2).
- **`docCode` vs `arcDocCode`**: **chốt dùng `docCode`** (137 file XML thật đều xuất `<docCode>`); generator sinh tag `<docCode>`.
- **Ngày EAD** (`startDate/endDate/issuedDate/photoTime/recordDate`): **lưu `string "DD/MM/YYYY"`** đúng như gói thật (chỉ `CreateDate/LastModDate/FileCreated` của METS là `DateTime` ISO).
- `docId` chỉ có ở `EAD_doc`, không có ở pic/media → `DocId` nullable.
- Mã danh mục: validate theo **tập mã `DanhMuc`** thay vì độ dài (độ dài TT05 rộng bất thường: `typeName` 10, `maintenance` 100 cho giá trị 2 ký tự).
- **AttachmentFile**: gói mẫu KHÔNG dùng (mọi tài liệu độc lập), nhưng TT05 có → vẫn giữ `TaiLieuChinhId` để khi gặp gói có đính kèm còn mô tả được.

> Constants cố định của chuẩn (PROFILE, namespace, tên thư mục, SHA-256, OAISPACKAGETYPE=SIP, agent phần mềm) để ở `Sip.be.Shared/Constants`, không đưa vào DB.

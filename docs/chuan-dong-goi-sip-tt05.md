# Chuẩn đóng gói SIP_tailieu (Phụ lục IV – Thông tư 05/2025/TT-BNV)

> Tài liệu tổng hợp từ Phụ lục IV "CẤU TRÚC DỮ LIỆU TÀI LIỆU NỘP (SUBMISSION INFORMATION PACKAGE – SIP_tailieu)", trang 239–291. Dùng làm chuẩn để kiểm tra & đóng gói SIP. Mọi giá trị/tên trường giữ nguyên theo văn bản gốc.

## 0. Tổng quan
- SIP_tailieu = gói thông tin nộp lưu, lưu dưới dạng **cấu trúc thư mục**, khi truyền nhận/lưu trữ thì **nén thành file ZIP**.
- Xây dựng mở rộng từ chuẩn **E-ARK (CSIP) v2.0.4**; tách rõ tệp dữ liệu đặc tả (metadata) và tệp văn bản/tài liệu.
- Một gói gồm: gói gốc (root) + **bản đại diện** `representations/rep1` (chứa dữ liệu thực `data/` và metadata từng tài liệu).

---

## 1. Cấu trúc thư mục vật lý

### A. Gói thông tin SIP_tailieu (root)
| TT | Tên | Định dạng | Yêu cầu | Số lượng | Mục đích |
|----|-----|-----------|---------|----------|----------|
| 1 | `SIP_tailieu/` | Thư mục | Bắt buộc | 01 | Tên thư mục gói = ID định danh gói SIP (theo `OBJID` của phần tử gốc METS.xml) |
| 2 | `METS.xml` | Tệp | Bắt buộc | 01 | File METS mô tả cấu trúc gói SIP_tailieu |
| 3 | `metadata/` | Thư mục | Bắt buộc | 01 | Chứa dữ liệu đặc tả mô tả |
| 4 | `metadata/descriptive/` | Thư mục | Bắt buộc | 01 | Chứa thông tin mô tả về tài liệu/dữ liệu đóng gói |
| 5 | `metadata/descriptive/EAD.xml` | Tệp | Bắt buộc | 01 | Mô tả thông tin chung của gói (theo EAD.xsd) |
| 6 | `representations/` | Thư mục | Bắt buộc | 01 | Chứa đại diện của gói |
| 7 | `representations/rep1/` | Thư mục | Bắt buộc | 01 | Bản đại diện của gói tài liệu |
| 8 | `schemas/` | Thư mục | Bắt buộc | 01 | Chứa file mô tả cấu trúc METS / metadata |
| 9 | `schemas/METS.xsd` | Tệp | Bắt buộc | 01 | Cấu trúc file METS của gói tin |
| 10 | `schemas/EAD.xsd, EAD_doc.xsd, EAD_media.xsd, EAD_pic.xsd` | Tệp | Bắt buộc | Tối thiểu 01 | Cấu trúc metadata: gói→EAD.xsd, văn bản→EAD_doc.xsd, video/âm thanh→EAD_media.xsd, phim âm bản/ảnh→EAD_pic.xsd |
| 11 | `documentation/` | Thư mục | Không bắt buộc | | Mô tả bổ sung về dữ liệu trong gói |
| 12 | `documentation/UserManual.pdf` | Tệp | Không bắt buộc | | File mô tả bổ sung |

### B. Gói đại diện `representations/rep1`
| TT | Tên | Định dạng | Yêu cầu | Số lượng | Mục đích |
|----|-----|-----------|---------|----------|----------|
| 1 | `rep1/` | Thư mục | Bắt buộc | 01 | Tên bản đại diện của SIP_tailieu |
| 2 | `METS.xml` | Tệp | Bắt buộc | 01 | METS đại diện cấu trúc của bản thể hiện |
| 3 | `metadata/` | Thư mục | Bắt buộc | 01 | Metadata mô tả/bảo quản của tài liệu |
| 4 | `metadata/descriptive/` | Thư mục | Bắt buộc | 01 | Thông tin mô tả về tài liệu |
| 5 | `metadata/descriptive/EAD_doc_File1.xml … EAD_media_File2.xml` | Tệp | Bắt buộc | Tối thiểu 01 | Mô tả thông tin từng tài liệu. Quy tắc tên: `[Tiêu chuẩn]_[Loại tài liệu]_[Tên file]` |
| 6 | `data/` | Thư mục | Bắt buộc | 01 | Chứa các tài liệu thực trong SIP |
| 7 | `data/File1.doc … Filen.mp3` | Tệp | Bắt buộc | Tối thiểu 01 | Mỗi file = 1 tài liệu. **Văn bản trong SIP phải là PDF/A, hai lớp** |

```
SIP_tailieu/                         (tên = OBJID gói)
├── METS.xml
├── metadata/
│   └── descriptive/
│       └── EAD.xml                  (mô tả chung cả gói)
├── representations/
│   └── rep1/
│       ├── METS.xml
│       ├── metadata/
│       │   └── descriptive/
│       │       └── EAD_doc_*.xml … EAD_media_*.xml   (mỗi tài liệu 1 file)
│       └── data/
│           └── File1.pdf … Filen.mp3                 (tài liệu thực)
├── schemas/
│   ├── METS.xsd
│   ├── EAD.xsd, EAD_doc.xsd, EAD_media.xsd, EAD_pic.xsd
│   └── (xml.xsd, xlink.xsd, DILCISExtensionMETS.xsd, DILCISExtensionSIPMETS.xsd)
└── documentation/                   (không bắt buộc)
    └── UserManual.pdf
```

---

## 2. METS.xml

### 2.1. Cấu trúc tổng quát
Gồm 5 phần tử: `mets` (gốc) → `metsHdr` (tiêu đề) → `dmdSec` (metadata mô tả) → `fileSec` (danh mục tệp) → `structMap` (bản đồ cấu trúc).

Namespace/PROFILE chuẩn:
```xml
<mets xmlns="http://www.loc.gov/METS/"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      xmlns:xlink="http://www.w3.org/1999/xlink"
      xmlns:csip="https://DILCIS.eu/XML/METS/CSIPExtensionMETS"
      xmlns:sip="https://DILCIS.eu/XML/METS/SIPExtensionMETS"
      OBJID="uuid-…" LABEL="…" TYPE="Collection"
      csip:CONTENTINFORMATIONTYPE="MIXED"
      PROFILE="https://earkcsip.dilcis.eu/profile/E-ARK-CSIP.xml"
      xsi:schemaLocation="…">
```
> Quy tắc ID dùng chung: `uuid-{UUIDs}` ( {UUIDs} tự sinh, **viết hoa** ). Riêng `<file>` dùng `ID-{UUIDs}`.
> Kiểu datetime: `YYYY-MM-DDThh:mm:ss.sTZD`. Hàm băm mặc định: **SHA-256**.

### 2.2. METS gói gốc (root)

**Phần tử `mets`**
| Thuộc tính | Yêu cầu | Mô tả |
|---|---|---|
| `OBJID` | Bắt buộc | ID định danh gói, `uuid-{UUIDs}`. VD `uuid-7D0D1987-0F1C-47A7-8FD6-CC5C7DE4064F` |
| `LABEL` | Không | Mô tả nội dung gói (lấy theo metadata `title`) |
| `TYPE` | Bắt buộc | Mặc định `Collection` |
| `csip:CONTENTINFORMATIONTYPE` | Không (bắt buộc ở METS thư mục representations) | Loại thông số kỹ thuật, VD `MIXED` |
| `PROFILE` | Bắt buộc | Đường dẫn profile gói tuân theo |

**Phần tử `metsHdr`**
| Thẻ/Thuộc tính | Yêu cầu | Mô tả |
|---|---|---|
| `CREATEDATE` | Bắt buộc | Thời gian tạo gói (datetime) |
| `LASTMODDATE` | Không | Thời gian cập nhật |
| `RECORDSTATUS` | Không | `NEW`(mặc định)/`SUPPLEMENT`/`REPLACEMENT`/`TEST`/`OTHER` |
| `csip:OAISPACKAGETYPE` | Bắt buộc | `SIP`(mặc định)/`AIP`/`DIP` |
| `agent` | Bắt buộc | Tác nhân tạo gói |
| `agent/@ROLE` | Bắt buộc | Mặc định `CREATOR` |
| `agent/@TYPE` | Bắt buộc | VD `INDIVIDUAL`/`ORGANIZATION`/`OTHER` |
| `agent/@OTHERTYPE` | Bắt buộc | VD `SOFTWARE` |
| `agent/name` | Bắt buộc | Tên công cụ/phần mềm tạo gói |
| `agent/note` | Bắt buộc | Ghi chú bổ sung |
| `note/@csip:NOTETYPE` | Bắt buộc | Mặc định `SOFTWARE VERSION` |
| `altRecordID` | Bắt buộc | Tham chiếu yêu cầu nộp |
| `altRecordID/@TYPE` | Bắt buộc | Mặc định `SUBMISSIONAGREEMENT`; giá trị = mã đăng ký yêu cầu nộp |

**Phần tử `dmdSec`** (mô tả file metadata trong `metadata/`)
| Thẻ/Thuộc tính | Yêu cầu | Mô tả |
|---|---|---|
| `dmdSec/@ID` | Bắt buộc | `uuid-{UUIDs}` |
| `dmdSec/@CREATED` | Bắt buộc | datetime |
| `dmdSec/@STATUS` | Không | VD `CURRENT` |
| `mdRef/@ID` | Bắt buộc | `uuid-{UUIDs}` |
| `mdRef/@LOCTYPE` | Bắt buộc | Luôn `URL` |
| `mdRef/@MDTYPE` | Bắt buộc | VD `OTHER` (gói), `DC` (tài liệu) |
| `mdRef/@MDTYPEVERSION` | Không | Mặc định `1.0` |
| `mdRef/@xlink:href` | Bắt buộc | Đường dẫn file metadata từ thư mục gốc |
| `mdRef/@MIMETYPE` | Bắt buộc | VD `text/xml` |
| `mdRef/@SIZE` | Bắt buộc | Dung lượng (bytes) |
| `mdRef/@CREATED` | Bắt buộc | datetime |
| `mdRef/@CHECKSUMTYPE` | Bắt buộc | Mặc định `SHA-256` |
| `mdRef/@CHECKSUM` | Bắt buộc | Giá trị băm |

**Phần tử `fileSec`**
| Thẻ/Thuộc tính | Yêu cầu | Mô tả |
|---|---|---|
| `fileSec/@ID` | Bắt buộc | `uuid-{UUIDs}` |
| `fileGrp` | Bắt buộc | Nhóm tệp cùng nội dung |
| `fileGrp/@USE` | Bắt buộc | Tên nhóm/đường dẫn: `Schemas`, `Documentation`, `Representations/rep1`… |
| `fileGrp/@ID` | Bắt buộc | `uuid-{UUIDs}` |
| `file/@ID` | Bắt buộc | `ID-{UUIDs}` |
| `file/@MIMETYPE` | Bắt buộc | Theo danh sách mimetype (mục 6) |
| `file/@CREATED` | Bắt buộc | datetime |
| `file/@CHECKSUMTYPE` | Bắt buộc | Mặc định `SHA-256` |
| `file/@CHECKSUM` | Bắt buộc | Giá trị băm |
| `file/Flocat/@xlink:type` | Bắt buộc | Mặc định `simple` |
| `file/Flocat/@xlink:href` | Bắt buộc | Vị trí file |
| `file/Flocat/@LOCTYPE` | Bắt buộc | Luôn `URL` |

**Phần tử `structMap`** (root) — `LABEL="CSIP"`, `TYPE="PHYSICAL"`
- `div` gốc `@LABEL` = ID của METS gói hồ sơ (uuid).
- `div[@LABEL="Metadata"]` → `@DMDID` trỏ tới `dmdSec`.
- `div[@LABEL="Schemas"]/fptr@FILEID` → `fileGrp USE="Schemas"`.
- `div[@LABEL="Representations/rep1"]/mptr@xlink:href` → `representations/rep1/METS.xml`; `@xlink:title` = ID fileGrp `USE="Representations/rep1"`.
- `div[@LABEL="Documentation"]/fptr@FILEID` (không bắt buộc) → fileGrp `USE="Documentation"`.

### 2.3. METS bản đại diện (`representations/rep1/METS.xml`)
Tương tự root, khác biệt chính:
- `mets`: `OBJID` riêng của bản đại diện; `csip:CONTENTINFORMATIONTYPE` **bắt buộc** ở đây; schemaLocation dùng đường dẫn tương đối `../../schemas/…`.
- `metsHdr/agent`: thường `TYPE="ORGANIZATION"`; `note/@csip:NOTETYPE="IDENTIFICATIONCODE"`, giá trị = **Mã phông** (VD `Phong_BNV`).
- `structMap` (`LABEL="CSIP"`):
  - `div[@LABEL="rep1"]` (`TYPE="ORIGINAL"`).
  - `div[@LABEL="Data"]/fptr@FILEID` → fileGrp `USE="Data"`.
  - `div[@LABEL="AttachmentFile"]/div@LABEL` = ID `<file>` tài liệu; `…/div/fptr@FILEID` = ID `<file>` tài liệu **đính kèm**.
  - `div[@LABEL="MetadataLink"]/div[@LABEL="MetadataLink/File"]`: `@DMDID` = ID `dmdSec` của tài liệu; `fptr@FILEID` = ID file tài liệu tương ứng (liên kết metadata ↔ tài liệu trong `data/`).

---

## 3. Metadata mô tả (EAD)

### 3.1. Gói SIP_tailieu — `EAD.xml` (root `<simpledc>`)
| Trường | Tên TV | Kiểu | Độ dài | Mô tả / Quy định |
|---|---|---|---|---|
| `fileCode` | Mã gói tin SIP_tailieu | String | 100 | = Mã định danh cơ quan/tổ chức/cá nhân + Năm hình thành + Số lần nộp lưu (2 ký tự, vd `01`) + Số thứ tự tài liệu trong lần nộp (7 ký tự, vd `0000001`) |
| `title` | Tiêu đề gói tin | String | 1000 | Tóm tắt nội dung & thời gian tài liệu trong gói |
| `source` | Nguồn gốc | Boolean | 1 | `0` = văn bản đi; `1` = văn bản đến |
| `totalDoc` | Tổng số tài liệu | Number | 10 | Tổng số tài liệu (văn bản, kỹ thuật, âm bản/ảnh, ghi âm/phim) |
| `description` | Ghi chú | String | 2000 | Tên người lập hồ sơ & nội dung cần làm rõ |

### 3.2. Tài liệu văn bản — `EAD_doc.xml`
| Trường | Tên TV | Kiểu | Độ dài | Mô tả / Giá trị |
|---|---|---|---|---|
| `docId` | Mã định danh tài liệu | String | 25 | Mã định danh tài liệu |
| `docCode` | Mã lưu trữ của tài liệu | String | 100 | Cấu trúc như `fileCode` (mã định danh + năm + số lần nộp 2 ký tự + STT 7 ký tự) |
| `maintenance` | Thời hạn lưu trữ | String | 100 | `01` Vĩnh viễn, `02` 70 năm, `03` 50 năm, `04` 30 năm, `05` 20 năm, `06` 10 năm, `07` Khác (nguồn nộp/sưu tầm chỉ nhận `01`) |
| `typeName` | Tên loại tài liệu | String | 10 | `01` Nghị quyết … `22` Công văn … `30` Bản đồ, `31` Bản vẽ kỹ thuật, `32` Khác (xem danh mục đầy đủ ở mục 7) |
| `codeNumber` | Số của tài liệu (nếu có) | String | 11 | |
| `codeNotation` | Ký hiệu tài liệu (nếu có) | String | 30 | |
| `issuedDate` | Ngày tháng năm tài liệu | Date | DD/MM/YYYY | |
| `organName` | Cơ quan/tổ chức/cá nhân ban hành | String | 200 | |
| `subject` | Trích yếu nội dung | String | 500 | |
| `language` | Ngôn ngữ | String | 100 | `01` Việt … `11` Khác (chọn 1 hoặc nhiều – xem mục 7) |
| `numberOfPage` | Số lượng trang | Number | 4 | |
| `inforSign` | Ký hiệu thông tin (nếu có) | String | 30 | |
| `keyword` | Từ khóa (nếu có) | String | 100 | |
| `mode` | Chế độ sử dụng | String | 20 | `01` Công khai, `02` Sử dụng có điều kiện, `03` Mật |
| `confidenceLevel` | Mức độ tin cậy | String | 30 | `01` Gốc điện tử, `02` Số hóa, `03` Hỗn hợp |
| `autograph` | Bút tích (nếu có) | String | 2000 | |
| `format` | Tình trạng vật lý (nếu có) | String | 50 | |
| `process` | Quy trình xử lý (nếu có) | Boolean | 1 | `0` không có / `1` có; bắt buộc với tài liệu điện tử xử lý trên Hệ thống (áp dụng `confidenceLevel` 01, 03) |
| `riskRecovery` | Chế độ dự phòng | Boolean | 1 | `1` Có / `0` Không |
| `riskRecoveryStatus` | Tình trạng dự phòng | String | 2 | `01` Đã dự phòng / `02` Chưa dự phòng (bắt buộc khi `riskRecovery=1`) |
| `description` | Ghi chú | String | 500 | |

### 3.3. Tài liệu phim âm bản/ảnh — `EAD_pic.xml`
| Trường | Tên TV | Kiểu | Độ dài | Giá trị |
|---|---|---|---|---|
| `docCode` | Mã lưu trữ tài liệu | String | 100 | như mục 3.2 |
| `maintenance` | Thời hạn lưu trữ | String | 100 | `01`–`07` (như 3.2) |
| `typePic` | Phân loại | String | 2 | `01` phim âm bản, `02` ảnh |
| `archivesNumber` | Số lưu trữ (đặc thù) | String | 50 | |
| `inforSign` | Ký hiệu thông tin | String | 30 | |
| `eventName` | Tên sự kiện | String | 500 | |
| `imageTitle` | Tiêu đề phim/ảnh | String | 500 | |
| `photographer` | Tác giả | String | 300 | |
| `photoPlace` | Địa điểm chụp | String | 300 | |
| `photoTime` | Thời gian chụp | Date | DD/MM/YYYY | |
| `colour` | Màu sắc | String | 50 | `01` Màu, `02` Đen trắng |
| `filmSize` | Cỡ phim/ảnh | String | 30 | |
| `docAttached` | Tài liệu đi kèm | Boolean | 1 | `0` Không / `1` Có |
| `mode` | Chế độ sử dụng | String | 20 | `01`/`02`/`03` (như 3.2) |
| `format` | Tình trạng vật lý | String | 50 | |
| `riskRecovery` | Chế độ dự phòng | Boolean | 1 | `1`/`0` |
| `riskRecoveryStatus` | Tình trạng dự phòng | String | 2 | `01`/`02` |
| `description` | Ghi chú | String | 500 | |

### 3.4. Tài liệu phim/âm thanh (ghi hình/ghi âm) — `EAD_media.xml`
| Trường | Tên TV | Kiểu | Độ dài | Giá trị |
|---|---|---|---|---|
| `docCode` | Mã lưu trữ tài liệu | String | 100 | như 3.2 |
| `maintenance` | Thời hạn lưu trữ | String | 100 | `01`–`07` |
| `typeMedia` | Phân loại | String | 2 | `01` ghi âm, `02` ghi hình |
| `archivesNumber` | Số lưu trữ (đặc thù) | String | 50 | |
| `inforSign` | Ký hiệu thông tin | String | 30 | |
| `eventName` | Tên sự kiện | String | 500 | |
| `movieTitle` | Tiêu đề phim/âm thanh | String | 500 | |
| `recorder` | Tác giả | String | 300 | |
| `recordPlace` | Địa điểm | String | 300 | |
| `recordDate` | Thời gian | Date | DD/MM/YYYY | |
| `language` | Ngôn ngữ | String | 100 | `01`–`11` (chọn 1 hoặc nhiều) |
| `playTime` | Thời lượng | String | 8 | |
| `docAttached` | Tài liệu đi kèm | Boolean | 1 | `0`/`1` |
| `mode` | Chế độ sử dụng | String | 20 | `01`/`02`/`03` |
| `quality` | Chất lượng | String | 50 | bình thường, mờ, lẫn tạp âm… |
| `format` | Tình trạng vật lý | String | 50 | |
| `riskRecovery` | Chế độ dự phòng | Boolean | 1 | `1`/`0` |
| `riskRecoveryStatus` | Tình trạng dự phòng | String | 2 | `01`/`02` |
| `description` | Ghi chú | String | 500 | |

---

## 4. Schema (.xsd)
Tất cả schema có gốc `<simpledc>` (complexType `elementContainer`, group `elementsGroup`), import `xml.xsd`.
- **EAD.xsd** (gói): `fileCode, title, source, totalDoc, description`.
- **EAD_doc.xsd** (văn bản): `docId, docCode, maintenance, typeName, codeNumber, codeNotation, issuedDate, organName, subject, language, numberOfPage, inforSign, keyword, mode, confidenceLevel, autograph, format, process, description`.
- **EAD_pic.xsd** (phim âm bản/ảnh): `docCode, maintenance, typePic, archivesNumber, inforSign, eventName, imageTitle, photographer, photoPlace, photoTime, colour, filmSize, mode, format, description, docAttached`.
- **EAD_media.xsd** (phim/âm thanh): `docCode, maintenance, typeMedia, archivesNumber, inforSign, eventName, movieTitle, recorder, recordPlace, recordDate, language, playTime, mode, quality, format, description, docAttached`.

---

## 5. Danh sách mimetype & extension
| Code | Loại | Extension → mimetype |
|---|---|---|
| DOC | Văn bản | `.txt`→text/plain · `.rtf`(v1.8/1.9.1)→application/rtf · `.docx`→…wordprocessingml.document · **`.pdf/a` (hai lớp)**→application/pdf · `.doc`→application/msword · `.odt`(v1.2)→…opendocument.text |
| OTHER | Bảng tính | `.csv`→text/csv · `.xlsx`→…spreadsheetml.sheet · `.xls`→application/vnd.ms-excel · `.ods`(v1.2)→…opendocument.spreadsheet |
| OTHER | Trình diễn | `.htm`→text/html · `.pptx`→…presentationml.presentation · `.ppt`→application/vnd.ms-powerpoint · `.odp`(v1.2)→…opendocument.presentation |
| PIC | Ảnh | `.jpeg/.jpg`→image/jpeg · `.gif`(89a)→image/gif · `.tif/.tiff`→image/tiff · `.png`→image/png |
| MEDIA | Video | MPEG-1/2/4→video/mpeg · `.avi`→video/x-msvideo · `.wmv`→video/x-ms-wmv · `.mov/.qt`→video/quicktime |
| MEDIA | Âm thanh | `.mp3`→audio/mpeg · `.wma`→audio/x-ms-wma · `.aac`→audio/aac |

> Định dạng tài liệu trong gói: văn bản/tài liệu kỹ thuật = **PDF/A hai lớp**; âm bản/ảnh = JPEG; âm thanh/video = MPEG-4, MP3, avi, wma, wmv.

---

## 6. Quy tắc đặt tên file nén ZIP
`Tên ZIP = [Số thứ tự SIP_tailieu trong lần nộp] + [ID gói SIP_tailieu]`
- Số thứ tự: người dùng tự đánh (số Ả-rập).
- ID gói: lấy theo `OBJID` của phần tử gốc METS.xml.

---

## 7. Phụ lục giá trị mã

**`typeName` – Tên loại tài liệu:** 01 Nghị quyết · 02 Quyết định · 03 Chỉ thị · 04 Quy chế · 05 Quy định · 06 Thông cáo · 07 Thông báo · 08 Hướng dẫn · 09 Chương trình · 10 Kế hoạch · 11 Phương án · 12 Đề án · 13 Dự án · 14 Báo cáo · 15 Tờ trình · 16 Giấy ủy quyền · 17 Phiếu gửi · 18 Phiếu chuyển · 19 Phiếu báo · 20 Biên bản · 21 Hợp đồng · 22 Công văn · 23 Công điện · 24 Bản ghi nhớ · 25 Bản thỏa thuận · 26 Giấy mời · 27 Giấy giới thiệu · 28 Giấy nghỉ phép · 29 Thư công · 30 Bản đồ · 31 Bản vẽ kỹ thuật · 32 Khác.

**`language` – Ngôn ngữ:** 01 Tiếng Việt · 02 Tiếng Anh · 03 Tiếng Pháp · 04 Tiếng Nga · 05 Tiếng Trung · 06 Việt Anh · 07 Việt Nga · 08 Việt Pháp · 09 Hán Nôm · 10 Việt Trung · 11 Khác.

**`maintenance` – Thời hạn lưu trữ:** 01 Vĩnh viễn · 02 70 năm · 03 50 năm · 04 30 năm · 05 20 năm · 06 10 năm · 07 Khác.

**`mode` – Chế độ sử dụng:** 01 Công khai · 02 Sử dụng có điều kiện · 03 Mật.

**`confidenceLevel` – Mức độ tin cậy:** 01 Gốc điện tử · 02 Số hóa · 03 Hỗn hợp.

**`RECORDSTATUS`:** NEW · SUPPLEMENT · REPLACEMENT · TEST · OTHER. **`csip:OAISPACKAGETYPE`:** SIP · AIP · DIP.

> Nguồn: Phụ lục IV, Thông tư 05/2025/TT-BNV (trang 239–291).

---

## 8. Đối chiếu GÓI MẪU THỰC TẾ (`uuid-0F203B84-6641-4CDA-9F52-DAAE55FEF6B1`)

> Đã giải nén & đọc 1 gói SIP thật trong repo (137 tài liệu PDF, đều là văn bản EAD_DOC). **Phần mềm tạo gói: gói mẫu ghi `ArchiveNex`, nhưng hệ thống này dùng tên `SipPack`** (đặt vào `agent/name`). Thực tế **lệch khá nhiều** so với bảng đặc tả mục 1–7 ở trên. Khi xây generator/validate, **bám theo cách làm thực tế + schema trong `schemas/` của gói**, không bám cứng bảng TT05.

### 8.1. Cấu trúc thư mục thực tế
```
uuid-0F203B84.../
├── METS.xml
├── metadata/descriptive/EAD.xml
├── schemas/  (ead.xsd, ead_doc.xsd, ead_media.xsd, ead_pic.xsd,
│              mets1_12.xsd, xlink.xsd, DILCISExtensionMETS.xsd, DILCISExtensionSIPMETS.xsd)
└── representations/rep1/
    ├── METS.xml
    ├── metadata/descriptive/EAD_DOC_<docId>.xml   (137 file, 1/tài liệu)
    └── data/<docId>.pdf                           (137 file)
```
- **Không có** `documentation/`. Tên file metadata thực tế: `EAD_DOC_H49.64.33.2001.160.1.xml` (chữ HOA `EAD_DOC_`), tên data = `H49.64.33.2001.160.1.pdf`.

### 8.2. EAD.xml (hồ sơ) THỰC TẾ ≠ bảng 3.1
Theo `schemas/ead.xsd`, EAD hồ sơ có **18 trường** (`xs:all`), **KHÔNG có `source`**, và có nhiều trường cấp hồ sơ mà bảng 3.1 không nêu:
`fileCode, title, maintenance, mode, language, startDate, endDate, keyword, totalDoc, numberOfPaper, numberOfPage, format, inforSign, confidenceLevel, paperFileCode, riskRecovery, riskRecoveryStatus, description`
- Bổ sung so với 3.1: `maintenance, mode, language, startDate, endDate, keyword, numberOfPaper, numberOfPage, format, inforSign, confidenceLevel, paperFileCode, riskRecovery, riskRecoveryStatus`.
- **Bỏ** so với 3.1: `source` (không xuất hiện trong gói thật).

### 8.3. EAD_doc/pic/media THỰC TẾ
- `ead_doc.xsd` có **21 trường** và **CÓ** `riskRecovery`/`riskRecoveryStatus` (giải quyết mâu thuẫn ở mục 3.2 vs schema TT05 — thực tế GIỮ 2 trường này).
- ⚠️ **`docCode` vs `arcDocCode`**: schema trong gói khai `arcDocCode`, nhưng **cả 137 file XML thực tế lại ghi `<docCode>`** → bản thân gói mẫu schema và dữ liệu KHÔNG khớp. Generator hiện hành dùng `docCode`.
- `ead_pic.xsd`/`ead_media.xsd` thực tế dùng `arcDocCode`, đều có `riskRecovery`/`riskRecoveryStatus`.

### 8.4. METS THỰC TẾ ≠ mô tả mục 2
| Điểm | TT05 (mục 2) | Gói thực tế |
|---|---|---|
| `mets/@TYPE` | `Collection` | **`Mixed`** |
| `agent` (root) | ROLE=CREATOR, TYPE=OTHER/SOFTWARE, note `SOFTWARE VERSION` | ROLE=CREATOR TYPE=OTHER OTHERTYPE=SOFTWARE, name=**`SipPack`** (gói mẫu là `ArchiveNex`), note **`IDENTIFICATIONCODE`=P_KHCN** (mã phông ngay ở root, **không** có note SOFTWARE VERSION) |
| `agent` (rep1) | TYPE=ORGANIZATION, IDENTIFICATIONCODE=mã phông | TYPE=**INDIVIDUAL**, name=`cvlt`, note IDENTIFICATIONCODE=P_KHCN |
| `amdSec` | không nêu | **CÓ** phần tử `<amdSec>` (rỗng) ở root |
| `dmdSec/mdRef` | MDTYPE=OTHER, MIMETYPE=text/xml | MDTYPE=**DC**, MDTYPEVERSION=**1.0.0**, MIMETYPE=**application/xml** |
| `altRecordID` | mã đăng ký nộp | = `fileCode` (`H49.64.33.2001.160`) |
| rep1 `mets/@OBJID` | uuid riêng | **`rep1`** (chuỗi cố định, không phải uuid) |
| `fileGrp/@USE` | Schemas, Documentation, Representations/rep1, Data | root: `Schemas` + `Representations/rep1`; rep1: chỉ `Data` |
| `<file>` schema | MIMETYPE theo danh mục | dùng `application/octet-stream` cho .xsd; `application/pdf` cho data |
| structMap rep1 | Data, AttachmentFile, MetadataLink | `rep1`(ORIGINAL) → `MetadataLink` (137× `MetadataLink/File`: `@DMDID`=dmdSec, `fptr@FILEID`=ID `<file>` data) + `Data` (fptr→ID fileGrp Data). **Không dùng `AttachmentFile`** trong gói mẫu (mọi tài liệu độc lập). |

### 8.5. Cấu trúc mã thực tế (≠ quy tắc fileCode mục 3.1)
- `fileCode` thật = `H49.64.33.2001.160` — **không** theo công thức "mã định danh + năm + số lần nộp(2) + STT(7)". Dạng: `H49.64.33` (cơ quan) `.2001` (năm) `.160` (số hồ sơ/mục lục).
- `docId` = `H49.64.33.2001.160.1` (= fileCode + STT tài liệu).
- `docCode` = `H49.64.33.1994.160.0000001` (có đuôi `0000001`). → **Không nên ràng buộc cứng** cấu trúc 2/7 ký tự; lưu string, validate mềm.
- **Tên file ZIP thực tế** = `b6acafed-a809-4b78-ae9f-0373d50d52f0.zip` — **1 GUID viết thường**, KHÔNG có tiền tố `uuid-`, **KHÔNG có STT**, và **khác** cả OBJID gói (`uuid-0F203B84-...`). Mâu thuẫn với mục 6 (TT05 quy định `[STT] + [OBJID]`). → **Theo gói thật: đặt tên ZIP = GUID thường, bỏ STT**; thư mục gói (tên = OBJID) nằm BÊN TRONG zip (giải nén ra mới thấy). `PackService` sinh đúng vậy và chỉ giữ file ZIP (xoá thư mục đã giải nén).

> Kết luận: gói thật theo một biến thể schema riêng (ead.xsd hồ sơ giàu trường, không có source; doc/pic/media giữ riskRecovery; METS dùng TYPE=Mixed, OBJID rep1, có amdSec, MDTYPE=DC). Thiết kế DB phải bám biến thể này — xem `thiet-ke-db-dong-goi-sip.md` mục cập nhật.

# SIP_tailieu packaging standard (Appendix IV – Circular 05/2025/TT-BNV)

> Compiled from Appendix IV "SUBMISSION INFORMATION PACKAGE DATA STRUCTURE (SIP_tailieu)", pages 239–291.
> Used as the reference for checking & packaging SIP. All values/field names are kept verbatim from the source text.

## 0. Overview
- SIP_tailieu = a submission information package, stored as a **folder structure**, and **zipped** when transferred/archived.
- Extended from the **E-ARK (CSIP) v2.0.4** standard; clearly separates metadata files from document/content files.
- A package contains: the root package + a **representation** `representations/rep1` (holding the real data `data/`
  and per-document metadata).

---

## 1. Physical folder structure

### A. SIP_tailieu package (root)
| # | Name | Format | Requirement | Count | Purpose |
|----|-----|-----------|---------|----------|----------|
| 1 | `SIP_tailieu/` | Folder | Required | 01 | Package folder name = package SIP identifier (per `OBJID` of the root METS.xml element) |
| 2 | `METS.xml` | File | Required | 01 | METS file describing the SIP_tailieu package structure |
| 3 | `metadata/` | Folder | Required | 01 | Holds descriptive metadata |
| 4 | `metadata/descriptive/` | Folder | Required | 01 | Holds descriptive info about the packaged documents/data |
| 5 | `metadata/descriptive/EAD.xml` | File | Required | 01 | Describes the package-level info (per EAD.xsd) |
| 6 | `representations/` | Folder | Required | 01 | Holds the package representations |
| 7 | `representations/rep1/` | Folder | Required | 01 | The document package representation |
| 8 | `schemas/` | Folder | Required | 01 | Holds METS / metadata schema files |
| 9 | `schemas/METS.xsd` | File | Required | 01 | METS structure of the package |
| 10 | `schemas/EAD.xsd, EAD_doc.xsd, EAD_media.xsd, EAD_pic.xsd` | File | At least 01 | Metadata structure: package→EAD.xsd, text→EAD_doc.xsd, video/audio→EAD_media.xsd, negative film/photo→EAD_pic.xsd |
| 11 | `documentation/` | Folder | Optional | | Extra description of the package data |
| 12 | `documentation/UserManual.pdf` | File | Optional | | Supplementary description file |

### B. Representation package `representations/rep1`
| # | Name | Format | Requirement | Count | Purpose |
|----|-----|-----------|---------|----------|----------|
| 1 | `rep1/` | Folder | Required | 01 | Name of the SIP_tailieu representation |
| 2 | `METS.xml` | File | Required | 01 | METS describing the representation structure |
| 3 | `metadata/` | Folder | Required | 01 | Descriptive/preservation metadata of the documents |
| 4 | `metadata/descriptive/` | Folder | Required | 01 | Descriptive info about the documents |
| 5 | `metadata/descriptive/EAD_doc_File1.xml … EAD_media_File2.xml` | File | At least 01 | Per-document metadata. Naming rule: `[Standard]_[DocType]_[FileName]` |
| 6 | `data/` | Folder | Required | 01 | Holds the actual documents in the SIP |
| 7 | `data/File1.doc … Filen.mp3` | File | At least 01 | One file = one document. **Text documents in the SIP must be two-layer PDF/A** |

```
SIP_tailieu/                         (name = package OBJID)
├── METS.xml
├── metadata/
│   └── descriptive/
│       └── EAD.xml                  (describes the whole package)
├── representations/
│   └── rep1/
│       ├── METS.xml
│       ├── metadata/
│       │   └── descriptive/
│       │       └── EAD_doc_*.xml … EAD_media_*.xml   (one file per document)
│       └── data/
│           └── File1.pdf … Filen.mp3                 (the actual documents)
├── schemas/
│   ├── METS.xsd
│   ├── EAD.xsd, EAD_doc.xsd, EAD_media.xsd, EAD_pic.xsd
│   └── (xml.xsd, xlink.xsd, DILCISExtensionMETS.xsd, DILCISExtensionSIPMETS.xsd)
└── documentation/                   (optional)
    └── UserManual.pdf
```

---

## 2. METS.xml

### 2.1. General structure
5 elements: `mets` (root) → `metsHdr` (header) → `dmdSec` (descriptive metadata) → `fileSec` (file inventory) → `structMap` (structure map).

Standard namespaces/PROFILE:
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
> Shared ID rule: `uuid-{UUIDs}` ({UUIDs} auto-generated, **uppercase**). `<file>` uses `ID-{UUIDs}` instead.
> Datetime type: `YYYY-MM-DDThh:mm:ss.sTZD`. Default hash: **SHA-256**.

### 2.2. Root package METS

**`mets` element**
| Attribute | Requirement | Description |
|---|---|---|
| `OBJID` | Required | Package identifier, `uuid-{UUIDs}`. E.g. `uuid-7D0D1987-0F1C-47A7-8FD6-CC5C7DE4064F` |
| `LABEL` | Optional | Package content description (taken from metadata `title`) |
| `TYPE` | Required | Default `Collection` |
| `csip:CONTENTINFORMATIONTYPE` | Optional (required in the representations directory METS) | Content spec type, e.g. `MIXED` |
| `PROFILE` | Required | Path to the profile the package conforms to |

**`metsHdr` element**
| Tag/Attribute | Requirement | Description |
|---|---|---|
| `CREATEDATE` | Required | Package creation time (datetime) |
| `LASTMODDATE` | Optional | Update time |
| `RECORDSTATUS` | Optional | `NEW`(default)/`SUPPLEMENT`/`REPLACEMENT`/`TEST`/`OTHER` |
| `csip:OAISPACKAGETYPE` | Required | `SIP`(default)/`AIP`/`DIP` |
| `agent` | Required | The package-creating agent |
| `agent/@ROLE` | Required | Default `CREATOR` |
| `agent/@TYPE` | Required | E.g. `INDIVIDUAL`/`ORGANIZATION`/`OTHER` |
| `agent/@OTHERTYPE` | Required | E.g. `SOFTWARE` |
| `agent/name` | Required | Name of the tool/software that created the package |
| `agent/note` | Required | Extra note |
| `note/@csip:NOTETYPE` | Required | Default `SOFTWARE VERSION` |
| `altRecordID` | Required | Reference to the submission request |
| `altRecordID/@TYPE` | Required | Default `SUBMISSIONAGREEMENT`; value = submission-request registration code |

**`dmdSec` element** (describes the metadata files under `metadata/`)
| Tag/Attribute | Requirement | Description |
|---|---|---|
| `dmdSec/@ID` | Required | `uuid-{UUIDs}` |
| `dmdSec/@CREATED` | Required | datetime |
| `dmdSec/@STATUS` | Optional | E.g. `CURRENT` |
| `mdRef/@ID` | Required | `uuid-{UUIDs}` |
| `mdRef/@LOCTYPE` | Required | Always `URL` |
| `mdRef/@MDTYPE` | Required | E.g. `OTHER` (package), `DC` (document) |
| `mdRef/@MDTYPEVERSION` | Optional | Default `1.0` |
| `mdRef/@xlink:href` | Required | Path to the metadata file from the root folder |
| `mdRef/@MIMETYPE` | Required | E.g. `text/xml` |
| `mdRef/@SIZE` | Required | Size (bytes) |
| `mdRef/@CREATED` | Required | datetime |
| `mdRef/@CHECKSUMTYPE` | Required | Default `SHA-256` |
| `mdRef/@CHECKSUM` | Required | Hash value |

**`fileSec` element**
| Tag/Attribute | Requirement | Description |
|---|---|---|
| `fileSec/@ID` | Required | `uuid-{UUIDs}` |
| `fileGrp` | Required | Group of files with the same content |
| `fileGrp/@USE` | Required | Group name/path: `Schemas`, `Documentation`, `Representations/rep1`… |
| `fileGrp/@ID` | Required | `uuid-{UUIDs}` |
| `file/@ID` | Required | `ID-{UUIDs}` |
| `file/@MIMETYPE` | Required | Per the mimetype list (section 6) |
| `file/@CREATED` | Required | datetime |
| `file/@CHECKSUMTYPE` | Required | Default `SHA-256` |
| `file/@CHECKSUM` | Required | Hash value |
| `file/Flocat/@xlink:type` | Required | Default `simple` |
| `file/Flocat/@xlink:href` | Required | File location |
| `file/Flocat/@LOCTYPE` | Required | Always `URL` |

**`structMap` element** (root) — `LABEL="CSIP"`, `TYPE="PHYSICAL"`
- Root `div@LABEL` = ID of the record-package METS (uuid).
- `div[@LABEL="Metadata"]` → `@DMDID` points to `dmdSec`.
- `div[@LABEL="Schemas"]/fptr@FILEID` → `fileGrp USE="Schemas"`.
- `div[@LABEL="Representations/rep1"]/mptr@xlink:href` → `representations/rep1/METS.xml`; `@xlink:title` = ID of `fileGrp USE="Representations/rep1"`.
- `div[@LABEL="Documentation"]/fptr@FILEID` (optional) → `fileGrp USE="Documentation"`.

### 2.3. Representation METS (`representations/rep1/METS.xml`)
Similar to root, main differences:
- `mets`: its own representation `OBJID`; `csip:CONTENTINFORMATIONTYPE` is **required** here; schemaLocation uses relative paths `../../schemas/…`.
- `metsHdr/agent`: usually `TYPE="ORGANIZATION"`; `note/@csip:NOTETYPE="IDENTIFICATIONCODE"`, value = **fonds code** (e.g. `Phong_BNV`).
- `structMap` (`LABEL="CSIP"`):
  - `div[@LABEL="rep1"]` (`TYPE="ORIGINAL"`).
  - `div[@LABEL="Data"]/fptr@FILEID` → `fileGrp USE="Data"`.
  - `div[@LABEL="AttachmentFile"]/div@LABEL` = ID of the document `<file>`; `…/div/fptr@FILEID` = ID of the **attached** document `<file>`.
  - `div[@LABEL="MetadataLink"]/div[@LABEL="MetadataLink/File"]`: `@DMDID` = document `dmdSec` ID; `fptr@FILEID` = corresponding document file ID (links metadata ↔ document in `data/`).

---

## 3. Descriptive metadata (EAD)

### 3.1. SIP_tailieu package — `EAD.xml` (root `<simpledc>`)
| Field | VN name | Type | Length | Description / Rule |
|---|---|---|---|---|
| `fileCode` | Mã gói tin SIP_tailieu | String | 100 | = Organization/individual identifier + year of creation + submission count (2 chars, e.g. `01`) + document sequence in this submission (7 chars, e.g. `0000001`) |
| `title` | Tiêu đề gói tin | String | 1000 | Summary of the content & time span of documents in the package |
| `source` | Nguồn gốc | Boolean | 1 | `0` = outgoing document; `1` = incoming document |
| `totalDoc` | Tổng số tài liệu | Number | 10 | Total number of documents (text, technical, negative/photo, audio/video) |
| `description` | Ghi chú | String | 2000 | Name of the file compiler & notes to clarify |

### 3.2. Text documents — `EAD_doc.xml`
| Field | VN name | Type | Length | Description / Value |
|---|---|---|---|---|
| `docId` | Mã định danh tài liệu | String | 25 | Document identifier |
| `docCode` | Mã lưu trữ của tài liệu | String | 100 | Same structure as `fileCode` (identifier + year + 2-char submission count + 7-char sequence) |
| `maintenance` | Thời hạn lưu trữ | String | 100 | `01` Permanent, `02` 70 years, `03` 50 years, `04` 30 years, `05` 20 years, `06` 10 years, `07` Other (submission/collection sources only accept `01`) |
| `typeName` | Tên loại tài liệu | String | 10 | `01` Nghị quyết … `22` Công văn … `30` Bản đồ, `31` Bản vẽ kỹ thuật, `32` Khác (see full list in section 7) |
| `codeNumber` | Số của tài liệu (nếu có) | String | 11 | |
| `codeNotation` | Ký hiệu tài liệu (nếu có) | String | 30 | |
| `issuedDate` | Ngày tháng năm tài liệu | Date | DD/MM/YYYY | |
| `organName` | Cơ quan/tổ chức/cá nhân ban hành | String | 200 | |
| `subject` | Trích yếu nội dung | String | 500 | |
| `language` | Ngôn ngữ | String | 100 | `01` Vietnamese … `11` Other (pick one or more – see section 7) |
| `numberOfPage` | Số lượng trang | Number | 4 | |
| `inforSign` | Ký hiệu thông tin (nếu có) | String | 30 | |
| `keyword` | Từ khóa (nếu có) | String | 100 | |
| `mode` | Chế độ sử dụng | String | 20 | `01` Công khai, `02` Sử dụng có điều kiện, `03` Mật |
| `confidenceLevel` | Mức độ tin cậy | String | 30 | `01` Gốc điện tử, `02` Số hóa, `03` Hỗn hợp |
| `autograph` | Bút tích (nếu có) | String | 2000 | |
| `format` | Tình trạng vật lý (nếu có) | String | 50 | |
| `process` | Quy trình xử lý (nếu có) | Boolean | 1 | `0` none / `1` yes; required for electronic documents processed on the System (applies to `confidenceLevel` 01, 03) |
| `riskRecovery` | Chế độ dự phòng | Boolean | 1 | `1` Yes / `0` No |
| `riskRecoveryStatus` | Tình trạng dự phòng | String | 2 | `01` Backed up / `02` Not backed up (required when `riskRecovery=1`) |
| `description` | Ghi chú | String | 500 | |

### 3.3. Negative film/photo documents — `EAD_pic.xml`
| Field | VN name | Type | Length | Value |
|---|---|---|---|---|
| `docCode` | Mã lưu trữ tài liệu | String | 100 | as in 3.2 |
| `maintenance` | Thời hạn lưu trữ | String | 100 | `01`–`07` (as in 3.2) |
| `typePic` | Phân loại | String | 2 | `01` negative film, `02` photo |
| `archivesNumber` | Số lưu trữ (đặc thù) | String | 50 | |
| `inforSign` | Ký hiệu thông tin | String | 30 | |
| `eventName` | Tên sự kiện | String | 500 | |
| `imageTitle` | Tiêu đề phim/ảnh | String | 500 | |
| `photographer` | Tác giả | String | 300 | |
| `photoPlace` | Địa điểm chụp | String | 300 | |
| `photoTime` | Thời gian chụp | Date | DD/MM/YYYY | |
| `colour` | Màu sắc | String | 50 | `01` colour, `02` black & white |
| `filmSize` | Cỡ phim/ảnh | String | 30 | |
| `docAttached` | Tài liệu đi kèm | Boolean | 1 | `0` No / `1` Yes |
| `mode` | Chế độ sử dụng | String | 20 | `01`/`02`/`03` (as in 3.2) |
| `format` | Tình trạng vật lý | String | 50 | |
| `riskRecovery` | Chế độ dự phòng | Boolean | 1 | `1`/`0` |
| `riskRecoveryStatus` | Tình trạng dự phòng | String | 2 | `01`/`02` |
| `description` | Ghi chú | String | 500 | |

### 3.4. Video/audio documents (recordings) — `EAD_media.xml`
| Field | VN name | Type | Length | Value |
|---|---|---|---|---|
| `docCode` | Mã lưu trữ tài liệu | String | 100 | as in 3.2 |
| `maintenance` | Thời hạn lưu trữ | String | 100 | `01`–`07` |
| `typeMedia` | Phân loại | String | 2 | `01` audio, `02` video |
| `archivesNumber` | Số lưu trữ (đặc thù) | String | 50 | |
| `inforSign` | Ký hiệu thông tin | String | 30 | |
| `eventName` | Tên sự kiện | String | 500 | |
| `movieTitle` | Tiêu đề phim/âm thanh | String | 500 | |
| `recorder` | Tác giả | String | 300 | |
| `recordPlace` | Địa điểm | String | 300 | |
| `recordDate` | Thời gian | Date | DD/MM/YYYY | |
| `language` | Ngôn ngữ | String | 100 | `01`–`11` (pick one or more) |
| `playTime` | Thời lượng | String | 8 | |
| `docAttached` | Tài liệu đi kèm | Boolean | 1 | `0`/`1` |
| `mode` | Chế độ sử dụng | String | 20 | `01`/`02`/`03` |
| `quality` | Chất lượng | String | 50 | normal, blurry, noisy… |
| `format` | Tình trạng vật lý | String | 50 | |
| `riskRecovery` | Chế độ dự phòng | Boolean | 1 | `1`/`0` |
| `riskRecoveryStatus` | Tình trạng dự phòng | String | 2 | `01`/`02` |
| `description` | Ghi chú | String | 500 | |

---

## 4. Schemas (.xsd)
All schemas have `<simpledc>` as root (complexType `elementContainer`, group `elementsGroup`), importing `xml.xsd`.
- **EAD.xsd** (package): `fileCode, title, source, totalDoc, description`.
- **EAD_doc.xsd** (text): `docId, docCode, maintenance, typeName, codeNumber, codeNotation, issuedDate, organName, subject, language, numberOfPage, inforSign, keyword, mode, confidenceLevel, autograph, format, process, description`.
- **EAD_pic.xsd** (negative film/photo): `docCode, maintenance, typePic, archivesNumber, inforSign, eventName, imageTitle, photographer, photoPlace, photoTime, colour, filmSize, mode, format, description, docAttached`.
- **EAD_media.xsd** (video/audio): `docCode, maintenance, typeMedia, archivesNumber, inforSign, eventName, movieTitle, recorder, recordPlace, recordDate, language, playTime, mode, quality, format, description, docAttached`.

---

## 5. Mimetype & extension list
| Code | Kind | Extension → mimetype |
|---|---|---|
| DOC | Text | `.txt`→text/plain · `.rtf`(v1.8/1.9.1)→application/rtf · `.docx`→…wordprocessingml.document · **`.pdf/a` (two-layer)**→application/pdf · `.doc`→application/msword · `.odt`(v1.2)→…opendocument.text |
| OTHER | Spreadsheet | `.csv`→text/csv · `.xlsx`→…spreadsheetml.sheet · `.xls`→application/vnd.ms-excel · `.ods`(v1.2)→…opendocument.spreadsheet |
| OTHER | Presentation | `.htm`→text/html · `.pptx`→…presentationml.presentation · `.ppt`→application/vnd.ms-powerpoint · `.odp`(v1.2)→…opendocument.presentation |
| PIC | Image | `.jpeg/.jpg`→image/jpeg · `.gif`(89a)→image/gif · `.tif/.tiff`→image/tiff · `.png`→image/png |
| MEDIA | Video | MPEG-1/2/4→video/mpeg · `.avi`→video/x-msvideo · `.wmv`→video/x-ms-wmv · `.mov/.qt`→video/quicktime |
| MEDIA | Audio | `.mp3`→audio/mpeg · `.wma`→audio/x-ms-wma · `.aac`→audio/aac |

> Document formats inside the package: text/technical documents = **two-layer PDF/A**; negative/photo = JPEG;
> audio/video = MPEG-4, MP3, avi, wma, wmv.

---

## 6. ZIP file naming rule
`ZIP name = [SIP_tailieu sequence within the submission] + [SIP_tailieu package ID]`
- Sequence: entered by the user (Arabic numerals).
- Package ID: taken from `OBJID` of the root METS.xml element.

---

## 7. Code-value appendix

**`typeName` – Document type:** 01 Nghị quyết · 02 Quyết định · 03 Chỉ thị · 04 Quy chế · 05 Quy định · 06 Thông cáo · 07 Thông báo · 08 Hướng dẫn · 09 Chương trình · 10 Kế hoạch · 11 Phương án · 12 Đề án · 13 Dự án · 14 Báo cáo · 15 Tờ trình · 16 Giấy ủy quyền · 17 Phiếu gửi · 18 Phiếu chuyển · 19 Phiếu báo · 20 Biên bản · 21 Hợp đồng · 22 Công văn · 23 Công điện · 24 Bản ghi nhớ · 25 Bản thỏa thuận · 26 Giấy mời · 27 Giấy giới thiệu · 28 Giấy nghỉ phép · 29 Thư công · 30 Bản đồ · 31 Bản vẽ kỹ thuật · 32 Khác.

**`language` – Language:** 01 Tiếng Việt · 02 Tiếng Anh · 03 Tiếng Pháp · 04 Tiếng Nga · 05 Tiếng Trung · 06 Việt Anh · 07 Việt Nga · 08 Việt Pháp · 09 Hán Nôm · 10 Việt Trung · 11 Khác.

**`maintenance` – Retention period:** 01 Vĩnh viễn · 02 70 năm · 03 50 năm · 04 30 năm · 05 20 năm · 06 10 năm · 07 Khác.

**`mode` – Usage mode:** 01 Công khai · 02 Sử dụng có điều kiện · 03 Mật.

**`confidenceLevel` – Confidence level:** 01 Gốc điện tử · 02 Số hóa · 03 Hỗn hợp.

**`RECORDSTATUS`:** NEW · SUPPLEMENT · REPLACEMENT · TEST · OTHER. **`csip:OAISPACKAGETYPE`:** SIP · AIP · DIP.

> Source: Appendix IV, Circular 05/2025/TT-BNV (pages 239–291).

---

## 8. Cross-check against the REAL SAMPLE PACKAGE (`uuid-0F203B84-6641-4CDA-9F52-DAAE55FEF6B1`)

> A real SIP package in the repo was extracted & read (137 PDF documents, all text EAD_DOC). **The tool that
> created the sample writes `ArchiveNex`, but this system uses the name `SipPack`** (placed in `agent/name`).
> In practice it **deviates significantly** from the spec tables in sections 1–7. When building the
> generator/validator, **follow the real-world behavior + the schemas in the package `schemas/`**, NOT the
> TT05 tables verbatim.

### 8.1. Real folder structure
```
uuid-0F203B84.../
├── METS.xml
├── metadata/descriptive/EAD.xml
├── schemas/  (ead.xsd, ead_doc.xsd, ead_media.xsd, ead_pic.xsd,
│              mets1_12.xsd, xlink.xsd, DILCISExtensionMETS.xsd, DILCISExtensionSIPMETS.xsd)
└── representations/rep1/
    ├── METS.xml
    ├── metadata/descriptive/EAD_DOC_<docId>.xml   (137 files, 1 per document)
    └── data/<docId>.pdf                           (137 files)
```
- **No** `documentation/`. Real metadata file name: `EAD_DOC_H49.64.33.2001.160.1.xml` (uppercase `EAD_DOC_`),
  data name = `H49.64.33.2001.160.1.pdf`.

### 8.2. Real EAD.xml (record) ≠ table 3.1
Per `schemas/ead.xsd`, the record EAD has **18 fields** (`xs:all`), **NO `source`**, and several record-level
fields not listed in table 3.1:
`fileCode, title, maintenance, mode, language, startDate, endDate, keyword, totalDoc, numberOfPaper, numberOfPage, format, inforSign, confidenceLevel, paperFileCode, riskRecovery, riskRecoveryStatus, description`
- Added vs 3.1: `maintenance, mode, language, startDate, endDate, keyword, numberOfPaper, numberOfPage, format, inforSign, confidenceLevel, paperFileCode, riskRecovery, riskRecoveryStatus`.
- Removed vs 3.1: `source` (does not appear in the real package).

### 8.3. Real EAD_doc/pic/media
- `ead_doc.xsd` has **21 fields** and **DOES have** `riskRecovery`/`riskRecoveryStatus` (resolving the conflict in
  3.2 vs the TT05 schema — in reality both are KEPT).
- ⚠️ **`docCode` vs `arcDocCode`**: the in-package schema declares `arcDocCode`, but **all 137 real XML files
  actually write `<docCode>`** → the sample's own schema and data do NOT match. The current generator uses `docCode`.
- `ead_pic.xsd`/`ead_media.xsd` in reality use `arcDocCode`, both have `riskRecovery`/`riskRecoveryStatus`.

### 8.4. Real METS ≠ section 2 description
| Point | TT05 (section 2) | Real package |
|---|---|---|
| `mets/@TYPE` | `Collection` | **`Mixed`** |
| `agent` (root) | ROLE=CREATOR, TYPE=OTHER/SOFTWARE, note `SOFTWARE VERSION` | ROLE=CREATOR TYPE=OTHER OTHERTYPE=SOFTWARE, name=**`SipPack`** (sample = `ArchiveNex`), note **`IDENTIFICATIONCODE`=P_KHCN** (fonds code at root, **no** SOFTWARE VERSION note) |
| `agent` (rep1) | TYPE=ORGANIZATION, IDENTIFICATIONCODE=fonds code | TYPE=**INDIVIDUAL**, name=`cvlt`, note IDENTIFICATIONCODE=P_KHCN |
| `amdSec` | not mentioned | **HAS** an `<amdSec>` element (empty) at root |
| `dmdSec/mdRef` | MDTYPE=OTHER, MIMETYPE=text/xml | MDTYPE=**DC**, MDTYPEVERSION=**1.0.0**, MIMETYPE=**application/xml** |
| `altRecordID` | submission registration code | = `fileCode` (`H49.64.33.2001.160`) |
| rep1 `mets/@OBJID` | own uuid | **`rep1`** (fixed string, not a uuid) |
| `fileGrp/@USE` | Schemas, Documentation, Representations/rep1, Data | root: `Schemas` + `Representations/rep1`; rep1: only `Data` |
| `<file>` schema | MIMETYPE per the list | uses `application/octet-stream` for .xsd; `application/pdf` for data |
| structMap rep1 | Data, AttachmentFile, MetadataLink | `rep1`(ORIGINAL) → `MetadataLink` (137× `MetadataLink/File`: `@DMDID`=dmdSec, `fptr@FILEID`=data `<file>` ID) + `Data` (fptr→Data fileGrp ID). **Does not use `AttachmentFile`** in the sample (all documents independent). |

### 8.5. Real code structure (≠ the fileCode rule in 3.1)
- Real `fileCode` = `H49.64.33.2001.160` — **not** following the "identifier + year + submission count(2) + sequence(7)"
  formula. Form: `H49.64.33` (organization) `.2001` (year) `.160` (record/catalogue number).
- `docId` = `H49.64.33.2001.160.1` (= fileCode + document sequence).
- `docCode` = `H49.64.33.1994.160.0000001` (has a `0000001` suffix). → **Do NOT hard-enforce** the 2/7-char
  structure; store as string, validate loosely.
- **Real ZIP file name** = `b6acafed-a809-4b78-ae9f-0373d50d52f0.zip` — **a single lowercase GUID**, with NO
  `uuid-` prefix, **no sequence**, and **different** from the package OBJID (`uuid-0F203B84-...`). Conflicts with
  section 6 (TT05 mandates `[seq] + [OBJID]`). → **Follow the real package: name the ZIP as a lowercase GUID,
  drop the sequence**; the package folder (name = OBJID) sits INSIDE the zip (only visible after extracting).
  `PackService` generates exactly this and keeps only the ZIP (deleting the extracted folder).

> Conclusion: the real package follows its own schema variant (rich record ead.xsd without source; doc/pic/media
> keep riskRecovery; METS uses TYPE=Mixed, OBJID rep1, has amdSec, MDTYPE=DC). The DB design must follow this
> variant — see the updates in `thiet-ke-db-dong-goi-sip.md`.

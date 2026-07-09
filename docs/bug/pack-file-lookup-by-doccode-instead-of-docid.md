# BUG: Pack matches attachment files by docCode instead of docId (empty rep1)

> **Severity: HIGH — produced SIP packages with an EMPTY `representations/rep1` (no data, no metadata).**
> Affects **PackService** (`PackHoSoFolderAsync` + `PackOneHoSo`). Read before touching the pack file-mapping.

## 1. Summary
When packing a record, `representations/rep1/data/` and `representations/rep1/metadata/descriptive/`
came out **completely empty** while the pack reported success. Root package (`METS.xml`, `EAD.xml`, `schemas/`)
was fine — only the representation had no documents.

## 2. Root cause
`PackService` located each document's attachment file **by `docCode`** (column `Mã lưu trữ của tài liệu`,
`CheckConstants.ColMaLuuTruTaiLieu`):
```csharp
var docCode = ReadCell(row, vanBanColumns, CheckConstants.ColMaLuuTruTaiLieu);
var sourceFile = attachmentFiles.FirstOrDefault(f =>
    Path.GetFileNameWithoutExtension(f) == docCode);   // WRONG key
```
But attachment files are named by **`docId`** (`Mã định danh tài liệu`, `ColMaDinhDanhTaiLieu`), and in real data
`docCode != docId`. So `sourceFile` was `null` for **every** row → every document skipped → empty representation.
(`CheckService` already matches files by `docId`, and the real sample package names data `= {docId}.pdf` and
metadata `= EAD_DOC_{docId}.xml`; only pack was inconsistent.)

Masking effect: a separate change had turned the "no file for row" case from `throw` into a silent `continue`
(sheet rows without a file are allowed). That flipped this bug from a visible pack failure into a **silent
empty-but-"successful"** package.

## 3. Evidence (measured)
Input `H49.64.33.2015.315`, sheet `Van_ban` row 2:
- `Mã định danh tài liệu` (docId) = `H49.64.33.2015.315.1` → equals the file `H49.64.33.2015.315.1.pdf`.
- `Mã lưu trữ của tài liệu` (docCode) = `H49.64.33.1994.315.0000001` → **different** (`.1994.` + `.0000001` suffix).
Output `c85f610e-…` package: `rep1/data` and `rep1/metadata` empty; root `EAD.xml`/`METS.xml`/`schemas` present.

## 4. Fix
Match the attachment file and name the data/metadata files by **`docId` (`ColMaDinhDanhTaiLieu`)**, not `docCode`:
```csharp
var docId = ReadCell(row, vanBanColumns, CheckConstants.ColMaDinhDanhTaiLieu);
if (string.IsNullOrWhiteSpace(docId)) continue;
var sourceFile = attachmentFiles.FirstOrDefault(f =>
    Path.GetFileNameWithoutExtension(f) == docId);
...
var metaFileName = $"{SipPackConstants.EadDocPrefix}{docId}.xml";
```
`docCode` stays only as the `<docCode>` field value inside the EAD_DOC XML (emitted by `BuildSimpleDc` from the
`Mã lưu trữ của tài liệu` column). Applied to both `PackHoSoFolderAsync` and `PackOneHoSo`.

## 5. How to prevent
- **File identity in this domain is `docId` (mã định danh) = the physical file name.** `docCode` (mã lưu trữ) is a
  metadata value, NOT a file key — never use it to find/name files.
- Keep **pack and check on the same matching key** (`ColMaDinhDanhTaiLieu`). If they diverge, a record that passes
  check can silently produce a broken package.
- Treat a "successful pack with empty rep1" as a red flag: assert `docs.Count > 0` (or compare against sheet rows)
  when verifying pack output.

# BUG: Config header Unicode-normalization mismatch (DB config vs `EadFieldMapping` keys)

> **Severity: CRITICAL — silent data loss in generated SIP packages + broken round-trip.**
> Affects **PackService** (packaging) and **UnpackService** (reverse/rebuild). A must-read for any agent
> touching config-driven field mapping, Excel↔EAD generation, or the check/pack/unpack flows.

---

## 1. Summary (one paragraph)

Field names (`TruongThongTin`) stored in the SQLite `ConfigHoSo` / `ConfigVanBan` tables are **not in the
same Unicode form** as the dictionary keys hard-coded in `EadFieldMapping.cs`. Several Vietnamese headers are
stored **partially decomposed (not NFC)**, and one contains a **non-breaking space (U+00A0)** instead of a
normal space. Because every lookup goes through `StringComparer.OrdinalIgnoreCase` (byte/codepoint-level, **not**
normalization-aware), `map.TryGetValue(configHeader)` **silently fails** for those headers. The field is then
skipped — dropped from the generated EAD when packing, and dropped from the rebuilt Excel when unpacking.

---

## 2. Root cause

- `Sip.be.Shared/Constants/Documents/EadFieldMapping.cs` defines the header→EAD-element maps
  (`EadFieldMapping.HoSo`, `EadFieldMapping.VanBan`). All keys in the C# source are **NFC** and use normal spaces.
- The DB config rows come from importing the Excel `Mô tả_Ho_so` / `Mô tả_Van_ban` sheets
  (`ConfigService.ImportConfig*`). The source workbook was edited in **Google Sheets**
  (the `.xlsx` carries a `GoogleSheetsCustomDataVersion2` marker), which can emit Vietnamese text in a
  **non-NFC (partially decomposed)** form and can introduce **non-breaking spaces**.
- SQLite stores the text **byte-for-byte** exactly as imported (it does **not** normalize Unicode). So the
  "dirty" form survives all the way into `ConfigHoSo.TruongThongTin` / `ConfigVanBan.TruongThongTin`.
- Dictionary lookups compare with `StringComparer.OrdinalIgnoreCase`, which is **case-insensitive but not
  normalization-insensitive**. A precomposed `ậ` (U+1EAD) and a decomposed `a` + combining marks are different
  codepoint sequences → **not equal** → `TryGetValue` returns `false`.

> Rule of thumb: **two strings that look identical on screen can differ in bytes.** Vietnamese is especially
> prone to NFC vs NFD divergence, and copy/paste or spreadsheet exports often inject NBSP (U+00A0).

---

## 3. Evidence (reality — measured, not guessed)

Comparing `Sip.be.Api/sip.db` config against `EadFieldMapping.cs` keys:

- `EadFieldMapping.cs` keys: **all NFC**, all normal spaces.
- DB config headers that DO NOT byte-match the C# keys, but DO match after `NFC + whitespace` normalization:

| Table | Stored `TruongThongTin` (repr) | Problem |
|---|---|---|
| ConfigHoSo | `'Tiêu đề\xa0hồ sơ'` | non-breaking space U+00A0 |
| ConfigHoSo | `'Tình trạng vật lý (nếu có)'` | not NFC (decomposed) |
| ConfigHoSo | `'Ký hiệu thông tin (nếu có)'` | not NFC |
| ConfigHoSo | `'Mức độ tin cậy'` | not NFC |
| ConfigHoSo | `'Mã hồ sơ gốc giấy (nếu có)'` | not NFC |
| ConfigVanBan | `'Mã lưu trữ của tài liệu'` | not NFC |
| ConfigVanBan | `'Mức độ tin cậy'` | not NFC |

All of them become equal to the corresponding `EadFieldMapping` key after
`Normalize(NFC)` + replace NBSP with space + collapse/trim whitespace.

Concrete round-trip observation (same record, `.01` renamed to `.02`):

- Original `H49.61.8.2017.01.xlsx`: `Ho_so` = **18 columns**, `Van_ban` = **21 columns**.
- Rebuilt `H49.61.8.2017.02.xlsx` (pack → unpack): `Ho_so` = **13 columns**, `Van_ban` = **19 columns**.
- Missing columns line up **exactly** with the mismatched headers above, e.g. `Tiêu đề hồ sơ` (title),
  `Mức độ tin cậy` (confidenceLevel), `Mã lưu trữ của tài liệu` (docCode) — including **required** fields.

---

## 4. Consequences

1. **PackService.BuildSimpleDc** (`Sip.be.Application/Document/Implements/PackService.cs`):
   `if (!map.TryGetValue(truong, out var element)) continue;` — for every mismatched header the element is
   **silently omitted** from `EAD.xml` / `EAD_DOC_*.xml`. The generated SIP package **loses data**
   (e.g. `title`, `docCode`, `confidenceLevel`, `format`) even though the source Excel had it.
2. **UnpackService.BuildColumns** (`.../Implements/UnpackService.cs`): the same
   `headerToElement.TryGetValue(header)` fails → those columns are dropped from the rebuilt Excel.
3. **Round-trip is lossy**: pack → unpack does not reproduce the original record; columns and values disappear.
4. **CheckService only appears healthy by accident**: it compares config headers against the *Excel* column
   headers, and both come from the **same "dirty" source**, so they match each other. This **masks** the bug —
   validation passes while packaging silently drops fields.

---

## 5. Solution (implemented)

Add a single canonicalizer and route **every header string through it at both the dictionary-key and the
dictionary-query boundary**, so comparisons are always canonical-vs-canonical regardless of the source form.

Canonicalizer -- `EadFieldMapping.NormalizeHeader(string?)`:
1. `String.Normalize(NormalizationForm.FormC)` (-> NFC / precomposed).
2. `Regex.Replace(s, @"\s+", " ")` -- collapses runs of whitespace to a single space. In .NET, `\s` matches
   `\p{Z}`, which **includes the non-breaking space U+00A0**, so NBSP is folded to a normal space here too.
3. `Trim()`.

Applied at exactly these boundaries (keeping `EadFieldMapping.cs` keys as the canonical NFC reference -- they are
already NFC, so a normalized query matches them):

- **Config import** (`ConfigService.ImportConfigHoSoAsync` / `ImportConfigVanBanAsync`): store `TruongThongTin`
  normalized -> the DB is clean at rest (the "normalize on the way in" root fix).
- **Excel header read** (`PackService.BuildColumnMap` + the two inline column maps in `CheckService` +
  `CheckService.BuildColumnMap`): normalize the **column-map keys** -- because the `Ho_so`/`Van_ban`
  data-sheet headers are **dirty too**, so cleaning only the config would break `ReadCell`/validation
  (clean config vs dirty Excel). Both sides must be canonical.
- **Every lookup query**: `PackService.ReadCell` + `PackService.BuildSimpleDc` (`map.TryGetValue`),
  `CheckService` (all `columns.TryGetValue(...)` against `ColMaHoSo`/`ColMaLuuTruTaiLieu`/`cfg.TruongThongTin`),
  `UnpackService.BuildColumns` + the `fileCode` resolution -- wrap the query in `NormalizeHeader(...)`. This also
  makes existing (already-dirty) DB rows work without a re-import.

> This fixes only the **key comparison** -- no business logic changed. It repairs both directions: PACK now writes
> all EAD elements (with values), and UNPACK/CHECK map all columns correctly.

> Note on the "SQLite compilation" hypothesis: SQLite does **not** transform text -- it stores exactly what was
> inserted. The non-NFC/NBSP forms originate upstream (the Google-Sheets-exported `.xlsx`). Normalizing at the
> data boundaries is robust regardless of origin, and preserves the header<->data column count.

## 6. How to prevent this (guidance for agents)

- **Never trust that DB/Excel/JSON text byte-matches a C# string literal.** Normalize external strings
  (`NFC` + whitespace/NBSP cleanup) before using them as dictionary keys, especially for Vietnamese.
- Prefer a **single normalization helper** and route all external headers/keys through it.
- When a config-driven field "silently disappears", suspect a **key mismatch** (Unicode form, NBSP, trailing
  space, zero-width chars) before suspecting logic. Compare with `repr()`/hex, not by eye.
- A passing `CheckService` does **not** prove the mapping is correct — check whether both sides share the same
  dirty source (which hides mismatches).

---

## 7. Quick reproduction / verification

- Dump `ConfigHoSo.TruongThongTin` from `sip.db` and compare each value (byte-level / `repr`) against the keys
  in `EadFieldMapping.cs`. Mismatches vanish after `NFC + NBSP→space + collapse-whitespace`.
- Round-trip a known record (pack then unpack) and diff the `Ho_so` / `Van_ban` column sets against the
  original workbook; dropped columns correspond 1:1 to the mismatched headers.

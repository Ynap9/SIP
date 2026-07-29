# BUG: PDFsharp rewrites the whole PDF when signing — every pre-existing signature is destroyed

> **Severity: BLOCKER — FIXED 2026-07-21.** Signing an already-signed archival document silently invalidated the
> original signature. Runtime-confirmed 2026-07-20 against the real file `filetestky/H49.59.24.2015.440.133.pdf`
> (signed by a production tool, then re-signed by SIPPACK with the real Phạm Ngọc Chính token).
> Fixed by option 1 below — see [§6](#6-fix-as-shipped).
> Plan: [../../.claude/be/plans/pdf-incremental-signing.plan.md](../../.claude/be/plans/pdf-incremental-signing.plan.md).

## 1. Summary
`PdfSigner` opens the document with `PdfReader.Open(path, PdfDocumentOpenMode.Modify)` and calls `document.Save()`.
PDFsharp does **not** append an incremental update — it **serialises a brand-new document**. Every byte offset
moves, so the pre-existing signature's `/ByteRange` now covers unrelated bytes and its signature no longer matches
the content it claims to cover.

Archival hồ sơ files are routinely already signed, so this is the normal case, not an edge case.

## 2. Evidence (runtime, real file)

| Measure | Original | After SIPPACK signed it |
|---|---|---|
| Size | 4 541 452 B | 4 641 775 B |
| `startxref` occurrences | — | **1** (an incremental update would add one) |
| `%%EOF` occurrences | — | **1** |
| First differing byte | — | **offset 10** |
| Header | `%PDF-1.7 … /Linearized 1 /L 4473213` | `%PDF-1.7 … 1 0 obj <</Contents 2 0 R/CropBox…` |

The original signature's own ranges, re-read from the new file:

| Range of the pre-existing signature | Bytes changed |
|---|---|
| `[0, 4473579)` | **4 453 608 / 4 473 579 (99.6 %)** |
| `[4506733, 4541452)` | **34 541 / 34 719 (99.5 %)** |

Both `/ByteRange` arrays survive in the output —
`[0 4473579 4506733 34719]` (old, ends at the *original* file length) and
`[0 4539816 4605354 36421]` (ours, ends at the new file length) — but the old one is now meaningless: the
linearised layout it was signed over no longer exists.

## 3. Root cause
`PdfDocument.Save` re-serialises the object graph. PDFsharp's signing support (`DigitalSignatureHandler`) computes
`/ByteRange` over **the document it is writing**, which is the correct behaviour for a first signature and the
wrong behaviour for adding one. Nothing in the PDFsharp API surface promises append semantics — the documentation
is silent on it, which is why
[../pdf-signing-libraries.md](../pdf-signing-libraries.md) §5 listed it as an open question to settle by spike
rather than an assumption. The spike has now answered it: **no append.**

## 4. Fix — options, none free
1. **Write a real incremental update** — append the new revision to the original bytes verbatim (sig dict,
   `/ByteRange` placeholder, new xref with `/Prev`, trailer), never re-serialising earlier revisions. This is plan
   step 5 of [../plans/pdf-signing.plan.md](../plans/pdf-signing.plan.md), the hand-rolled writer that adopting
   PDFsharp was meant to avoid.
2. **Refuse to sign an already-signed file** and surface it in the FE (`alreadySigned` is already returned by
   `GET sign/danh-sach-file`). Safe, but leaves the real workflow unsupported.
3. **Change library.** iText does incremental signing correctly but is AGPL/paid — rejected on licence grounds in
   [../pdf-signing-libraries.md](../pdf-signing-libraries.md) §2.

## 5. How to prevent
- **"The library handles the container" is only true for the operations the library documents.** PDFsharp
  documents *signing*; it never documents *appending to a signed file*. An undocumented capability is an absent
  capability — treat it as such instead of inferring it from the presence of a related feature.
- **Test the append case with a file that is already signed**, from the first commit. A fixture that starts
  unsigned can never catch this, and every sample in `file/` is single-signature.
- **Byte-compare the output against the input** when a format claims append semantics: identical leading bytes are
  the definition of an incremental update, and the check costs three lines.

## 6. Fix as shipped

Option 1. `PdfIncrementalSigner` copies the original bytes verbatim and appends a new revision (Catalog and page 1
overridden under their original object numbers, new objects for the field/widget/signature value/appearance, then an
uncompressed xref stream with `/Prev`). `PdfSigner` no longer calls `PdfReader.Open(Modify)` or `document.Save()`.
PDFsharp is kept **only** to draw the appearance — signer names carry Vietnamese diacritics, so an embedded Unicode
font subset is mandatory and PDFsharp already builds one correctly.

### Verified (measured, all 14 sample PDFs, 2026-07-21)

Signed with a self-signed probe certificate — this exercises the **container**, not the TSA path.

| Check | Result |
|---|---|
| Leading bytes identical to input | **14/14**; the real file keeps its first **4 541 452** bytes, output 4 640 541 |
| `startxref` / `%%EOF` in output | **3 / 3** on every file (was 1 / 1 before the fix) |
| Signature blocks | **1 → 2** on every file |
| Pre-existing signature verdict | **unchanged**: `ValidTrusted` on the 13 `file/` PDFs, `TimeMismatch` on the real one |
| Any `InvalidSignature` | **none** |
| `/Encrypt` guard | blocked, `ErrorCodes.SignPdfEncrypted` (1033) |
| `/DocMDP /P 1` guard | blocked, `ErrorCodes.SignPdfLocked` (1034) |

### Real-token run (2026-07-21) — the remaining gap is now closed
Signed through the real API with a production USB token (Phạm Ngọc Chính, CA phục vụ các cơ quan Nhà nước G2):

| Check | Result |
|---|---|
| Leading bytes identical to input | **4 541 452** bytes preserved |
| `startxref` / `%%EOF` | **3 / 3**; signature blocks **2** |
| Integrity of *both* signatures | **PASS** on both |
| TSA token on both | present, `VerifySignatureForSignerInfo` **PASS** |
| New signature's verdict | **`ValidTrusted`** (delta −233 s, inside the 300 s tolerance) |
| Pre-existing signature's verdict | **unchanged** (`TimeMismatch`, a genuine 378 s drift predating SIPPACK) |
| File-level verdict | `TimeMismatch` by worst-wins, driven entirely by the pre-existing signature |
| Rendering | opens correctly in Foxit; both signatures shown |

This also produced the first signature in the corpus with a standards-correct `signingTime`, which is what exposed
[genTime-offset-compensates-on-the-wrong-side.md](genTime-offset-compensates-on-the-wrong-side.md).

**All 14 sample PDFs are already signed** — `file/` contains no unsigned fixture, so the "sign an unsigned PDF"
path is covered only by the code handling a missing `/AcroForm`, not by a measurement.

# Plan — PDF incremental signing: keep existing signatures and original bytes intact

> **Status: steps 1–5 done 2026-07-21** (`PdfRevisionInfo`, `PdfObjectWriter`, `PdfAppearanceBuilder`,
> `PdfIncrementalSigner`, wired into `PdfSigner`). Step 6's probe was written and run against all 14 sample PDFs —
> results in the [bug log §6](../../../docs/bug/pdfsharp-rewrites-document-destroying-existing-signature.md) — but
> it is **not committed**; where it should live is still open. Not verified: signing with a real USB token.

> Replaces `document.Save()` in [`PdfSigner`](../../../Sip.BE/Sip.be.External/Documents/PdfSigner.cs) with a real
> **incremental update**. Fixes the BLOCKER in
> [pdfsharp-rewrites-document-destroying-existing-signature.md](../../../docs/bug/pdfsharp-rewrites-document-destroying-existing-signature.md).
> Supersedes step 4 of [pdf-signing-be.plan.md](pdf-signing-be.plan.md) and step 5 of
> [pdf-signing.plan.md](../../../docs/plans/pdf-signing.plan.md). Prerequisite (**done**): the `ParseBlock` fix —
> without it a destroyed signature is skipped silently and every check below reports a false pass.

## Requirements / Input

- **Input**: an existing PDF (signed or not) plus the cached token certificate and key, exactly what `SignAsync`
  receives today. No change to the API surface.
- **The original bytes must survive verbatim.** The output's leading bytes are **byte-identical** to the input and
  the new revision is appended after them. A PDF signature covers absolute byte offsets, so this is the only way an
  earlier signature keeps covering what it signed.
- **One code path for signed and unsigned input.** Appending to an unsigned PDF is a valid incremental update too;
  a separate "fast path" would only be a second thing to get wrong.
- **Fail-closed guards**: refuse `/Encrypt` (out of scope) and refuse a certification signature with `/DocMDP /P 1`
  (the author forbade further changes). Each surfaces as a per-file failure, never a silent pass.
- **Reuse as-is**: `CadesSigner` (CMS + `signingCertificateV2` + TSA), `StoreSigningKey` cache, `_keyLock`,
  `SigningConstants`, the temp-file-then-rename write, `ISignatureVerifier`.

### What the real file already proves

`filetestky/H49.59.24.2015.440.133.pdf` is **itself an incremental update**, written by iText 5.5.3:

| Property | Value | Consequence for us |
|---|---|---|
| `startxref` / `%%EOF` | 2 each, `/Prev 216` | the append pattern is already in the file; we add a third revision |
| xref form | **xref stream** (`/Type/XRef`, `/W [1 3 2]`) | ours must also be a stream — a classic table chained onto a stream is not read reliably |
| `ObjStm` | present | an object we override may live compressed; an uncompressed override with the same number wins |
| `/Encrypt` | absent | no decryption work on the real corpus |
| `/DocMDP` | absent | adding a signature is permitted |
| `/Index [0 1 14 1 18 1 20 1 24 1 38 15]` | 5 objects overridden, 15 added | **the exact scope of our writer, confirmed by a production tool** |

## Steps

1. **Revision probe + guards.** `PdfRevisionInfo` in `Sip.be.External/Documents/`: resolve the trailer from the last
   `startxref` and expose `Root` ref, `/ID`, `/Size`, xref offset, plus flags `IsEncrypted` / `DocMdpLocked`; throw
   `UserFriendlyException` on the two guard cases. Check the probe against all 15 sample PDFs (`file/` ×13,
   `filetestky/` ×2) — every value must match the raw bytes. `dotnet build`.
2. **Focused object serializer.** `PdfObjectWriter`: serialize only the value types that actually occur in the
   Catalog / AcroForm / Page dictionaries — name, number, string, boolean, null, indirect reference, array, nested
   dictionary. **Decision:** streams are never inline in those three dicts (always references), so stream encoding
   stays out of scope and this remains ~80 lines rather than a general PDF writer. Round-trip check: serialize a
   parsed Catalog, re-parse it, compare. `dotnet build`.
3. **Appearance transplant.** Keep PDFsharp for drawing only: render the two-line appearance into a throwaway
   in-memory PDF, then copy the appearance XObject **and its font object subtree** into our revision, renumbering
   as we go. **Decision:** signer names carry Vietnamese diacritics (`Phạm Ngọc Chính`); base-14 Times-Roman with
   WinAnsi cannot encode `ạ ọ í`, so an embedded Unicode subset is mandatory — and PDFsharp already builds one
   correctly, which is why it stays. Confirm the glyphs render with diacritics in a real viewer. `dotnet build`.
4. **The appender.** `PdfIncrementalSigner`: copy the input bytes verbatim, then append — overrides of Catalog
   (`/AcroForm`), AcroForm (`/Fields` += , `/SigFlags 3`) and Page (`/Annots` += ) reusing their original object
   numbers; new objects for the signature field/widget, the signature value dict (`/Contents` placeholder +
   `/ByteRange`) and the transplanted appearance; then an **uncompressed xref stream** (`/W [1 4 2]`, `/Prev` = the
   old `startxref`, `/ID` preserved) followed by `startxref` and `%%EOF`. **Decision:** the xref stream declares no
   `/Filter` — the spec permits it and it keeps a Flate encoder off the critical path. Two passes: lay the revision
   out to fix offsets, patch `/ByteRange`, hand the two ranges to `CadesSigner`, patch `/Contents` in place.
   `dotnet build`.
5. **Wire into `PdfSigner`.** Replace `PdfReader.Open(Modify)` + `document.Save()` with the appender, keeping the
   temp-file-then-rename. Delete the three comments that claim PDFsharp appends (`PdfSigner.cs:19-20`,
   `IPdfSigner.cs:14`, `SignFileListDto.cs:16`) — they assert the opposite of measured behaviour. `dotnet build`.
6. **Verification harness.** A probe that, for every sample PDF, asserts the output's leading bytes equal the input
   byte-for-byte, then runs `ISignatureVerifier` and asserts **every** signature is reported — the pre-existing one
   no worse than its verdict before signing. **Decision:** byte-prefix equality is the definition of an incremental
   update and costs three lines; it catches this class of bug that a "does it still open?" check never will.
   `dotnet build`.

## Expected output

- Signing `filetestky/H49.59.24.2015.440.133.pdf` produces a file whose **first 4 541 452 bytes are identical** to
  the input, carrying 3 `startxref` / `%%EOF` markers.
- `ISignatureVerifier` reports **two** signatures on it: the iText one holding its original verdict (`TimeMismatch`,
  the same 378 s skew it has today) and the SIPPACK one `ValidTrusted`. The file verdict is worst-wins across the
  two and is **never `InvalidSignature`** — which is exactly what it is today.
- Signing an unsigned PDF from `file/` gives one signature, `ValidTrusted`, original bytes intact.
- Encrypted and `/DocMDP /P 1` files fail their own file with a clear reason; the batch continues.

## Deviations taken while building (measured, not assumed)

- **Overrides splice bytes; they are not re-serialised.** Step 2 planned to parse a Catalog / Page dict and write
  it back. Probing showed every sample has the AcroForm **inline** in a plain Catalog with `/Fields[N 0 R]`, and
  page 1 in an ObjStm with `/Annots[N 0 R]` — so the override is a one-token insert into the original bytes
  (`PdfObjectWriter.AppendToArray`). Keeping the original bytes verbatim cannot corrupt a value we failed to model;
  re-serialising can. The writer is still used for the objects we author ourselves.
- **Both xref forms are read, and the new xref mirrors the input's form.** The plan fixed on an xref stream. All 14
  samples are streams, but PDFsharp itself emits classic tables, and the appearance transplant reads a PDFsharp
  document with the same reader — so classic-table support fell out for free rather than becoming a failure mode.
- **`/Widths` is an array object, not a dict.** The first transplant left `/Widths 13 0 R` pointing at the *source*
  document because the reader only returned dicts. Non-dict indirect objects are now read too. This was invisible
  in the output file and would have rendered a corrupt appearance.
- **`SignService` reported an empty failure reason.** `UserFriendlyException` keeps its text in `MessageLocalize`,
  not `Exception.Message`, so the two new guards would have surfaced a blank reason to the FE. Fixed in the
  per-file catch.

# Visible signature appearance — extracted from the real files

> The exact on-page signature block SIPPACK must reproduce, **read out of the production sample**
> `file/H49.64.33.2015.315.1.pdf` (signer *UBND phường Hà Tu*), not from a spec table.
> Per [CLAUDE.md](../CLAUDE.md) golden rule 4, the real file is the source of truth.
> Companion: [pdf-signing-libraries.md](pdf-signing-libraries.md), [plans/pdf-signing.plan.md](plans/pdf-signing.plan.md) step 8.

## 1. What it looks like

```
┌───────────────────────────────────────┐   170 × 30 pt
│ Ủy ban Nhân dân phường Hà Tu          │   line 1 — signer CN
│ 2026-04-16T05:25:06.2686779+07:00     │   line 2 — signing time, ISO 8601
└───────────────────────────────────────┘   Times New Roman 10pt, black
```

Two lines only. **No** "Ký bởi:" / "Ký ngày:" labels, no logo, no border, no red seal — plainer than most
Vietnamese signing tools produce. Reproduce this, not a prettier variant.

## 2. Exact values

| Item | Value |
|---|---|
| Widget `/Rect` | `[424.72 812.4 594.72 842.4]` |
| Size | **170 × 30 pt** |
| Position | **top-right corner**, flush to top and right edge (page `/MediaBox [0 0 594.72 842.4]`, A4) |
| Font | `ALPRUB+TimesNewRomanPSMT` subset, `Identity-H`, **10 pt** |
| Colour | `0 0 0 rg` (black) |
| Line 1 origin | `Tm = 1 0 0 1 2 18` → x=2, y=18 |
| Line 2 origin | `Tm = 1 0 0 1 2 8` → x=2, y=8 |
| Line 1 text | signer certificate **CN** |
| Line 2 text | signing time, **ISO 8601 with offset** (`yyyy-MM-ddTHH:mm:ss.fffffffzzz`) |
| Annotation flags | `/F 132` (Print 4 + Locked 128) |

The 7-digit fractional seconds are .NET's round-trip format (`DateTime.ToString("o")`) — the sample was produced by
iTextSharp 5.5.3.

> **SIPPACK deviates on one point (2026-07-28):** it **centres** both lines in the block instead of anchoring them
> at x=2pt as the sample does. Everything else (size, font, 10pt at the 30pt reference height, the two line
> contents) still follows the table above. See
> [sign-placement.contract.md](../.claude/contracts/sign-placement.contract.md).

## 3. Object structure in the file

```
obj 27  /FT/Sig /Type/Annot /Subtype/Widget  /Rect[…] /AP<</N 26 0 R>> /P 6 0 R
        /T(Sign_<guid-no-dashes>)  /V 21 0 R          ← /V → the signature dict
obj 25  /XObject /Form  BBox[0 0 170 30]  "FRM"
        /Resources /XObject << /n0 22 0 R  /n2 23 0 R >>
        content:  q 1 0 0 1 0 0 cm /n0 Do Q   q 1 0 0 1 0 0 cm /n2 Do Q
obj 22  n0 — BBox[0 0 100 100], background, effectively empty
obj 23  n2 — BBox[0 0 170 30], the two text lines
obj  2  /AcroForm << /Fields[27 0 R] /DR<</XObject<</FRM 25 0 R>>…>> /SigFlags 3 >>
page 6  /Annots[27 0 R]
```

**The `n0` + `n2` split is an iText artifact, not a requirement.** Adobe deprecated layers `n1`/`n3`/`n4` long ago;
Acrobat and Foxit render `/AP /N` and overlay their own status badge. A single flat form XObject displays
identically — reproduce the *visual result*, do not feel obliged to recreate the layer nesting.

## 4. Signature dictionary of the same file (for reference)

```
/Type/Sig /Filter/Adobe.PPKLite /SubFilter/ETSI.CAdES.detached
/Reason(Ký số tài liệu) /Location(Việt Nam) /ContactInfo()
/M(D:20260416052506+07'00')
/ByteRange [0 648109 681263 37658]
```

`/SubFilter /ETSI.CAdES.detached` confirms PAdES — this is the value SIPPACK must write too (see
[pdf-signing-libraries.md](pdf-signing-libraries.md) §5 open question 1).

## 5. How to build it with PDFsharp

`DigitalSignatureOptions` covers all of it: `Rectangle` (`XRect`) for §2's position/size, `Reason` / `Location` /
`ContactInfo` for §4, and `AppearanceHandler` — which hands over an `XForm` canvas — for the two text lines.
Nothing here needs a hand-rolled appearance stream.

Open point: PDFsharp's `Rectangle` is an `XRect` in its own coordinate space; confirm the block lands at the
**top-right** of the page, since PDF `/Rect` is bottom-left-origin while `XRect` is top-left-origin. Verify against
the sample's `[424.72 812.4 594.72 842.4]` rather than trusting the conversion.

## 6. Caveat — appearance is not validity

Foxit/Adobe always draw `/AP /N`, but the green "Signature valid" tick depends on **the viewer's own trust store**,
not on the file. Neither trusts Ban Cơ yếu Root G1/G2 by default, so a genuine signature reads "validity unknown"
until that root is installed — exactly what [toolsign-integration.md](toolsign-integration.md) §5 hit. Keep the two
acceptance criteria separate: *SIPPACK produced a conformant signature* vs *this machine's viewer trusts the CA*.

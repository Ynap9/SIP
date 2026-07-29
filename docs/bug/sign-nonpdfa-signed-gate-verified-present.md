# BUG: non-PDF/A file reportedly signed — sign-path PDF/A gate verified present & correct

> **Severity: MEDIUM — reported symptom, no reproducible code defect in current source.**
> **Status: INVESTIGATED 2026-07-21.** Affects **`Sip.BE/Sip.be.Application/Document/Implements/SignService.cs`**
> (`SignAsync`, the `ky-thu-muc` loop) and **`Sip.BE/Sip.be.External/Documents/PdfFormatInspector.cs`**.

---

## 1. Summary

A user reported that a file whose pre-check status was "không phải PDF/A" still produced a signed output. On review,
the defensive PDF/A gate in `SignAsync` is **present and correct**: before signing each file it calls
`_pdfFormatInspector.Inspect(item.FullPath)` and, for any verdict other than `Valid`, sets
`success=false / skipped=true`, increments `skippedCount`, emits the `file` event and `return`s **without** calling
`_pdfSigner.SignAsync`. Every path to the signer is guarded; a non-`Valid` verdict cannot reach it.

---

## 2. Root cause (most likely)

- The **entire** sign feature is uncommitted work-in-progress (`SignService.cs` is untracked). The defensive gate +
  the `skipped` mechanism were added the same day (see the dated note in `.claude/contracts/sign-flow.contract.md`).
- The desktop binary the user tested was **built before that gate line existed**, so it signed without gating. The
  current source already blocks it; a rebuild carries the fix. This matches the symptom exactly (pre-check said
  NotPdfA, sign still signed) — impossible with the current code because both paths run the **same** inspector on the
  **same** file and are therefore deterministic.

---

## 3. Two real, in-code limitations noted (not fixed here — out of scope)

1. **`PdfFormatInspector` is a self-declaration heuristic, not a full validator.** It classifies a file `Valid` when
   the XMP declares `pdfaid:part` (in the PDF/A namespace) **and** it has both an image and a text layer. A file that
   self-declares PDF/A but fails a real validator (veraPDF) would legitimately pass this gate. The common non-PDF/A
   case (no `pdfaid:part` at all) is caught → `NotPdfA` → skipped.
2. **`PdfFormatVerdict.Valid = 0` is the enum default (fail-open).** `PdfFormatInspectResult` defaults to `Valid`
   before inspection. Every current path overwrites it, so it is safe today, but any future path that returns without
   setting `Verdict` would default to "signable". Prefer making the default a non-signable value if the enum is ever
   reordered (note: it is FE-facing in `sign-flow.contract.md`, so do not reorder casually).

---

## 4. How to prevent this

- **Rebuild after wiring a defensive gate** before concluding it "doesn't work" — a stale WIP binary is the first
  suspect when reviewed source already guards the path.
- Keep security gates **fail-closed**: assert the positive (`== Valid` to proceed) and treat every other verdict —
  including a default-constructed result — as blocked.
- Remember the inspector trusts the file's PDF/A self-declaration; it is not a conformance validator.

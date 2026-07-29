# BUG: `sign-step` writes pre-check summary into `allValid` instead of `checkSummary`

> **Severity: HIGH — dead state signal; a whole class of derived UI silently never fires.**
> **Status: FIXED 2026-07-21** (same task that switched the sign gate to `validCount`).
> Affects **`Sip.FE/src/app/pages/ky-so/sign-step/sign-step.ts`** (the PDF/A pre-check → sign flow).

---

## 1. Summary (one paragraph)

`sign-step` declares a `checkSummary` signal and derives `validCount()`, `invalidCount()`, `totalCount()` and
`hasSkippable()` from it. But `handleCheckEvent`, on the SSE `summary` event, stored the payload into a **different**
signal — `allValid` — and **never** called `checkSummary.set(...)`. So `checkSummary` stayed `null` forever, every
derived computed returned its `?? 0` fallback, and any UI reading them was permanently wrong.

---

## 2. Root cause

- The pre-check summary event carries `{ totalFiles, validCount, invalidCount, allValid }`.
- The handler only did `this.allValid.set(ev.summary.allValid)` — it dropped the counts on the floor.
- `validCount = computed(() => this.checkSummary()?.validCount ?? 0)` (and siblings) read from `checkSummary`,
  which nothing ever set → always `null` → always `0`.
- It went unnoticed because the original "Ký số" gate used `allValid()` (which *was* set), so the only consumer
  that mattered at the time worked; the count-based consumers (`hasSkippable`) were latent.

---

## 3. Evidence

- Grep showed `checkSummary` was only ever **read** (in the four computeds) — never assigned — while `allValid`
  held the data that `checkSummary` was designed to hold.
- `hasSkippable = checkDone() && validCount() > 0 && invalidCount() > 0` therefore could never be `true`, so the
  "có file bị bỏ qua" note in the template had **never rendered** since it was written.

---

## 4. Consequences

1. **`hasSkippable` note never shows** — users were never told some files would be skipped.
2. **Latent trap for the gate change**: switching `canSign()` from `allValid()` to `validCount() > 0` (the
   requested new behaviour: enable "Ký số" when at least one file is valid) would have left the button **permanently
   disabled**, because `validCount()` is hard-wired to `0` by the missing assignment.
3. Any future feature keyed on `validCount/invalidCount/totalCount` would silently read zeros.

---

## 5. Solution (implemented)

- In `handleCheckEvent`, the `summary` branch now stores the whole payload:
  `this.checkSummary.set(ev.summary)` — the single source `validCount`/`invalidCount`/`hasSkippable` read from.
- Reset it in `startCheck()` with `this.checkSummary.set(null)` (replacing the old `allValid.set(false)`).
- Removed the now-redundant `allValid` signal entirely (it was write-only after the gate moved to `validCount`).

No business logic changed beyond routing the summary to the signal its consumers actually read.

---

## 6. How to prevent this (guidance for agents)

- **A declared signal that is only ever read is a red flag** — grep both `.set(` and the read sites when a signal
  backs `computed()`s. A write-only *other* signal nearby usually means the data landed in the wrong place.
- When a derived value is "always 0 / always empty", suspect its **source signal is never assigned** before
  suspecting the computed's logic.
- When changing a gate from signal A to signal B, verify B is actually populated on the same event that populated A.

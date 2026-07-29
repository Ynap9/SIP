# Review reports

> Findings written by the **sip-reviewer** agent. Every other agent **reads this folder before starting work** so
> it knows what is already known-broken and what must be fixed. Reviews are the hand-off channel between the
> reviewer and the `sip-be` / `sip-fe` agents.

## 1. File naming

```
.claude/review/<yyyyMMdd-HHmm>-<slug>.md      e.g. 20260720-1530-pdf-signing-be-plan.md
```

Vietnam time (UTC+7). One review per file — never append a second review to an existing file, so history stays
readable and a stale review is easy to delete.

## 2. Required shape of a review file

```markdown
# Review — <what was reviewed>

> Target: <path(s)>  ·  Reviewed: <yyyy-MM-dd HH:mm>  ·  Verdict: PASS | PASS WITH GAPS | FAIL

## 1. Requirements checked against
| # | Requirement (as the user stated it) | Met | Where |
|---|---|---|---|

## 2. Findings
| # | Severity | Finding | File:line | Fix |
|---|---|---|---|---|

## 3. Not verified
<what the reviewer could not check, and why — never leave this implied>
```

- **Severity**: `BLOCKER` (violates a stated requirement) · `MAJOR` (works but breaks a repo convention) ·
  `MINOR` (style/clarity).
- Requirements come from **what the user actually asked**, quoted — not from what the implementation assumed.
- Every finding must cite `file:line` and name a concrete fix. "Could be better" is not a finding.

## 3. Status marker

When a finding is fixed, the agent that fixed it edits the review file and marks that row `✅ FIXED (<commit/date>)`.
Do not delete the row — the point is that the same mistake is not repeated.

A review file whose findings are **all** fixed gets `Verdict: RESOLVED` in its header and may be deleted after the
feature ships.

## 4. Rules

- The reviewer **never edits code** — it only writes files in this folder.
- A review that finds nothing still gets a file, with `Verdict: PASS` and section 3 filled in. Silence is not a
  result.
- Reviews are English (per the [write-markdown](../skills/write-markdown/SKILL.md) skill); keep Vietnamese domain
  terms verbatim.
- Each file stays **under 100 lines**; if a review runs longer, split it per that skill and link the parts.

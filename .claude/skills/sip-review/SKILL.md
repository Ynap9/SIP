---
name: sip-review
description: Review the work just completed against what the user actually asked for. Invoked by the user as /sip-review. Calls the sip-reviewer agent, which writes a report to .claude/review/ and returns a verdict. Use only when the user asks — never automatically after a task.
---

# Skill: /sip-review

Run a requirements review of the work just done. **User-triggered only.**

## Steps
1. **Collect the specification.** Find the user's original request for this task **verbatim** in the conversation —
   the message that stated the requirements, not your summary of it. If several messages refined it, pass all of
   them in order; the later ones override.
2. **Collect the targets.** List the files created/changed in this task. If unsure, use
   `git status --short` and `git diff --name-only` to get the real set rather than recalling it.
3. **Call `sip-reviewer`** with, in this order:
   - the verbatim request(s),
   - the target paths,
   - "post-task review",
   - any constraint the user stated mid-task (e.g. "don't split under 10 lines over", "constants in shared").
4. **Relay the result**: verdict, count by severity, every BLOCKER in full, and the review file path. The agent's
   report is not shown to the user — so quote what matters.
5. **Do not fix anything yet.** Ask the user which findings to act on; they may accept a gap deliberately.

## When fixes are approved
Route each finding to the owning agent (`sip-be` for `Sip.BE/**`, `sip-fe` for `Sip.FE/**`, main session for docs).
The agent that fixes a finding marks that row `✅ FIXED` in the review file — the row stays, so the mistake is not
repeated.

## Rules
- Pass the request **verbatim**. A reviewer given a paraphrase reviews the paraphrase, and will confirm work that
  drifted from what was asked — the exact failure this command exists to catch.
- If no clear requirement statement exists in the conversation, say so; the review runs against repo conventions
  only and is labelled `PASS WITH GAPS`.
- One review per invocation, one file per review (see [../../review/README.md](../../review/README.md)).

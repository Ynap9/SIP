# API contracts (BE ↔ FE hand-off)

> The agreement between `sip-be` and `sip-fe` on a full-stack task. **`sip-be` writes it; `sip-fe` implements
> against it.** The FE must never read BE source to guess a field name — that is what this folder prevents.

## Naming
```
.claude/contracts/<feature>.contract.md      e.g. pdf-signing.contract.md
```

## Required shape
```markdown
# Contract — <feature>

| Method | Route | Request | Response | Notes |
|---|---|---|---|---|
| GET | api/core/document/sign/chung-thu-so | ?onlySignable=bool | ApiResponse<SignCertDto[]> | |
| POST | api/core/document/sign/ky-thu-muc | SignRequestDto | SSE SignProgressDto | text/event-stream |

## DTO shapes — JSON exactly as the FE receives it (camelCase)
```json
{ "thumbprint": "…", "sourcePath": "…", "reason": "…" }
```

## Enums (serialised as NUMBERS)
`CertSource`: 0 Local · 1 Server · 2 UsbToken

## SSE events
Which `targetType` values exist, which one is the manifest, which one is the summary, and what `completed` means.
```

## Rules
- **camelCase**, and enums are **numbers** — state the numbering explicitly; the FE mirrors it in
  `shared/constants/<feature>.constants.ts`.
- Every REST call returns **HTTP 200**; real status is in `ApiResponse.status` (1=ok, 0=error). Do not document
  non-200 status codes — they do not occur.
- **A rename is a contract change.** Update this file and `SendMessage` the other agent. A silent rename is the
  single most common way this repo breaks.
- The contract **wins** any BE/FE disagreement until `sip-be` updates it.
- Under 100 lines per the [write-markdown](../skills/write-markdown/SKILL.md) skill; split and link if longer.

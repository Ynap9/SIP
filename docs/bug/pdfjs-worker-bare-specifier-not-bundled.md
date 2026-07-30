# BUG: pdf.js worker resolved from a bare specifier is never emitted by the production build

> **Severity: HIGH — PDF preview works in `ng serve` but is dead in every packaged desktop build.**
> **Status: FIXED 2026-07-29.**
> Affects the **Sign** flow step 2 (`pages/ky-so/config-step`) — the only screen that renders PDF.
> A must-read for any agent adding a worker, wasm module, or other runtime-loaded asset to the FE.

---

## 1. Summary

`config-step.ts` pointed pdf.js at its worker with a **bare package specifier**:

```ts
pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url
).toString();
```

The `ng serve` dev server resolves that specifier through its module graph, so preview worked on the developer
machine. The production builder (esbuild) **does not** resolve bare specifiers inside `new URL(...)` into an
emitted asset — it leaves the expression verbatim in the bundle and emits no file. In the installed app the
expression evaluated to a path that does not exist, so no PDF ever rendered.

---

## 2. Root cause

- esbuild only treats `new URL('<path>', import.meta.url)` as an asset reference when `<path>` is a
  **relative path**. A bare specifier (`pdfjs-dist/...`) is left untouched — no warning, no error.
- Evidence, from the shipped bundle before the fix:
  ```js
  Ae.workerSrc = new URL("pdfjs-dist/build/pdf.worker.min.mjs", import.meta.url).toString()
  ```
  and `dist/Sip.FE/browser/media/` contained **only** the primeicons font files — no worker.
- At runtime `import.meta.url` is the chunk's own URL, so the value resolved to
  `<origin>/pdfjs-dist/build/pdf.worker.min.mjs`, which is not served.
- The failure was **silent instead of a 404**: `Program.cs` ends the pipeline with
  `MapFallbackToFile("index.html")`, so the missing worker returned **HTTP 200 with an HTML body**. pdf.js
  tried to run that HTML as a worker script and failed, which is why the console error never said "not found".

---

## 3. Evidence

| Build | `workerSrc` at runtime | Worker file in output | PDF preview |
|---|---|---|---|
| `ng serve` (dev) | resolved by dev server | served from module graph | works |
| `--configuration desktop` (before fix) | `<origin>/pdfjs-dist/build/pdf.worker.min.mjs` | **absent** | dead |
| `--configuration desktop` (after fix) | `<origin>/pdf.worker.min.mjs` | `pdf.worker.min.mjs`, 1.25 MB | works |

---

## 4. The fix

1. `Sip.FE/angular.json` — copy the worker out of `node_modules` as a build asset, so it lands at the root of
   the browser output and is served by `UseStaticFiles` (and picked up unchanged by `build-desktop.ps1` step 3,
   which copies `dist/Sip.FE/browser` into `publish/wwwroot`):
   ```json
   { "glob": "pdf.worker.min.mjs", "input": "node_modules/pdfjs-dist/build" }
   ```
2. `Sip.FE/src/app/pages/ky-so/config-step/config-step.ts` — resolve against the document base instead:
   ```ts
   pdfjs.GlobalWorkerOptions.workerSrc = new URL('pdf.worker.min.mjs', document.baseURI).toString();
   ```
   `document.baseURI` follows the `<base href="/">` in `index.html`, so it is correct both under `ng serve`
   and in the desktop host, whose Kestrel port is assigned dynamically.

No backend change was needed: `.mjs` **is** present in the default `FileExtensionContentTypeProvider` of
.NET 9, so the static-file middleware serves it with a JavaScript MIME type. Had it been absent, the file would
have been skipped as an unknown type and swallowed by the same `MapFallbackToFile` trap.

---

## 5. How to prevent

- Never point a runtime asset at a **bare specifier** inside `new URL(..., import.meta.url)`. Either declare it
  in `angular.json` `assets` and resolve against `document.baseURI`, or use an explicit **relative** path.
- Treat "works under `ng serve`, broken when packaged" as an **asset-resolution** symptom first. The dev server
  resolves far more than the production builder does.
- After touching any runtime-loaded asset, verify the built output, not just the dev run:
  ```bash
  ls Sip.FE/dist/Sip.FE/browser/          # the file must actually be there
  grep -o "workerSrc=[^;]*" Sip.FE/dist/Sip.FE/browser/*.js
  ```
- Remember that `MapFallbackToFile("index.html")` turns every missing static asset into a 200 + HTML. A wrong
  asset path will never surface as a 404 in this app — check the emitted files, not the network status code.

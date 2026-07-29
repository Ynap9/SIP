# Plan — PDF signing (BE), part 2 of 2: decisions

> **Part 2 of 2** · previous: [pdf-signing-be.plan.md](pdf-signing-be.plan.md) — requirements, steps, expected
> output. This part explains **why** those steps look the way they do. Read part 1 first.

## Certificate & PIN

- **Fail-fast on the certificate, per-file on the documents.** The asymmetry is deliberate: a bad certificate
  invalidates *every* file, so stopping at file 0 saves a useless run; a bad *file* affects only itself.
- **The pre-flight test-signs 32 dummy bytes.** `ICertificateProvider` deliberately reads key metadata only so it
  never raises a PIN prompt — which means nothing else has ever proven the token can sign. This is the one place
  that must, and it is also where the unresolved CNG-vs-PKCS#11 question (readiness §4) surfaces first.
- **The PIN is never read, transported or stored by SIPPACK.** The dialog belongs to the token middleware
  (bit4id xPKI / VGCA PKI Manager) and appears when the private key is first used. Collecting the PIN ourselves
  would mean building a PIN UI, shipping the string from the WebView to the BE, and holding it in memory — strictly
  worse security for no gain. Instead we **hold the key handle** for the batch; the middleware caches the PIN
  against that handle, so the user types it once at the pre-flight and the rest of the batch is silent.

## Cache

- **What is cached**: the resolved `X509Certificate2`, the `TokenVerifyDto`, and the **private-key handle**, keyed
  by thumbprint. Without it, every file re-scans two certificate stores, rebuilds the chain, re-runs the test-sign
  — and re-prompts for the PIN. 500 files would mean 500 PIN dialogs.
- **When it dies**: `ClearCache()` disposes the key handle and drops everything. It is called in `finally` at the
  end of a run, when the token is detected missing, and when a different thumbprint is requested. A cache that
  outlived the run would report a valid token after the user had unplugged it.
- **Re-running always re-verifies.** Because the cache is gone, a repeat run after re-plugging goes through the
  full pre-flight and prompts for the PIN again — which is the intended behaviour, not overhead.
- **`SignData` refuses to run un-verified.** If the cache does not hold the requested thumbprint it throws rather
  than silently resolving the key, because a lazy resolve inside the parallel stage would pop a PIN dialog in the
  middle of a run.

## Token monitoring

- **A monitor loop polls `IsTokenPresent` while the batch runs.** Unplugging a token mid-run otherwise surfaces as
  a confusing per-file crypto error on every remaining file; detecting it once and stopping is both faster and
  legible.
- **The probe must not touch the private key.** It re-scans the store for the thumbprint — when the token is
  removed, its certificates disappear from the store. Touching the key instead would re-prompt for the PIN on every
  poll.
- **The probe deliberately bypasses the cache** — the cache is exactly the thing being falsified.
- **On detection**: cancel the run, report every unprocessed file as failed with a token-removed reason, and clear
  the cache. Reporting the remaining files rather than silently truncating keeps `SuccessCount + FailedCount`
  equal to the manifest the FE already drew.

## Parallelism

- **Pipeline, not "sign everything in parallel".** Three stages, different limits: *prepare* (CPU+IO, parallel),
  *key signature* (**serialised** — a USB token is a single hardware session; concurrent use risks a PIN prompt per
  file or a token lock), *timestamp + write* (network, parallel — and the dominant cost: measured TSA round-trip is
  **2–131 s**). Serialising only the key stage protects the token while the slow stage still overlaps.
- **Dynamic work queue, not static chunks.** Splitting the list into N fixed slices is exactly the "one thread
  swamped, others idle" failure — PDF sizes vary wildly. `Parallel.ForEachAsync` (or `Channel<T>` + N consumers)
  lets each worker pull the next file when free, so load balances itself with no size estimation.
- **Degree of parallelism** = `Math.Clamp(Environment.ProcessorCount, 2, 8)`, TSA stage capped separately
  (default 4). The bottleneck is network, not CPU, and an uncapped fan-out would hammer a government TSA.

## Output & safety

- **Progress is file-count based** (`Current` / `Total` / `Percent`), not byte-based: per-file cost is dominated by
  a roughly constant TSA round-trip, so file count tracks perceived time better than size.
- **Always write to a new folder** — a failed signature can never damage an original; the run stays re-runnable.
- **`SignService` must not touch `_sipDbContext`** — signing persists nothing, and `DbContext` is not thread-safe,
  so leaving it unused removes the only shared-state hazard in the parallel path.
- **Every signed file is re-verified by `ISignatureVerifier`** before being counted a success. Writing bytes to disk
  is not the same as producing a signature the pack gate will accept.

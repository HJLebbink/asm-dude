# Intel documentation source (for the docs extractor)

This project extracts instruction reference data from the **Intel® 64 and IA-32
Architectures Software Developer's Manual (SDM)** — the *combined volumes*
edition (1, 2A–2D, 3A–3D, 4), Intel document/order number **325462**.

## Where to get the latest PDF

| What | URL |
|------|-----|
| **Permanent "latest" redirect** (always newest revision) | <https://cdrdv2.intel.com/v1/dl/getContent/671200> |
| Human landing page (lists current revision of every volume) | <https://www.intel.com/content/www/us/en/developer/articles/technical/intel-sdm.html> |

The permanent redirect resolves to a CDN URL whose **filename encodes the
revision**, e.g.:

```
https://cdrdv2.intel.com/v1/dl/getContent/671200
  → https://cdrdv2-public.intel.com/<contentId>/325462-091-sdm-vol-1-2abcd-3abcd-4-v2.pdf
                                                        ^^^ revision (091)
```

The numeric `<contentId>` prefix changes with every release; the
`325462-<rev>-sdm…` revision number is the stable signal we compare against.

> **Note:** `www.intel.com` blocks scripted requests (HTTP 403). The
> `cdrdv2.intel.com` / `cdrdv2-public.intel.com` CDN endpoints do **not** —
> they are what the checker and the download command below use.

## Download the latest manually

```bash
# from the asm-annotate/data directory:
curl -L -A "Mozilla/5.0 (Windows NT 10.0; Win64; x64)" \
  -o "325462-<rev>-sdm-vol-1-2abcd-3abcd-4.pdf" \
  "https://cdrdv2.intel.com/v1/dl/getContent/671200"
```

Save it into `VS/CSHARP/asm-annotate/data/`. The extractor automatically
picks the **highest-revision** `325462-*.pdf` it finds there
(`IntelDocChecker.FindLocalPdf`), so you don't need to delete the old one —
but you'll usually want to, since each PDF is ~26 MB.

## Verifying you have the latest (in code)

`IntelDocChecker` (see `IntelDocChecker.cs`) performs a lightweight `HEAD`
request against the permanent redirect, parses the resolved revision, and
compares it to the local PDF. It never downloads the 26 MB body just to check.

Run the check from the CLI:

```bash
dotnet run --project VS/CSHARP/asm-annotate -- check-latest
```

Exit code `0` = up to date, `1` = outdated / could not verify. Example output:

```
Checking Intel SDM revision (document 325462, combined volumes)...
  Source: https://cdrdv2.intel.com/v1/dl/getContent/671200
  Local : revision 090  (.\data\325462-090-sdm-vol-1-2abcd-3abcd-4.pdf)
  Latest: revision 091  (325462-091-sdm-vol-1-2abcd-3abcd-4-v2.pdf)
  Verdict: ❌ Outdated (local 090 < latest 091). Download the latest:
           https://cdrdv2-public.intel.com/<id>/325462-091-sdm-vol-1-2abcd-3abcd-4-v2.pdf
```

The `extract-aaa` demo also calls the check first and prints a warning (but
does **not** block) if a newer revision exists.

## API quick reference

| Member | Purpose |
|--------|---------|
| `IntelDocChecker.LatestCombinedVolumesRedirectUrl` | The permanent redirect constant. |
| `FindLocalPdf(dataDir)` | Highest-revision local `325462-*.pdf` + its revision. |
| `ParseRevision(name)` | Pull the revision int out of a filename/URL. |
| `GetLatestRevisionAsync()` | `HEAD` Intel, return `(revision, fileName, url)`. |
| `CheckAsync(dataDir)` | Compare local vs. latest; never throws on network errors. |
| `CheckAndReportAsync(dataDir)` | `CheckAsync` + console report; returns `true` if current. |

## Revision history of the local copy

| Revision | Intel publication | Notes |
|----------|-------------------|-------|
| 090 | February 2026 | previous local copy |
| 091 | (current as of June 2026) | downloaded via the permanent redirect |

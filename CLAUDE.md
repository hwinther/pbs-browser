# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

`pbs-browser` is a single-container internal web app that browses and restores individual files from
**client-side-encrypted** Proxmox Backup Server (PBS) snapshots. It wraps the `proxmox-backup-client`
CLI (which holds the decryption keyfile) behind a small HTTP API + SPA. There is no database.

## Layout

- `backend/` — ASP.NET Core 10 Minimal API (`PbsBrowser.slnx`, project `src/Api`, tests `tests/Api.Tests`).
- `frontend/` — Vite + React + TypeScript SPA, built into `backend/src/Api/wwwroot` at image build.
- `Dockerfile` — single multi-stage image (frontend build → pbs-client-static → dotnet publish → runtime).
- `deploy/` — workload manifests (Deployment, Service, Ingress), shipped as a signed OCI artifact
  that Flux in hwinther/proxmox applies. `deploy/README.md` is the contract with the cluster.
- `e2e/` — Playwright suite (own npm package on `@wshno/playwright-config`). It asserts on the
  `FakePbsClient` sample data and runs against `docker-compose.e2e.yml` (the image with `PBS_FAKE=1`).
  `e2e/Dockerfile` bakes it into an image on `ghcr.io/wshno/playwright-config` (same version as the
  lock file), run as the compose `playwright` service; the app traces to the stack's OTel collector
  (`e2e/otel/collector.yaml` → `e2e/otel-out/traces.jsonl`) for the slow-request report.

## Commands

```bash
# dotnet commands run from backend/: its global.json opts into Microsoft.Testing.Platform, and
# `dotnet test` from the repo root fails with "Testing with VSTest target is no longer supported"
cd backend && dotnet build PbsBrowser.slnx
cd backend && dotnet run --project src/Api/Api.csproj   # :8080
cd backend && dotnet test --solution PbsBrowser.slnx
cd frontend && npm install && npm run dev              # :5173
cd frontend && npm test && npm run build
docker build --platform linux/amd64 -t pbs-browser:dev .
# e2e against the image, as CI runs it (suite in its own image, as a compose service)
docker build -t pbs-browser-e2e:local e2e
APP_IMAGE=pbs-browser:dev docker compose -f docker-compose.e2e.yml up -d --wait
APP_IMAGE=pbs-browser:dev docker compose -f docker-compose.e2e.yml --profile e2e run --rm playwright
# or the suite from the host (also against `dotnet run`, which fakes PBS in Development)
cd e2e && npm ci && npx playwright install chromium && npm test
```

Single tests and linting:

```bash
# backend (from backend/): xunit v3 on MTP — filters go straight to dotnet test
dotnet test --project tests/Api.Tests --filter-class "*CatalogParserTests"
dotnet test --project tests/Api.Tests --filter-method "*IsValidSnapshot*"
# frontend (vitest)
cd frontend && npx vitest run src/Tree.test.tsx -t "<test name>"
cd frontend && npm run lint && npm run typecheck && npm run format:check
# e2e
cd e2e && npx playwright test tests/browse.spec.ts && npm run lint && npm run typecheck
```

The backend builds with `TreatWarningsAsErrors` and restores from committed `packages.lock.json`
files, so a NuGet change also needs the lock file updated (`dotnet restore --force-evaluate`).

## Architecture

The request flow, spread across `Program.cs` and `backend/src/Api/Pbs/`:

1. **Endpoints** (all in `Program.cs`, a Minimal API under `/api`) validate every user value with
   `PbsInputValidation` before anything else.
2. **`IPbsClient`** has two implementations, picked at startup: `PbsClient` (spawns the binary) and
   `FakePbsClient` (in-memory sample data). The fake is used when `PBS_FAKE=1`, or in Development when
   the binary at `PBS_CLIENT_PATH` is absent. A new client operation needs **both** implementations, and
   the e2e specs assert on the fake's sample data, so changing that data means updating `e2e/tests`.
3. **Parsers**: `SnapshotParser` reads the JSON from `snapshot list|files`, and `CatalogParser` turns
   `catalog dump` text into a `CatalogNode` tree. `/api/catalog` caches the tree in `IMemoryCache` for 10 min.
4. **Download**: `restore <snapshot> <archive> <scratch> --pattern <path>` into a `pbsr-<guid>` temp
   dir, then streamed back through `CleanupFileStream`, which deletes the dir on dispose. Each restore
   is audit-logged (category `Audit`) with the `Remote-User`.

Runtime flags besides the `PBS_*` connection vars (listed in README):
- `PBS_FAKE`: forces the fake client on (`1`) or off (`0`).
- `PBS_DEBUG=1`: adds `/api/debug/catalog`, which returns the raw `catalog dump` exit code and
  output. Use it when adjusting the `CatalogParser` tokenizer.
- `CSP_MODE`: `report-only` (the default) or `enforce`. `Security/SecurityHeaders.cs` sets a strict
  CSP with no `'unsafe-inline'`, so the SPA must not use inline scripts or styles. Browsers post
  violations to `/csp-report` (`CspReports`).
- `OTEL_EXPORTER_OTLP_ENDPOINT`: turns on OTLP traces (one span per request, `/healthz` excluded).
  Logs are exported only for the CSP-report category; everything else stays on stdout.

The frontend has no router or state library: `App.tsx` handles the snapshot → archive → tree flow,
`Tree.tsx` is the lazy tree, and `api.ts` holds the fetch layer plus the pure helpers (snapshot
grouping by host, archive-name mapping) that the unit tests cover. `vite build` empties and writes
`backend/src/Api/wwwroot`, so to run e2e on the host against `dotnet run`, build the SPA first. The
Vite dev server proxies `/api` and `/healthz` to :8080.

## CI

- `ci.yml`: frontend and backend build, test and lint.
- `pr-e2e.yml`: build the app and e2e images → Grype scan and Playwright e2e (compose service, with
  a PR comment listing API requests over 2 s from the OTel traces) in parallel → with the
  `deploy-feature` label, push the image that passed and deploy a preview (real PBS, Authelia-gated).
  On main it also runs the unit tests and publishes the quality artifact.
- `container.yml`: release image on `v*` tags (dispatched by `tag-and-release.yml`), then
  `manifests:<version>` rendered from `deploy/` and signed.
- `manifest-diff.yml`: PRs that touch `deploy/` get a rendered diff and resource map as a comment.

## Conventions & hard rules

- **Never** build a shell command string for the PBS client. Always use `Process.StartInfo.ArgumentList`
  (no shell, no interpolation). All user-supplied `snapshot`/`archive`/`path` values must pass
  `PbsInputValidation` (allow-list regex, reject `..`, NUL, glob metacharacters) before reaching the
  client wrapper. This is the security boundary — keep it covered by tests.
- **Read-only** with respect to PBS: the app only lists, dumps the catalog, and restores to a scratch
  dir for streaming. It never writes back to the datastore.
- The app trusts `Remote-User` / `Remote-Email` headers **only** because the NetworkPolicy admits
  ingress solely from Traefik (forward-auth). Do not expose this app without that in front.
- Scratch dirs from a restore are deleted on stream close (`CleanupFileStream`) / in a `finally`.
- The image is **amd64-only** (`proxmox-backup-client-static` has no arm64 build).
- `catalog dump` (client 4.2.8) writes its listing to **stderr** with an empty stdout, so it is
  parsed from both streams (`CatalogParser.ParseCommandOutput`); only known entry-type lines count.
  The real captured output is a fixture in `CatalogParserTests`, and `FakePbsClient` mimics it.
- Some snapshots (the k0s nodes) are deliberately encrypted with a key this app does not hold, so
  it cannot become a privilege-escalation path; they fail with "wrong key". Don't work around that.
- `catalog dump` output is a text format, not a stable API — the line tokenizer in `CatalogParser`
  may need adjusting to the installed client version; the tree-building logic is unit-tested so only
  the tokenizer should ever change.

## Deployment

The workload manifests are `deploy/` here. Each release publishes them as
`ghcr.io/hwinther/pbs-browser/manifests:<version>`, and the infra repo pins that tag in
`proxmox/clusters/production/apps/pbs-browser-production/` (the promotion gate). The infra repo
keeps the namespace (labelled for the Kyverno keyfile clone), the network policies and
`APP_HOST` (`pbs-browser.mgmt.wsh.no`). Secrets are created out-of-band — see
`pbs-browser-secrets.md` there.

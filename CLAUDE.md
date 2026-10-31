# CLAUDE.md

Guidance for working in this repo.

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

## Commands

```bash
dotnet build backend/PbsBrowser.slnx
dotnet run   --project backend/src/Api/Api.csproj      # :8080
dotnet test  backend/PbsBrowser.slnx
cd frontend && npm install && npm run dev              # :5173
cd frontend && npm test && npm run build
docker build --platform linux/amd64 -t pbs-browser:dev .
# e2e against the image (or against `dotnet run`, which fakes PBS in Development; build the SPA first)
APP_IMAGE=pbs-browser:dev docker compose -f docker-compose.e2e.yml up -d --wait
cd e2e && npm ci && npx playwright install chromium && npm test
```

## CI

- `ci.yml`: frontend and backend build, test and lint.
- `pr-e2e.yml`: build the image → Grype scan and Playwright e2e in parallel → with the
  `deploy-feature` label, push the image that passed and deploy a preview (real PBS, Authelia-gated).
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

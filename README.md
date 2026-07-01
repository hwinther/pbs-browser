# pbs-browser

A small internal web UI to **browse and restore individual files from client-side-encrypted
Proxmox Backup Server (PBS) snapshots**.

## Why this exists

Our app/PVC backups are made with `proxmox-backup-client` using a **client-side encryption keyfile
that PBS itself does not hold**. Because the server only ever sees ciphertext, the built-in PBS web
file-restore browser cannot open these host-type (pxar) snapshots — that is structural, not a missing
feature. The only way to pull one file out is the CLI with `--keyfile` on a host that has the key.
This app wraps that CLI behind a tiny web UI.

See the design note in the infra repo: `proxmox/docs/pbs-encrypted-backup-web-viewer.md`.

## Architecture

Single container, no database:

- **backend/** — ASP.NET Core 10 Minimal API. Wraps the `proxmox-backup-client` binary (invoked via
  an argument array — never a shell string) to list snapshots, dump the catalog (metadata only, cheap
  to decrypt) to build a file tree, and restore one file via a targeted `--pattern` extract. Also
  serves the built SPA from `wwwroot`.
- **frontend/** — Vite + React + TypeScript SPA: snapshot picker, archive picker, lazy file tree,
  per-file download. Built and copied into the backend image's `wwwroot`.

Auth is handled **outside** the app by the cluster's Traefik forward-auth (Authelia) on
`*.mgmt.wsh.no`; the app reads the injected `Remote-User` / `Remote-Email` headers only for an audit
log of who restored what.

The image is **linux/amd64 only** — Proxmox ships `proxmox-backup-client-static` for amd64 only.

## Endpoints

| Method | Path | Purpose |
| ------ | ---- | ------- |
| GET | `/api/snapshots` | list snapshots (`snapshot list --output-format json`) |
| GET | `/api/snapshots/files?snapshot=` | list archives in a snapshot |
| GET | `/api/catalog?snapshot=` | file tree (`catalog dump`, decrypted with the keyfile) |
| GET | `/api/download?snapshot=&archive=&path=` | restore + stream one file |
| GET | `/api/me` | echo the forward-auth identity headers |
| GET | `/healthz` | liveness/readiness |

## Develop

`proxmox-backup-client-static` is **Linux/amd64 only** — there is no Windows/macOS build — so anything
that shells out to it can't run natively off Linux. Pick the path that fits what you're working on:

### A. UI / API iteration — native, no binary (recommended, works on Windows)

The API ships a **fake PBS mode** that serves sample snapshots/catalog/downloads, so `dotnet run`
works anywhere with no binary and no live PBS. It's auto-enabled in Development when the binary is
absent; you can also force it with `PBS_FAKE=1`.

```powershell
# backend (http://localhost:8080) — fake data
$env:PBS_FAKE = "1"; dotnet run --project backend/src/Api/Api.csproj

# frontend (http://localhost:5173, proxies /api -> :8080)
cd frontend; npm install; npm run dev
```

### B. Exercise the real client — Docker (needs a reachable PBS)

Build the image (it contains the real binary) and run it with your PBS env + keyfile; point the Vite
dev server at it. `--platform linux/amd64` is required.

```powershell
docker build --platform linux/amd64 -t pbs-browser:dev .
docker run --rm -p 8080:8080 `
  -e PBS_REPOSITORY='user@pbs!token@host:8007:datastore' `
  -e PBS_PASSWORD='<token-secret>' -e PBS_ENCRYPTION_PASSWORD='<passphrase>' `
  -v ${PWD}\keyfile.json:/etc/pbs/keyfile:ro `
  pbs-browser:dev
# then in another shell: cd frontend; npm run dev
```

### C. Closest to prod — WSL2

In WSL2 (Linux/amd64) install `proxmox-backup-client-static` from the Proxmox pbs-client apt repo (see
the `Dockerfile` for the exact repo lines), then `dotnet run` normally against a real PBS.

Configuration (env vars, read by the API and passed through to the client binary):

| Var | Required | Notes |
| --- | -------- | ----- |
| `PBS_REPOSITORY` | yes | `user@pbs!token@host:8007:datastore` |
| `PBS_PASSWORD` | yes | API token secret |
| `PBS_ENCRYPTION_PASSWORD` | yes | passphrase that unlocks the keyfile |
| `PBS_FINGERPRINT` | if self-signed | server cert fingerprint |
| `PBS_NAMESPACE` | optional | e.g. `Production/k0s` |
| `PBS_KEYFILE` | optional | keyfile path (default `/etc/pbs/keyfile`) |
| `PBS_CLIENT_PATH` | optional | binary path (default `/usr/bin/proxmox-backup-client`) |

## Build the image

```bash
docker build --platform linux/amd64 -t ghcr.io/hwinther/pbs-browser/app:dev .
```

## Test

```bash
dotnet test backend/PbsBrowser.slnx
cd frontend && npm test
```

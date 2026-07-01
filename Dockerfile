# syntax=docker/dockerfile:1
#
# Single image for pbs-browser: Vite/React SPA + ASP.NET Core 10 API + the proxmox-backup-client
# binary it shells out to. linux/amd64 ONLY — Proxmox ships proxmox-backup-client-static for amd64
# only (see images/proxmox-pbs-backup-client in the infra repo for the same install pattern).

# 1) Build the SPA -> /src/frontend/dist
FROM node:24-bookworm-slim AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json* ./
RUN npm ci
COPY frontend/ ./
# Override the dev outDir (which points at the backend tree) to a self-contained dist.
RUN npm run build -- --outDir dist --emptyOutDir

# 2) Fetch the static PBS client (official Proxmox pbs-client apt repo, Debian Trixie)
FROM debian:trixie-slim AS pbs-client
ENV DEBIAN_FRONTEND=noninteractive
RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends ca-certificates wget; \
    wget -qO /usr/share/keyrings/proxmox-archive-keyring.gpg \
      https://enterprise.proxmox.com/debian/proxmox-archive-keyring-trixie.gpg; \
    printf '%s\n' \
      'Types: deb' \
      'URIs: http://download.proxmox.com/debian/pbs-client' \
      'Suites: trixie' \
      'Components: main' \
      'Signed-By: /usr/share/keyrings/proxmox-archive-keyring.gpg' \
      > /etc/apt/sources.list.d/pbs-client.sources; \
    apt-get update; \
    apt-get install -y --no-install-recommends proxmox-backup-client-static; \
    install -D /usr/bin/proxmox-backup-client /out/proxmox-backup-client; \
    rm -rf /var/lib/apt/lists/*

# 3) Publish the API, with the SPA baked into wwwroot
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY backend/ ./backend/
COPY --from=frontend /src/frontend/dist/ ./backend/src/Api/wwwroot/
RUN dotnet publish backend/src/Api/Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# 4) Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=backend /app/publish ./
COPY --from=pbs-client /out/proxmox-backup-client /usr/bin/proxmox-backup-client

ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    PBS_CLIENT_PATH=/usr/bin/proxmox-backup-client

# Non-root (the aspnet image ships an `app` user as $APP_UID=1654). Scratch restores go to /tmp.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "PbsBrowser.Api.dll"]

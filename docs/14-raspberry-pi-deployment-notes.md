# 14 - Raspberry Pi Deployment Notes

This project has its own local Pi diagnostic scripts. Do not rely on the `SwedesClanTracker` repository for future Pi access.

The existing `SwedesClanTracker` repository remains useful as a reference for deployment patterns, but scripts needed by this project should live in this repository.

Reference repository:

```text
C:\Users\Sebastian\source\repos\SwedesClanTracker
```

## Local Pi access helpers

Use:

```text
scripts/windows/pi/pi-common.ps1
scripts/windows/pi/test-pi-ssh.ps1
scripts/windows/pi/check-pi-db-readonly.ps1
scripts/windows/pi/inspect-pi-prereqs.ps1
```

Do not use ad hoc SSH for diagnostics when a helper exists.

The helper defaults currently resolve to:

```text
Pi host: 192.168.10.106
Pi user: sebastian
SSH key: ~/.codex/keys/swedesclantracker-pi/.codex_pi_ed25519
known_hosts: ~/.codex/keys/swedesclantracker-pi/.codex_known_hosts
```

The scripts first check for Event Planner-specific key paths and then fall back to the existing tracker key paths while the Pi setup is shared.

`check-pi-db-readonly.ps1` defaults to `swedesclantracker` because that is the readable database currently present on the Pi. Once the Event Planner database and read-only access exist, set:

```text
PI_READONLY_DATABASE=swedeseventplanner
```

If remote commands need quotes, pipes, SQL, regexes, or multiline logic, send base64-encoded script or SQL content and decode it remotely.

## Read-only inspection results

Read-only checks were run on June 20, 2026.

Results:

```text
SSH: working
read-only PostgreSQL role: codex_ro works against swedesclantracker
non-interactive sudo: available
journal access: available via sudo
env diagnostics: /etc/swedesclantracker/*.env readable via sudo
nginx: active and enabled
postgresql: active and enabled
swedesclantracker-api: active and enabled
swedesclantracker-worker: active and enabled
LAN dashboard probe: HTTP 200
API localhost probe: HTTP 401
```

Observed Pi environment:

```text
OS: Debian GNU/Linux 13 (trixie), aarch64
Kernel: 6.18.29+rpt-rpi-v8
.NET runtime: Microsoft.NETCore.App 10.0.8
ASP.NET Core runtime: Microsoft.AspNetCore.App 10.0.8
PostgreSQL: 17.10
nginx: 1.26.3
rsync: 3.4.1
jq: 1.7
```

Network/service shape:

```text
nginx listens on port 80
ASP.NET Core API listens on 127.0.0.1:5166
PostgreSQL listens on 127.0.0.1:5432 and ::1:5432
SSH listens on port 22
```

Existing app paths:

```text
/opt/swedesclantracker/api
/opt/swedesclantracker/worker
/opt/swedesclantracker/frontend
/etc/swedesclantracker/api.env
/etc/swedesclantracker/worker.env
```

The existing env files are mode `600`.

## Reusable deployment pattern

The existing tracker deploy flow:

```text
publish API for linux-arm64
publish worker for linux-arm64
build Vite frontend
copy artifacts to deploy/pi
upload artifacts to /tmp on the Pi
stop systemd API/worker services
rsync artifacts into /opt/<app>/
chown app files to the app system user
start services
reload nginx
run verification
```

This project should use the same general pattern, adjusted for the event planner app name and project layout.

Deployment scripts for this repo should be Windows PowerShell scripts that run locally on Windows and deploy to the Pi over SSH/SCP/rsync. Do not create Linux deployment scripts unless there is a specific future need.

## Recommended Event Planner deployment shape

Use project-specific names and paths so this app can coexist with `SwedesClanTracker`.

Recommended names:

```text
system user: swedesevents
application root: /opt/swedeseventplanner
configuration root: /etc/swedeseventplanner
database: swedeseventplanner
database user: swedesevents
API service: swedeseventplanner-api
worker service: swedeseventplanner-worker
nginx site: swedeseventplanner.conf
```

Recommended app paths:

```text
/opt/swedeseventplanner/api
/opt/swedeseventplanner/worker
/opt/swedeseventplanner/frontend
/etc/swedeseventplanner/api.env
/etc/swedeseventplanner/worker.env
```

Recommended defaults:

```text
API bind: 127.0.0.1 on a port different from 5166
PostgreSQL: localhost only
nginx: serve frontend and proxy /api to the backend
frontend production API calls: relative /api paths
```

Swagger/OpenAPI should not be publicly exposed in production by default.

If the app is running on the Pi in development/testing mode, Swagger should be reachable from the Windows development environment without public exposure. Prefer an SSH tunnel or another explicit development-only access path.

Do not hardcode the Pi IP, local LAN subnet, credentials, production URLs, or Linux paths into application code.

Deployment scripts may contain safe defaults and variables, but paths, users, service names, ports, and LAN CIDR should be configurable.

## Nginx considerations

The current Pi already uses port 80 for `SwedesClanTracker`.

Before finalizing Event Planner nginx deployment, decide whether it should be exposed as:

```text
a separate hostname
a path under the same host
a different LAN port
a replacement for the current default site
```

Do not overwrite the existing tracker nginx site without an explicit decision.

## Script policy

Do not run deployment scripts until explicitly asked.

Safe read-only diagnostics may use:

```text
test-pi-ssh.ps1
check-pi-db-readonly.ps1
inspect-pi-prereqs.ps1
custom read-only helper commands through pi-common.ps1
```

When this project gets its own scripts, keep the same safety properties:

```text
BatchMode SSH
known_hosts pinning
short connection timeout
NoPause option
ShouldProcess for deploy scripts
read-only health checks separated from deploy scripts
no secrets in source control
```

# Raspberry Pi Scripts

These scripts are safe diagnostic helpers for the Swedes Event Planner Raspberry Pi target.

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/windows/pi/test-pi-ssh.ps1 -NoPause
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/windows/pi/check-pi-db-readonly.ps1 -NoPause
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/windows/pi/inspect-pi-prereqs.ps1 -NoPause
```

They use `BatchMode=yes`, pinned `known_hosts`, and an SSH timeout through `pi-common.ps1`.

`check-pi-db-readonly.ps1` defaults to `swedesclantracker` because that is the readable database currently present on the Pi. After the Event Planner database exists, run it with:

```powershell
$env:PI_READONLY_DATABASE="swedeseventplanner"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/windows/pi/check-pi-db-readonly.ps1 -NoPause
```

Deployment scripts intentionally do not exist yet. Add them after the backend, worker, frontend build output, service names, and health endpoints exist.

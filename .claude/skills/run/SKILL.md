---
name: run
description: Launch and verify the BrownEvents app (backend + frontend + postgres) locally via Podman, since Docker Desktop requires manual Gatekeeper approval on this machine.
---

# Running BrownEvents locally

This repo ships a `docker-compose.yml`, but Docker Desktop is not
usable non-interactively here (Gatekeeper blocks it, install got
uninstalled once already). **Podman is the working path** — it's
already installed via Homebrew along with `podman-compose`.

## One-time setup (skip if already done)

```bash
brew install podman podman-compose   # if not installed
podman machine init                  # if no machine exists yet
podman machine start                 # if machine is not running
```

Check machine state first: `podman machine list`. If it shows
`Currently running`, skip `init`/`start`.

## Launch

From the repo root:

```bash
podman-compose up --build -d
```

This builds and starts three containers: `shazam_postgres_1`,
`shazam_backend_1`, `shazam_frontend_1`.

## Verify

```bash
podman ps -a   # all three should be "Up", postgres "healthy"

curl -s http://localhost:5000/api/conferences | head -c 300   # backend seed data
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5173  # expect 200
```

| Service      | URL                            |
|--------------|---------------------------------|
| Frontend     | http://localhost:5173           |
| Backend API  | http://localhost:5000/api       |
| Swagger UI   | http://localhost:5000/swagger   |
| Postgres     | localhost:5433                  |

## Stop / clean up

```bash
podman-compose down
```

## Notes

- `docker` / `docker-compose` CLI commands are NOT available in this
  environment's sandbox — always use `podman-compose`, not
  `docker-compose` or `docker compose`.
- If `podman-compose up` fails with a connection error, the machine
  is probably stopped — run `podman machine start` and retry.

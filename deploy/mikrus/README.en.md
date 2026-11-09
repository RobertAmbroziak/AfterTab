# Optional mikr.us / VPS deployment

[Polski](README.md)

This option runs your own frontend + API + PostgreSQL instance on a VPS.
No Azure account is required. Pushing to GitHub does not start containers or deploy
the application. The API defaults to `SqlServer`; Compose selects `PostgreSql`
through environment variables.

This model version requires an **empty database** on either engine. Before updating
an older installation, follow the planned [schema reset](../../after-tab-api/docs/database.en.md).
This also applies when eventually publishing the new API to Azure SQL; pushing code does not reset it.

## Components

- `frontend`: built PWA files served by Nginx; `/api/` and OAuth callbacks go to the API.
- `api`: ASP.NET Core using PostgreSQL through the nonadministrative `aftertab_app` login.
- `postgres`: PostgreSQL 17 with no published database port.
- `migrate`: a one-shot helper applying migrations as `aftertab_owner` before API startup.

The frontend uses relative `/api` URLs, so its image contains no Azure or target
hostname. Only the frontend is published, by default on the VPS's `127.0.0.1:8080`.
You need Linux containers, Docker Compose v2, and a **public HTTPS domain**.
Terminate HTTPS at a host reverse proxy or a mikr.us proxy in front of this stack.
The stack does not provision certificates or domains. Secure cookies and PWA
features require HTTPS. Kubernetes and Azure are not required.

## Configure and start

Run these Linux shell commands from `deploy/mikrus`:

```sh
cp .env.example .env
chmod 600 .env
openssl rand -hex 32
openssl rand -hex 32
```

Put two different generated passwords in `DB_OWNER_PASSWORD` and `DB_APP_PASSWORD`.
Set `PUBLIC_ORIGIN` to your HTTPS origin without a trailing slash, for example
`https://aftertab.example.com`. Real configuration stays in the ignored `.env`
and is excluded from image build contexts. Hex passwords avoid connection-string
and Compose interpolation delimiters. Compose reads `.env`; the API does not.

If `172.30.37.0/24` is already used, change `DOCKER_SUBNET`, `DOCKER_GATEWAY`,
`FRONTEND_IP` and `TRUSTED_PROXY_CIDR` together. The API trusts forwarded headers
only from `FRONTEND_IP`, and this behavior is enabled only by Compose settings.

```sh
docker compose config --quiet
docker compose up -d --build
docker compose ps -a
curl -f http://127.0.0.1:8080/api/health
```

Compose waits for PostgreSQL readiness and successful migrations before starting
the API and frontend. `migrate` exiting with code 0 is normal. Migration failure
blocks API startup. Health only checks the process, not database connectivity;
also verify creating a guest and an event through the public HTTPS URL.

Building images needs more RAM than running them. For a small VPS, build on your
computer/CI and deliver images rather than compiling on the server. Choose a
mikr.us plan supporting Docker with enough resources for the API and database.

## HTTPS and client addresses

`host-nginx.example.conf` shows a host proxy for a VPS with ports 80/443 and an
existing certificate. The default trusted proxy CIDR is the Docker gateway used
by that host proxy.

For an external mikr.us proxy, adapt `HTTP_BIND` and `HTTP_PORT` to the assigned
port and connectivity. `HTTP_BIND=0.0.0.0` publishes IPv4; `HTTP_BIND=[::]` needs
working Docker IPv6 port mapping. Restrict access to that port to your proxy.
Set `TRUSTED_PROXY_CIDR` to its actual address/CIDR, never `0.0.0.0/0`.
The proxy must preserve the public `Host` and set `X-Forwarded-For` to the client
address. Otherwise rate limits may group all visitors under one proxy address.

The frontend forwards scheme `https` to the API because this is a private HTTP
hop behind TLS termination. Do not expose this entry point as a public HTTP site.
`PUBLIC_ORIGIN` configures allowed origins and authentication links.

Set optional SMTP (`MAIL_*`) for registration/password-reset emails and optional
Google/Facebook credentials. Register callbacks for this deployment:
`PUBLIC_ORIGIN/signin-google` and `PUBLIC_ORIGIN/signin-facebook`.
The default image does not publish an APK or Android Digital Asset Links for the new domain.

## Persistence, updates and logs

Named volumes `postgres-data`, `api-logs` and `api-keys` keep the database,
Serilog files and Data Protection keys across restarts, container replacement
and ordinary `docker compose down`. **`docker compose down -v` deletes them.**
Keep the Compose project name (`aftertab`) to reuse the same volumes. Volumes are
not backups and do not protect against losing the VPS.

Back up before updating, then after pulling changes:

```sh
docker compose build
docker compose stop frontend api
docker compose run --rm migrate
# Continue only after migration succeeds:
docker compose up -d
docker compose logs --tail=100 api migrate
```

PostgreSQL initialization scripts/password variables apply only to a new empty
volume. Editing `.env` does not change an existing role's password; use
`ALTER ROLE`, update `.env` and recreate affected containers. Do not automatically
change the PostgreSQL major image version: plan an upgrade/backup restore.

Use `docker compose logs -f api` and `docker compose logs migrate`. Docker keeps
three 10 MB log files per service; Serilog rotates its files separately.
Persisted `api-keys` prevent container replacement from resetting encryption keys.

Linux backup example (contains user data; the directory is ignored):

```sh
mkdir -p backups
chmod 700 backups
docker compose exec -T postgres pg_dump -U aftertab_owner -d aftertab -Fc > backups/aftertab.dump
```

Check command success and test restoration; also keep a copy outside the VPS.
Restore into an empty database with a compatible schema before starting the API.
Back up `api-keys` separately if encrypted-cookie continuity is needed.
Moving existing Azure SQL data to PostgreSQL remains a separate operation.

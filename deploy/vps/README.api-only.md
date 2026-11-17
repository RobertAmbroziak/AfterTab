# API + MCP + PostgreSQL with the frontend on Azure Static Website

This stack deliberately does not build or serve the frontend. It creates an
independent, empty PostgreSQL volume on the VPS, applies the PostgreSQL baseline
migrations, and exposes the API/MCP through a small Nginx edge container. The
Azure frontend continues to use its existing deployment, with its API URL changed
to the VPS origin when the backend is ready.

## Configure

From this directory on the VPS, copy `.env.api-only.example` to `.env`. Generate
two distinct database passwords with `openssl rand -hex 32`. Set:

- `PUBLIC_API_ORIGIN`: the final public HTTPS origin for this API, with no trailing slash (for example, `https://api.example.com`).
- `FRONTEND_ORIGIN`: the exact browser origin serving the Azure frontend, with no path.
- `CLOUDFLARE_TUNNEL_TOKEN`: token for a remotely-managed Cloudflare Tunnel.
- The generated passwords and any optional Google/Facebook/SMTP settings.

Do not use the Azure SQL connection string here. This compose file selects
PostgreSQL explicitly. On first start the separate `aftertab-api` project creates
a fresh named database volume and migrations create its schema. No Azure SQL data
is read, copied, or reset.

The API, PostgreSQL, Nginx edge, and Cloudflare Tunnel have no host-published
ports. The Tunnel makes an outbound connection, so the VPS needs no inbound web
port or public IPv4. In Cloudflare, publish `api.example.com` to
`http://api-edge:8080`. TLS terminates at Cloudflare. The Nginx edge sets the
public scheme to HTTPS and forwards Cloudflare's visitor IP. Its private fixed IP
is the only forwarded-header proxy trusted by the API.

## First start

Create a remotely-managed tunnel in Cloudflare under **Networking > Tunnels**
and copy its connector token. Add a published application route with hostname
`api.example.com` and service `http://api-edge:8080`. Create `.env`, enter the
tunnel token, origins, and generated database passwords, then run:

```sh
docker compose -f compose.azure-frontend.yaml config --quiet
docker compose -f compose.azure-frontend.yaml up -d --build
docker compose -f compose.azure-frontend.yaml ps -a
docker compose -f compose.azure-frontend.yaml logs --tail=100 migrate api api-edge
```

Verify
`GET https://api.example.com/api/health` and MCP discovery before changing the
Azure frontend. Cloudflare Tunnel keeps the VPS web ports closed and encrypts the
connection from the VPS to Cloudflare.

## Connect the Azure frontend

Set these variables in the Azure Static Web Apps build environment, then rebuild
and publish the frontend:

```text
VITE_API_BASE_URL=https://<api-origin>
VITE_MCP_ENABLED=true
```

The API allows only `FRONTEND_ORIGIN` through CORS. If Google or Facebook login is
enabled, register the API callbacks `PUBLIC_API_ORIGIN/signin-google` and
`PUBLIC_API_ORIGIN/signin-facebook` with the providers. `Authentication:FrontendOrigin`
continues to point to the Azure frontend, so browser login and MCP consent return
there.

Keep using this compose filename and project name so the new database, API logs,
and Data Protection keys persist. Back up PostgreSQL and `/app/keys` separately.
Do not use `docker compose down -v` after deployment; it deletes the new database
and persistent keys.

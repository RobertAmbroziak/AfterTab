# AfterTab MCP

[Polski](README.md) | **English**

This MCP module exposes AfterTab events and settlements to AI clients using
standard Streamable HTTP. It is a separate project in the existing solution,
hosted by the API at `/mcp`. It needs no additional App Service, AI account or
OpenAI API key on the AfterTab server. Configure your server address in the client.

The server supports OAuth 2.1 with client registration, PKCE S256, consent in
AfterTab, and token rotation. Users link their account through an AfterTab browser
consent page without copying a token into ChatGPT. Manual Bearer tokens remain
available for Codex and other technical clients. The module never accepts an
account name or password from the model. Custom MCP servers are available in
ChatGPT on the web; the ChatGPT mobile app does not currently offer adding one.

## Enable in your deployment

The integration defaults to `Mcp:Enabled=false`. Both `/mcp` and token management
are unavailable while disabled. To enable:

1. Apply all pending MCP migrations (`McpIntegration`, `AddMcpUserInstructions`,
   `McpOAuth`) for your database provider. They add integration and preference
   tables without resetting data. See the migration commands in the
   [main README](../../../README.en.md).
2. Set `Mcp__Enabled=true` in the API environment, such as App Service configuration.
3. Set `VITE_MCP_ENABLED=true` when building the frontend to show the token panel.
4. Set `Mcp__PublicOrigin` to the public API origin, for example
   `https://api.example.com`. This is the origin for `/mcp`, OAuth endpoints and
   `/.well-known` documents. Also set the existing
   `Authentication__FrontendOrigin` to the AfterTab app origin; the consent page
   must use that same API and the user's session.
5. Expose `/mcp` over HTTPS on your API domain. Reverse proxies must forward
   `Authorization` and `Mcp-*` headers, support POST and avoid buffering SSE responses.
   Do not log Authorization headers or responses containing token secrets.

For [Docker Compose](../../../deploy/mikrus/README.en.md), set `MCP_ENABLED=true`
in your private `.env` and rebuild images using the deployment instructions.
The supplied nginx forwards `/mcp` to the API. The default `false` keeps the
integration disabled. Enabling MCP does not require changing the database provider
on Azure.

For local development run `dotnet user-secrets set "Mcp:Enabled" "true"` in
`after-tab-api/src/AfterTab.API`. Locally, the API origin can be inferred from an
HTTPS request; set `Mcp:PublicOrigin` in public deployments. Trust your development
certificate or use your deployed HTTPS endpoint rather than disabling certificate
verification in the client.

## Account and token

Once enabled, sign into AfterTab normally. OAuth consent links the account signed
in in the browser to the MCP client; you can disconnect it from **AI integrations**
in the account menu. Apply all pending MCP migrations for SQL Server or PostgreSQL.
Access tokens expire after 10 minutes; refresh tokens rotate and expire after 30 days. Revoking
access invalidates the client's tokens.

Manual Bearer tokens remain available for Codex testing and other clients. Their secret is shown only
when issued and kept only in the open panel's memory. Read access is the default;
enable write access to create events and change expenses. Expiry defaults to
30 days and can be set from 1 to 90 days. Each user may have 20 active tokens.
Revoke tokens in the same panel. Signing out of the PWA does not revoke integrations.

A token acts as the account that issued it, including a remembered guest.
Supplying a name alone never grants access to someone else's account. The AfterTab
account must be active and its display name confirmed. ChatGPT also requires a
signed-in ChatGPT account. AfterTab does not need to stay open after linking, but
its API must remain reachable online. OAuth uses an account already signed in to
AfterTab; MCP does not create an anonymous account or sign in using only a name.

Only the SHA-256 hash is stored. Integration tokens cannot access ordinary PWA
endpoints, and PWA sessions cannot authenticate `/mcp`. Existing business services
enforce event membership and expense permissions. A token does not grant owner
privileges. Blocking/deleting the account, expiry or revocation prevents new MCP
requests; an already running request can still finish.

Management is also available using an ordinary AfterTab session:

| Method and path | Action |
| --- | --- |
| `GET /api/profile/mcp-tokens` | Own token metadata without secrets or hashes |
| `POST /api/profile/mcp-tokens` | `{ "name": "Codex", "canWrite": false, "expiresInDays": 30 }`; returns a one-time `token` and `details` |
| `DELETE /api/profile/mcp-tokens/{id}` | Revoke an owned token |

## Connect Codex

Keep the secret outside Git and model conversations. Supply `AFTERTAB_MCP_TOKEN`
to the process that launches Codex. PowerShell can read it without putting the
value in command history:

```powershell
$mcpSecret = Read-Host 'AfterTab MCP token' -AsSecureString
$env:AFTERTAB_MCP_TOKEN = [System.Net.NetworkCredential]::new('', $mcpSecret).Password
codex mcp add aftertab --url https://api.example.com/mcp --bearer-token-env-var AFTERTAB_MCP_TOKEN
codex
# After use:
Remove-Item Env:AFTERTAB_MCP_TOKEN
```

Replace the example domain with your API address. Equivalent private
`~/.codex/config.toml` entry:

```toml
[mcp_servers.aftertab]
url = "https://api.example.com/mcp"
bearer_token_env_var = "AFTERTAB_MCP_TOKEN"
```

This is client configuration, not a file to commit to this repository. Check
available tools after starting Codex. Client approval settings govern confirmation
of changes; tool annotations do not replace approval or server authorization.
Other clients supporting MCP Streamable HTTP with OAuth can link their account
through the same AfterTab consent page.

Example: “Create a Dinner event with virtual participants Alex and Sam. Add pizza
for PLN 100: 33% each for me, Alex and Sam, assigning the remainder to Alex.
Show me the preview first.” The model reads actual participant IDs, previews the
split, then saves the agreed expense.

## Tools

| Tools | Scope |
| --- | --- |
| `get_current_user`, `get_user_instructions`, `list_events`, `get_event` | Token account, personal AI instructions, own events (10 per page), event details and participants |
| `create_event` | Create an event without returning an invitation secret |
| `add_virtual_participant`, `rename_virtual_participant`, `remove_virtual_participant` | Owner-only management in an open event; removal requires an unused participant |
| `list_expenses`, `preview_expense`, `create_expense` | Read, calculate without saving, atomically save an expense and shares |
| `update_expense_split`, `set_expense_balance`, `set_expense_payer` | Replace shares, assign/undo explicit remainder, change payer; existing permissions and `rowVersion` apply |
| `get_settlement`, `close_event`, `reopen_event` | Settlement plan; owner-only close/reopen |
| `mark_transfer` | Set/undo the authenticated user's `Sent` or `Received` mark in the current closed settlement |

Personal assistant instructions are saved in the AI Integrations panel and belong
to the AfterTab account, not an individual token. MCP exposes them only to a
connection authenticated as that same account. Without a personal rule, the
assistant should ask when it cannot identify which participant the user means.

Every action needs a defined tool. The model has no unrestricted API or database
access. This version has no expense deletion/renaming, invitation joining,
participant merging or account-management tools. Join an existing event in
AfterTab first. Opening an event reads it; reopening a settlement is a separate tool.

Money uses integer grosze: `100 PLN = 10000 amountMinor`. Percentage shares use
basis points: `33% = 3300 percentageBps`. `splitMethod` is `Amount` or `Percentage`.
Each percentage share has `amountMinor=0`; each amount share has `percentageBps=0`.
`balanceParticipantId` must identify someone already in the shares. Omitting it
leaves the remainder unassigned. The server never picks a remainder recipient automatically.

Editing requires the current `rowVersion` from the expense list. Payment marking
requires the current settlement version, exact participant pair and amount.
Marks are user statements, not bank confirmations. User-entered event text is
data, not instructions to the AI.

## Retries and maintenance

Each new intended write needs a fresh `operationId` UUID. An identical retry must
keep both its ID and parameters: the server returns the saved result without
repeating the change. Current token validity and event membership are checked
even on replays. IDs are unique per account across its tokens. Reusing an ID
with different parameters fails.

A durable reservation is committed before changing data. If execution fails
between committing the change and returning its result, the outcome can be
uncertain. The server refuses to rerun that operation. Read the current state
to establish what was saved. This also applies to rejected operations after
reservation. Never automatically try a fresh ID following an error.

`McpOperations` stores results containing event/settlement data. This version
has no automatic table cleanup; backup and retention policies must cover it too.
Deleting a record removes duplicate protection for its old ID. Do not prune it
while retries are active. MCP is limited to 120 requests per minute per IP;
adjust the limit for shared proxies and configure trusted proxies correctly.

Unit tests cover tool permissions. HTTP/SQLite tests exercise the protocol,
public GUIDs, creation/retries, read-only tokens, revocation, expiry and account
isolation. Provider-model tests verify both sets of migrations.

```powershell
cd after-tab-api
dotnet test AfterTabApi.sln
```

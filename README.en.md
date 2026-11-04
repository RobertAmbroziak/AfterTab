# AfterTab

**[Polski](README.md)** | **English**

Package your own deployment for Android: [Android guide](after-tab-android/README.en.md).

AfterTab is a PWA for sharing expenses. Create events, invite friends, record who
paid and how costs are split, then see who needs to pay whom.

Run your own instance using the instructions below. Domains under `example.com`,
email addresses ending in `@example.com`, and values in `<...>` are placeholders to replace.

## Languages

Use **PL / EN** in the header to change language without signing out, losing an
invitation or clearing a form. On the first visit, the browser's primary language
selects Polish for Polish settings and English for other languages. If no language
is available, Polish is used. Your manual choice is remembered on the device and
takes priority on later visits. Amounts remain in PLN; number and date formatting
follows the selected language. Event names, descriptions and participant names are
user content and are not translated.

Privacy, data deletion and terms pages contain both languages at their original
URLs. `?lang=pl` and `?lang=en` select a version. Without JavaScript, both complete
versions are readable. Switching languages does not change the Google/Meta legal
URLs or OAuth callbacks configured for your deployment. Account emails use the language selected when requesting them.

## Run locally

GitHub Actions runs **Develop CI** on every push to `develop`: it installs
dependencies, builds the backend and frontend, and runs the .NET and Node.js
unit tests. Results and logs appear in the repository's **Actions** tab.
The workflow does not run for `main`, run integration tests, require a database,
or deploy the application.

Optional frontend + API + PostgreSQL deployment on mikr.us/VPS:
[Docker Compose](deploy/mikrus/README.en.md). Azure is an optional hosting choice.

Optional footer links use `VITE_GITHUB_URL` and `VITE_ANDROID_APK_URL`. Blank values
hide the corresponding link, so another deployment does not have to point to the
original author's repository or downloads. Keep production values outside Git,
for example in `after-tab-app/.env.production.local`.

The APK can be served by your frontend host, including Azure Static Web Apps. For a URL such as
`/downloads/App-1.0.0.apk`, set `AFTERTAB_ANDROID_APK_PATH` to the local APK file
(absolute or relative to `after-tab-app`). `npm run build` copies only the APK to
`dist/downloads`, generates a SHA-256 checksum, and verifies the file is present.
The binary stays outside Git and the PWA offline cache. Use a new filename for
each version because downloads have immutable cache headers. Keep the complete
PWABuilder ZIP and signing credentials private.

Requirements:

- SQL Server or PostgreSQL. The default example below uses SQL Server Express LocalDB on Windows; PostgreSQL also allows running the backend on Linux.
- .NET SDK 9.0.3xx, as specified in `after-tab-api/global.json`.
- Node.js 22.12 or later and npm.
- Visual Studio users: VS 2022 17.14 with ASP.NET tools.

The repository contains two projects:

```text
after-tab-api/    .NET and EF Core backend; AfterTabApi.sln
after-tab-app/    React, Vite and PWA frontend
```

### Backend and local database

This release requires an **empty database**. Existing installations must follow the
[database reset instructions](after-tab-api/docs/database.en.md) before updating.

Open PowerShell in the repository root:

```powershell
cd after-tab-api
$env:Database__Provider = 'SqlServer'
$env:ConnectionStrings__AfterTab = 'Server=(localdb)\MSSQLLocalDB;Database=AfterTab;Trusted_Connection=True;TrustServerCertificate=True'
$env:Cors__AllowedOrigins__0 = 'http://localhost:5173'
dotnet restore
dotnet tool restore
dotnet dev-certs https --trust
dotnet ef database update --project src/AfterTab.DAL.EF --startup-project src/AfterTab.API
dotnet run --project src/AfterTab.API --launch-profile https
```

Accept the local HTTPS certificate on first use. The migration command creates or
updates the local database. Check the connection string before applying migrations
to any other database.

- Local database: `AfterTab` on `(localdb)\MSSQLLocalDB`, Windows authentication.
- API: `https://localhost:7180`.
- Process health: `https://localhost:7180/api/health`; this does not check the database.

Keep the backend terminal open.

### Your deployment URLs

The examples below use two public HTTPS origins. Replace them for your installation:

| Purpose | Example to replace |
| --- | --- |
| Frontend / PWA | `https://app.example.com` |
| API | `https://api.example.com` |
| Sender approved by your SMTP service | `no-reply@example.com` |
| Your instance administrator's contact | `support@example.com` |

In the **API** environment, set `Authentication__FrontendOrigin` and
`Cors__AllowedOrigins__0` to your public frontend origin, without a trailing `/`.
When building the **frontend**, set `VITE_API_BASE_URL` to the API origin without
a trailing `/api`; the client adds endpoint paths. Choose your own Azure resource
names, subscription, database, SMTP account and OAuth clients.

For one public origin, such as Docker Compose, both the frontend and API entry
can use `https://app.example.com`. Leave `VITE_API_BASE_URL` empty, as Compose does;
the client adds `/api` itself. OAuth callbacks must still reach the backend at
`https://app.example.com/signin-google` and `https://app.example.com/signin-facebook`,
outside `/api`. The reverse proxy forwards these routes to the API.
Legal pages use the frontend origin. Configure Google/Meta with your actual URLs,
not the example domains.

### Select SQL Server or PostgreSQL at deployment

The same API build supports both engines. `Database:Provider` defaults to
`SqlServer` in `appsettings.json`. Provider selection stays the same, but this
release requires a fresh empty schema, including on Azure SQL. Read the
[numeric keys and database reset instructions](after-tab-api/docs/database.en.md)
before updating an existing installation. Configure these environment variables on the server or in the
deployment pipeline, outside Git:

| Environment setting | SQL Server / Azure SQL | PostgreSQL |
| --- | --- | --- |
| `Database__Provider` | `SqlServer` | `PostgreSql` |
| `ConnectionStrings__AfterTab` | SQL Server connection string | PostgreSQL connection string |

Restart the API after changing these settings. An unknown provider prevents
startup instead of silently selecting a different engine. Each process uses one
engine; switching the setting does not transfer data between databases.

For PostgreSQL, first provision a server, database and user. Then, from
`after-tab-api` after `dotnet restore` and `dotnet tool restore`:

```powershell
$env:Database__Provider = 'PostgreSql'
$env:ConnectionStrings__AfterTab = 'Host=localhost;Port=5432;Database=AfterTab;Username=aftertab;Password=<password>'
dotnet ef database update --project src/AfterTab.DAL.Migrations.PostgreSql --startup-project src/AfterTab.API
dotnet run --project src/AfterTab.API --launch-profile https
```

`<password>` is a placeholder. Keep real credentials in environment configuration
or a secret store. Configure production TLS according to your database provider
and run migrations with a schema-authorized account. The running API account
does not need permission to create tables.

SQL Server migrations are in `src/AfterTab.DAL.EF/Migrations`. PostgreSQL has its own
migration project, `src/AfterTab.DAL.Migrations.PostgreSql`. Both engines now start
with `InitialNumericKeys`, replacing the previous migration history. It requires
an empty database and does not convert existing GUID-based data. Every subsequent model change needs a migration
for each engine, for example:

```powershell
dotnet ef migrations add ChangeName --project src/AfterTab.DAL.EF --startup-project src/AfterTab.API -- --Database:Provider SqlServer
dotnet ef migrations add ChangeName --project src/AfterTab.DAL.Migrations.PostgreSql --startup-project src/AfterTab.API -- --Database:Provider PostgreSql
```

Set the matching engine's connection string for each command. Never apply SQL
Server migrations to PostgreSQL. The API does not migrate on startup. The
deployment tool also supports both engines through `--migrate`; see the
[deployment instructions](after-tab-api/tools/AfterTab.Deployment/README.md).

Concurrency protection keeps the API's Base64 version format. SQL Server uses
native `rowversion`; PostgreSQL uses a token refreshed by EF on writes. Direct
PostgreSQL expense updates outside EF must refresh that token as well. Primary and
foreign keys are `bigint` (`long` in C#), generated using a HiLo sequence. Public
user, session, event, participant and expense identifiers remain GUIDs with unique
indexes; relationships use numeric IDs. See [database setup](after-tab-api/docs/database.en.md)
for reset steps and sequence permissions.

Run configuration and generated-SQL tests with `dotnet test AfterTabApi.sln`.
Real-server tests require `AFTERTAB_TEST_SQLSERVER` and `AFTERTAB_TEST_POSTGRESQL`
connection strings to local test servers with create/drop database permissions.
They create and delete their own randomly named `aftertab_test_*` databases;
without those variables, the two real-server cases are skipped.

### Frontend

Open a second PowerShell in the repository root:

```powershell
cd after-tab-app
$env:VITE_API_BASE_URL = 'https://localhost:7180'
npm ci
npm run dev -- --port 5173 --strictPort
```

Open `http://localhost:5173`. Stop any previous instance if the port is occupied.
On later runs, set the environment variables again and start `dotnet run` and
`npm run dev`. Restore dependencies and apply migrations when relevant project
changes require them. Stop either process with Ctrl+C. Local and hosted databases
are separate; data is not transferred automatically.

## User guide

### Guests and registered accounts

Choose **CONTINUE AS GUEST**, enter a name and confirm. **RETURN AS GUEST** restores
a guest remembered by this browser. The account menu lets you change your name,
sign out or forget a guest. Signing out preserves the ability to return. Forgetting
a guest or clearing browser data can remove access; entering the same name does
not recover the same identity. These actions do not delete server data.

Choose **SIGN IN / CREATE ACCOUNT** for an email account or an available Google or
Facebook provider. Email registration requires a name and a 12–128 character
password. Confirm the email address with the one-time link, valid for 24 hours,
before signing in. Password recovery links last 30 minutes. Resetting a password
revokes previous sessions and other reset links; the link itself does not sign you
in. After five incorrect passwords, the account is locked for 15 minutes. Forms
also have request limits. Recover Google/Facebook passwords with the provider.

A guest can use **Keep account / Sign in**. Registering a new account preserves
the guest's identity, events and expenses. First use of a new Google/Facebook
account may also preserve that guest. Signing in to an existing account opens its
events and does not automatically merge accounts. New social-login users confirm
or edit their name. Registered accounts can access the same events on another device.

Sign-in can be remembered for up to a year using a secure HttpOnly API cookie.
The app also keeps an API session and a restoration hint in this browser. After
session expiry, a short API redirect can restore access. **Sign out** removes the
remembered registered-account login in this browser. Clearing cookies, revocation
or expiry requires signing in again.

### Events and invitations

Create an event with a name and optional description. Copy and keep the generated
invitation link; share it only with intended participants. A recipient previews
the invitation, signs in and confirms joining. Invitations survive sign-in. A
messaging app's browser may use a separate session. A link or its 64-character code
can also be pasted into **Join with a link or code**.

The owner can generate a new link or revoke the current one. Previous links then
stop working; existing participants stay in the event. A closed event cannot be
joined. **My events** lists 10 events per page, newest first, with Previous/Next
navigation and Open/Closed indicators.

### Participants without accounts

The owner of an open event can add someone without a phone or account. They can
share expenses and be selected as the payer, but cannot sign in through this entry.
Adding someone does not alter existing expense splits. Their name can be edited.
An unused entry without expenses, shares or payment marks can be removed.

If that person later joins through an invitation, the owner can merge the entry
without an account with the joined participant. Check the selected people before
confirming: their expenses and shares are combined and this cannot be undone in
the app. People are never merged automatically by name.

### Expenses and splits

Add an expense name and amount. The event owner can select another participant as
the payer; otherwise the author is the payer. Choose who shares the expense and
split it by amount or percentage. Values accept a decimal comma or dot, with up
to two decimal places. The preview shows allocated shares, **Others** and the total.

An unallocated amount goes to **Others**. **Assign remainder** allocates it to a
selected person included in the split. Selecting another person moves the same
remainder; selecting the current person again removes the assignment. Changing
the amount, shares, selected people or split method clears the pending remainder
selection. Saved remainder assignments can be changed by the payer or event owner.
The expense author can edit its split subject to permissions; the owner can change
the payer without changing authorship. Concurrent edits require refreshing data.

### Settlements and payment marks

The settlement offsets mutual debts to simplify transfers. If A owes B PLN 12 and
B owes A PLN 3, the result is one PLN 9 transfer. Your amount owed or receivable
appears above the expenses. Desktop shows expenses and settlement side by side;
mobile uses **Expenses / Settlement** tabs.

- **Preview** follows saved data. Refresh manually or wait for the automatic
  15-second update while the screen is visible.
- **Close and settle** finalises the current plan and locks adding expenses,
  editing splits and joining. Only the owner can close it, after every expense's
  **Others** amount reaches zero and any expense form has been saved or cancelled.
- In a final settlement, the sender can mark **Sent** and the recipient **Received**.
  Each edits only their own mark. Both marks are private to those two parties and
  are statements, not bank confirmations.
- **Reopen** allows changes. Closing again creates a new settlement version.
  Earlier marks remain in the parties' private history. The new plan **does not
  automatically deduct previous payments**; check the history before paying again.

### Install the PWA

Use the install button in the header. Supported Android and desktop browsers show
an installation prompt; otherwise the app shows instructions. On iPhone, use the
browser's **Share → Add to Home Screen** menu. Signing in, saving and fetching
settlements requires the API. The first connection after inactivity can take up
to two minutes. Use **Retry connection** after a timeout.

## Configure registered accounts

### Local migration and test emails

Apply local migrations using the startup instructions. `InitialNumericKeys`
includes one-time account operations, sign-in lockout fields and an event-list index.
It replaces the old migration history and requires a reset for existing installations.
To test confirmation and reset emails without SMTP, in the backend terminal:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Mail__PickupDirectory = Join-Path $PWD 'artifacts\mail'
dotnet run --project src/AfterTab.API --launch-profile https
```

Open the `.eml` files in an email application. They are Git-ignored and contain
one-time links; do not publish them. Pickup mode works only in Development.
Production requires SMTP. Setting `Mail:Host` takes priority over pickup mode.

### Google

Use [Google Cloud Console](https://console.cloud.google.com/), choose a project,
and open Google Auth Platform → Clients (or APIs & Services → Credentials). Create
a Web application OAuth client for AfterTab or edit an appropriate existing client.
Add redirect URIs for the environments you use; replace the production example
with your public API origin:

```text
https://localhost:7180/signin-google
https://api.example.com/signin-google
```

Configure Branding/Audience and test users. A shared Google project's branding can
affect other apps; do not remove their redirect URIs. Store credentials locally
from the `after-tab-api` directory:

```powershell
dotnet user-secrets set 'Authentication:Google:ClientId' '<client-id>' --project src/AfterTab.API
dotnet user-secrets set 'Authentication:Google:ClientSecret' '<client-secret>' --project src/AfterTab.API
```

Set your own frontend domain, contact details and legal page URLs from the
"Public legal pages" section in the consent screen configuration.
Restart the API. No frontend Google SDK or keys are required. See the
[Microsoft guide](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins?view=aspnetcore-9.0)
and [Google redirect rules](https://developers.google.com/identity/protocols/oauth2/web-server).

### Facebook

In [Meta for Developers](https://developers.facebook.com/apps/), create or select
an app and enable Facebook Login for a website. A separate Meta app avoids shared
branding and user identifiers. Add Valid OAuth Redirect URIs for the environments
you use; replace the production example with your public API origin:

```text
https://localhost:7180/signin-facebook
https://api.example.com/signin-facebook
```

Enable web login and HTTPS and check access to `email`. AfterTab identifies users
by provider identifier and also works without a Facebook email. Initially test
with an app administrator, developer or tester. Public access may require mode
changes and the requirements shown in Meta's dashboard. Store App ID/secret:

```powershell
dotnet user-secrets set 'Authentication:Facebook:AppId' '<app-id>' --project src/AfterTab.API
dotnet user-secrets set 'Authentication:Facebook:AppSecret' '<app-secret>' --project src/AfterTab.API
```

Restart the API. Both providers' callbacks point to the API, not the PWA. See the
[Microsoft Facebook Login guide](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/facebook-logins?view=aspnetcore-9.0).

### SMTP and production hosting configuration

Use an SMTP service with authenticated STARTTLS and an approved sender:

```powershell
dotnet user-secrets set 'Mail:Host' '<smtp-host>' --project src/AfterTab.API
dotnet user-secrets set 'Mail:Port' '587' --project src/AfterTab.API
dotnet user-secrets set 'Mail:Username' '<smtp-user>' --project src/AfterTab.API
dotnet user-secrets set 'Mail:Password' '<smtp-password-or-key>' --project src/AfterTab.API
dotnet user-secrets set 'Mail:From' '<approved-sender>' --project src/AfterTab.API
```

For Gmail, enable two-step verification and create an
[app password](https://myaccount.google.com/apppasswords) if your account permits it.
Use `smtp.gmail.com`, port `587`, the full Gmail address for Username and From,
and the app password rather than your normal Google password. Restart and test
confirmation/reset delivery and spam. Gmail has account limits; a transactional
email service is an option for larger traffic.

For Azure Communication Services Email, configure an email domain and link it to
a Communication Services resource, configure the Entra application and SMTP user,
then use `smtp.azurecomm.net:587`, the SMTP username, Entra client secret and an
approved sender from that domain. Monitor secret expiry. Consult current
[pricing](https://learn.microsoft.com/en-us/azure/communication-services/concepts/email-pricing)
and [SMTP setup](https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/send-email-smtp/smtp-authentication).
The API's region does not automatically determine the email service's data region.

Configure the equivalent settings on your chosen host: environment settings in
Azure App Service, or process/container settings on a VPS. Example API variables:

```text
Authentication__FrontendOrigin=https://app.example.com
Cors__AllowedOrigins__0=https://app.example.com
Authentication__Google__ClientId
Authentication__Google__ClientSecret
Authentication__Facebook__AppId
Authentication__Facebook__AppSecret
Mail__Host
Mail__Port
Mail__Username
Mail__Password
Mail__From
```

Set your own `ConnectionStrings__AfterTab`, frontend CORS origin, HTTPS and
trusted forwarded-header settings for your host. OAuth providers must receive HTTPS
callbacks. ASP.NET Data Protection keys must survive API restarts. Never store
secrets in source code, `VITE_*` variables, committed commands or chat messages.

Check registration before/after confirmation, remembered sessions, signing out,
password reset and revocation, Google/Facebook sign-in and cancellation, access
from another device, and preserving a guest when registering a new account.
Automated tests do not replace real provider sign-in or actual SMTP delivery tests.

## Public legal pages

Before publishing, customize `after-tab-app/public/privacy.html`, `terms.html`
and `data-deletion.html` in both languages for your installation: supply the
data controller, contact details (such as `support@example.com`), service providers
and retention rules. Replace the example contact with your administrator's actual address.

- `/privacy.html` — privacy policy, PL and EN.
- `/data-deletion.html` — email instructions for data deletion, PL and EN.
- `/terms.html` — terms of use, PL and EN.

These static pages are independent of the API and sign-in. Use your own frontend
URLs in Google and Meta. Example fields for Meta App settings → Basic:

```text
Privacy policy URL: https://app.example.com/privacy.html
Terms of Service URL: https://app.example.com/terms.html
User data deletion → Data deletion instructions URL:
https://app.example.com/data-deletion.html
```

Replace the example origin and configure the same legal page URLs in Google. Meta's deletion
instructions URL is not an API callback. Requests must be processed manually,
verifying account ownership and deleting or anonymising relevant data. Disconnecting
a provider, signing out or forgetting a guest does not delete server data.
Update the documents if retention, providers, charges or deletion procedures change.

## Data model, migrations and deployment

`EventParticipants.Id` identifies a settlement participant. `UserId` optionally
links an account; `DisplayName` holds the name of someone without an account.
Linked participants show the current user name. Merging preserves the identifier
of the participant without an account and makes the retired row an alias through
`MergedIntoParticipantId`, preserving private payment history. No fake Users are created.

`InitialNumericKeys` creates optional participant UserId and links transfer
confirmations to participants in the same event. Database keys are numeric;
HTTP identifiers use a separate GUID PublicId. There is no data-preserving upgrade
from the previous schema: the reset also removes payment history.
API and frontend must use compatible versions:
`participantId`, `paidByParticipantId`, `fromParticipantId` and `toParticipantId`
refer to participants; `authorUserId` still refers to the author account.

Owner-only participant management for open events:

```text
POST   /api/events/{eventId}/participants/virtual        { "displayName": "Alex" }
PUT    /api/events/{eventId}/participants/{participantId}
DELETE /api/events/{eventId}/participants/{participantId}
POST   /api/events/{eventId}/participants/{participantId}/link
       { "targetParticipantId": "..." }
```

The merge target must be an active participant with an account in the same event.

`InitialNumericKeys` includes optional `BalanceParticipantId` and
`BalanceAmountMinor`, allowing the payer or event owner to move a saved remainder
without guessing the original shares.
Create/edit requests use `balanceParticipantId`; `balance=true` without a selected
person is rejected. To move or undo a saved remainder:

```text
PUT /api/events/{eventId}/expenses/{expenseId}/balance
{ "participantId": "...", "rowVersion": "..." }
```

Use `participantId: null` to undo. The recipient must be included in the split.
`rowVersion` protects against stale forms; closed events reject changes. Merging
a participant also transfers the remainder recipient reference.

Apply missing local migrations with the command in the startup instructions.
For the separate migration tool, configure `Database__Provider` and
`ConnectionStrings__AfterTab` in its environment. Run `--migrate` with a schema-authorized
account for your own database. See the [deployment instructions](after-tab-api/tools/AfterTab.Deployment/README.en.md).
User Secrets stay outside the repository but are not encrypted.

Publishing the API/frontend and migrating Azure are separate operations. Pushing
code to GitHub does not automatically deploy this app or migrate its database.
The bilingual interface needs no database migration or new Azure settings; publish
the updated frontend and API to deliver all changes, including localised emails.

## Verification

Backend, from `after-tab-api`:

```powershell
dotnet test AfterTabApi.sln
```

Frontend, from `after-tab-app`:

```powershell
npm test
npm run lint
npm run build
```

The repository excludes local configuration, credentials, databases, dependencies
and generated output. Tests cover settlement arithmetic, participants and merging,
permissions, closed-event protections, private history, authentication recovery,
language selection and localisation consistency.

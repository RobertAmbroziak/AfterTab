# Migrations for your deployment

**[Polski](README.md)** | **English**

Run commands from the repository root. Replace values in `<...>` with your own
service settings. This mode supports SQL Server/Azure SQL and PostgreSQL without
requiring a particular domain, subscription or database name.

## Prepare your database

The numeric-key release requires an empty database and the `InitialNumericKeys`
baseline. Read the [reset instructions](../../docs/database.en.md) before upgrading
an earlier installation. The tool does not delete data and rejects incompatible migration history.

Create your own database and a schema-authorized migration account. Use a separate
API account with data and HiLo sequence permissions as described in the database guide.
The API does not migrate on startup and does not require administrative privileges.

## Configure the tool

| Environment variable | Value |
| --- | --- |
| `Database__Provider` | `SqlServer` or `PostgreSql`; defaults to `SqlServer` |
| `ConnectionStrings__AfterTab` | Your database connection string using the migration account |

You can also configure `Database:Provider` and `ConnectionStrings:AfterTab` in the
tool project's .NET User Secrets. Environment variables override them. The tool
reads neither API configuration nor `.env` files. User Secrets stay outside Git
but are not encrypted. Use your CI/CD provider's secret store in a deployment pipeline.

### SQL Server

```powershell
$env:Database__Provider = 'SqlServer'
$env:ConnectionStrings__AfterTab = 'Server=<sql-host>;Database=<database>;User ID=<migration-user>;Password=<password>;Encrypt=True;TrustServerCertificate=False'
dotnet run --project after-tab-api/tools/AfterTab.Deployment -- --migrate
```

### Azure SQL

Select `SqlServer` and supply your own server's connection string, for example:

```text
Server=tcp:<sql-server>.database.windows.net,1433;Database=<database>;User ID=<migration-user>;Password=<password>;Encrypt=True;TrustServerCertificate=False
```

This example uses SQL authentication. Choose an access method supported by your
server and migration account, and keep real credentials outside Git. Allow your
computer or runner through your database's network configuration. If the running
API uses a managed identity, create its user in your database and grant access to
data and the sequence. Identity settings come from your own API resource.
`--migrate` does not create users or grant permissions.

### PostgreSQL

```powershell
$env:Database__Provider = 'PostgreSql'
$env:ConnectionStrings__AfterTab = 'Host=<postgres-host>;Port=5432;Database=<database>;Username=<migration-user>;Password=<password>;SSL Mode=VerifyFull'
dotnet run --project after-tab-api/tools/AfterTab.Deployment -- --migrate
```

This example assumes TLS with a trusted certificate matching the hostname.
Configure certificates for your server. Local examples and Docker Compose are
covered by the root README and the [mikr.us/VPS guide](../../../deploy/mikrus/README.en.md).

## Inspect state or use EF CLI

With these settings, run the tool without `--migrate` to read migration history
and record counts from a prepared database without changing its schema.

Alternatively, from `after-tab-api`, run `dotnet tool restore`, followed by
`dotnet ef database update --project src/AfterTab.DAL.EF --startup-project src/AfterTab.API`
for SQL Server, or select `src/AfterTab.DAL.Migrations.PostgreSql` for PostgreSQL.
Match the provider and connection string to the selected engine.

Do not use `--bootstrap` with these settings. It is a separate compatibility mode,
not general provisioning for your instance. Use `--migrate` and manage API account
grants independently.

## Publish

Build and publish the API and frontend to your chosen host. Configure the running
API's connection string, frontend origin, CORS, SMTP and your own OAuth clients
separately. See the [root README](../../../README.en.md) for example domains and callbacks.
Pushing to GitHub does not deploy the application or migrate its database.

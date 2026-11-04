# SQL Server migrations for your deployment

**[Polski](README.md)** | **English**

This application branch supports SQL Server/Azure SQL. For your own deployment,
use standard EF CLI. It reads `ConnectionStrings__AfterTab` and does not require
a specific server, database name, Azure account or API identity. Replace values
in `<...>` with settings for your database.

## Apply migrations

From the repository root, change into `after-tab-api`:

```powershell
cd after-tab-api
$env:ConnectionStrings__AfterTab = 'Server=<sql-host>;Database=<database>;User ID=<migration-user>;Password=<password>;Encrypt=True;TrustServerCertificate=False'
dotnet restore
dotnet tool restore
dotnet ef database update --project src/AfterTab.DAL.EF --startup-project src/AfterTab.API
```

For Azure SQL, the host is `tcp:<sql-server>.database.windows.net,1433`.
Create your own database and schema-authorized migration account. The example
uses SQL authentication; choose an access method supported by your server.
Keep actual credentials in environment settings or your pipeline secret store,
outside Git. Allow your migration connection in your server's network rules.

Before updating an existing database, verify the connection string and take a
backup. This branch's migrations differ from the numeric-key migrations on
`develop`. Use documentation and commands matching the code version you publish.

## Running API account

Configure the API connection string separately from the migration account.
The API needs data permissions rather than schema modification privileges.
If it uses an Azure managed identity, create that identity's user in your database
and grant access. Its name and identifier come from your own API resource.
The API does not run migrations on startup.

## Helper limitations on this branch

`AfterTab.Deployment` on this branch contains an older mode with additional
database and user-name restrictions. It is not a general provisioning tool for
your instance. The EF CLI approach above works independently and does not require
`--bootstrap`, its configuration, or accounts from the original deployment.

## Publish

Publish the API and frontend to your chosen host, and configure your own domains,
CORS, SMTP and Google/Facebook clients as described in the [root README](../../../README.en.md).
Pushing to GitHub does not deploy the application or migrate its database.

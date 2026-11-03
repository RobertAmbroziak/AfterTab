# Numeric keys and a fresh database schema

[Polski](database.md)

This release **replaces the previous GUID schema**. SQL Server/Azure SQL and
PostgreSQL each have their own `InitialNumericKeys` baseline. There is no existing-data
conversion or automatic deletion. Pushing to `develop` does not reset a database.

## Key model

- All 12 tables use `Id: bigint` (`long` in C#). Foreign keys, including audit
  references, are numeric. `ExpenseSplits` has no public GUID column.
- `Users`, `Sessions`, `Events`, `EventParticipants` and `Expenses` also have a
  GUID/UUID `PublicId` with a unique index. URLs, JSON fields and the frontend
  continue to use GUIDs. On SQL Server, the numeric PK is clustered and the PublicId index is nonclustered.
- EF allocates IDs from a HiLo sequence. Keys are available after `Add`, before
  `SaveChanges`, allowing a user/session or event/participant/invite graph to be
  committed in one transaction. Processes reserve separate blocks. Gaps and
  differences between numeric order and commit order are expected.
- Login-operation `Version` GUIDs, session secrets and expense `RowVersion`
  tokens serve different purposes and remain independent of primary keys.

## One-time upgrade of an existing installation

Perform during a planned deployment, separately for each environment:

1. Stop the API and frontend writes. Keep a backup of the old database if you
   want the option of rolling back to the previous application version.
2. Prepare a **new empty database**, or explicitly delete and recreate the
   application's existing database using an administration tool. Neither old
   tables nor the old `__EFMigrationsHistory` may remain. Clearing rows or only
   deleting migration history is insufficient.
3. Set `Database__Provider` and `ConnectionStrings__AfterTab` outside Git for
   the API and deployment tool. Use a schema-authorized migration account.
4. From the repository root, run:

   ```powershell
   dotnet run --project after-tab-api/tools/AfterTab.Deployment -- --migrate
   ```

   For the existing Azure/Entra deployment mode, use `--bootstrap` instead,
   following the [deployment tool instructions](../tools/AfterTab.Deployment/README.md).
   Recreate the managed identity database user and its grants after recreating the database.
5. Publish the matching API and start the frontend. Verify guest/account creation,
   an event, an expense and its settlement. Everyone starts again: old accounts,
   sessions, events, invites, expenses and payment acknowledgements are unavailable.

The deployment tool's `--migrate`/`--bootstrap` refuses unknown or old migration
history. It never drops data or tables to resolve incompatibility. The first
`dotnet ef database update` for this release also requires an empty database.
Subsequent updates to this schema use normal migrations without another reset.
Rolling back the old code requires restoring its database backup, not running it
against the new schema.

## Sequence permissions

The API account needs table CRUD and access to `EntityFrameworkHiLoSequence`:

- SQL Server/Azure SQL: `UPDATE` on the sequence or the `dbo` schema. The existing
  bootstrap grants CRUD on `dbo`, which covers sequence allocation.
- PostgreSQL: `USAGE` on the `public` schema and sequence. Compose's
  `init-runtime-user.sql` grants default `USAGE, SELECT` on sequences created by
  `aftertab_owner`, so `aftertab_app` also receives access to the HiLo sequence.

The running API does not need schema migration privileges. Check sequence grants
as well as the connection string when configuring roles manually.

## Docker Compose

A new PostgreSQL volume starts empty; `migrate` creates the schema. An existing
volume containing the old schema needs the planned reset above. Rebuilding images
does not reset data. After recreating the database, restore `aftertab_app` access
and default grants: initialization scripts only run when a volume is first initialized.

`docker compose down -v` also deletes database data, logs and Data Protection keys;
it is not a normal update step. See the [mikr.us deployment guide](../../deploy/mikrus/README.en.md).

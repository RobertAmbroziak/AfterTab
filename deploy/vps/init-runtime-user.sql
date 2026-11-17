-- Runs only when the Postgres data volume is first initialized.
-- psql quotes password literals and identifiers; no secret is stored in this file.
\set ON_ERROR_STOP on
\getenv app_password DB_APP_PASSWORD
\getenv db_name POSTGRES_DB
\getenv owner_name POSTGRES_USER

CREATE ROLE aftertab_app LOGIN PASSWORD :'app_password';
GRANT CONNECT ON DATABASE :"db_name" TO aftertab_app;
GRANT USAGE ON SCHEMA public TO aftertab_app;
ALTER DEFAULT PRIVILEGES FOR ROLE :"owner_name" IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO aftertab_app;
ALTER DEFAULT PRIVILEGES FOR ROLE :"owner_name" IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO aftertab_app;

# Database scripts

This folder contains SQL diagnostics, safe test fixtures, and dated schema exports.

The only source of truth for the current application schema is EF Core migrations in `LiMS.Api/Data/Migrations`, applied by `LiMS.Api/Data/DatabaseInitializer.cs`. The file `LiMSDB-schema-snapshot-20261115.sql` is a machine-specific historical export and is **not** a deployment script; it predates later migrations. Apply the current schema with `dotnet ef database update --project LiMS.Api --context LiMSDbContext` after setting the correct environment connection string, or start the API to run its configured migration initializer.

Do not put live database backups (`.bak`, `.mdf`, or `.ldf` files), credentials, or real reader data in this folder or commit them to GitHub. The SQL Server instance and migration history must be verified in the target environment; files in this repository alone do not prove deployment.

Book cover images are stored in SQL Server. The `AddBookCoverImage` migration adds the nullable image-data and content-type columns; later migrations add reader notifications and integrity/query indexes.

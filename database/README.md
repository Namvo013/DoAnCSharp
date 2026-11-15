# Database scripts

This folder is for SQL scripts that are safe to share with the project team, such as schema documentation and anonymized sample data.

The application currently manages its SQL Server schema through EF Core migrations in `LiMS.Api/Data/Migrations`. Do not put live database backups (`.bak`, `.mdf`, or `.ldf` files), credentials, or real reader data in this folder or commit them to GitHub.

Book cover images are stored in SQL Server. The `AddBookCoverImage` migration adds the nullable image-data and content-type columns; the API applies pending migrations at startup.

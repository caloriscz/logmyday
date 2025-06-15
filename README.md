# logmyday

Track daily activities

## Migration

Run from the root of your solution (or in LogMyDay.App folder):

```
dotnet ef migrations add InitialCreate --project LogMyDay.Api --startup-project LogMyDay.App --output-dir Infrastructure/Data/Migrations
```

Then apply the migration:

```
dotnet ef database update --project LogMyDay.Api --startup-project LogMyDay.App
```

## Generate migration script

```
dotnet ef migrations script --project LogMyDay.Api --startup-project LogMyDay.App --output InitialCreate.sql
```

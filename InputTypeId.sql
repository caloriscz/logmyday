BEGIN TRANSACTION;
ALTER TABLE [Tags] ADD [IsRange] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Tags] ADD [IsRepeatable] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Tags] ADD [TimeGranularity] int NOT NULL DEFAULT 0;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250608064509_RepeatableRangeGranularity', N'9.0.5');

COMMIT;
GO


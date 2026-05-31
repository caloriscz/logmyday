-- Migration: RemoveTagNotifications
-- Date: 2026-05-15
-- Drops the LogMyDay_Notifications table and all associated constraints/indexes.

BEGIN TRANSACTION;

IF OBJECT_ID(N'[LogMyDay_Notifications]', N'U') IS NOT NULL
BEGIN
    -- Drop FK constraint first (references LogMyDay_Tags)
    IF EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_LogMyDay_Notifications_LogMyDay_Tags_TagId'
    )
        ALTER TABLE [LogMyDay_Notifications]
            DROP CONSTRAINT [FK_LogMyDay_Notifications_LogMyDay_Tags_TagId];

    -- Drop index
    IF EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_LogMyDay_Notifications_TagId'
          AND object_id = OBJECT_ID(N'[LogMyDay_Notifications]')
    )
        DROP INDEX [IX_LogMyDay_Notifications_TagId] ON [LogMyDay_Notifications];

    -- Drop table
    DROP TABLE [LogMyDay_Notifications];
END;

-- Record migration (skip if already recorded)
IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515165057_RemoveTagNotifications'
)
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260515165057_RemoveTagNotifications', N'9.0.10');

COMMIT;

-- Migration: RemoveTagNotifications
-- Date: 2026-05-15
-- Drops the LogMyDay_Notifications table and all associated constraints/indexes.

BEGIN TRANSACTION;

-- Drop FK constraint first (references LogMyDay_Tags)
ALTER TABLE [LogMyDay_Notifications]
    DROP CONSTRAINT [FK_LogMyDay_Notifications_LogMyDay_Tags_TagId];

-- Drop index
DROP INDEX [IX_LogMyDay_Notifications_TagId] ON [LogMyDay_Notifications];

-- Drop table
DROP TABLE [LogMyDay_Notifications];

-- Record migration
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260515165057_RemoveTagNotifications', N'9.0.10');

COMMIT;

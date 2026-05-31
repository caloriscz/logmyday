BEGIN TRANSACTION;
DROP TABLE "LogMyDay_Notifications";

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260515165057_RemoveTagNotifications', '9.0.10');

COMMIT;


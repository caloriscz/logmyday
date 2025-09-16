BEGIN TRANSACTION;
CREATE TABLE [Users] (
    [Id] uniqueidentifier NOT NULL,
    [Email] nvarchar(450) NOT NULL,
    [DisplayName] nvarchar(max) NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [IsAdmin] bit NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [UpdatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);

CREATE TABLE [PasswordResets] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Token] nvarchar(450) NOT NULL,
    [ExpiresUtc] datetime2 NOT NULL,
    [UsedUtc] datetime2 NULL,
    [CreatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_PasswordResets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PasswordResets_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_PasswordResets_Token] ON [PasswordResets] ([Token]);

CREATE INDEX [IX_PasswordResets_UserId] ON [PasswordResets] ([UserId]);

CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250912073611_AddUserAuthentication', N'9.0.5');

COMMIT;
GO


IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
ALTER TABLE [DocumentChunks] ADD [EmbeddingVector] nvarchar(max) NULL;

ALTER TABLE [DocumentChunks] ADD [HasEmbedding] bit NOT NULL DEFAULT CAST(0 AS bit);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260528065919_AddEmbeddingVector', N'9.0.17');

ALTER TABLE [Subjects] ADD [ManagedByUserId] int NULL;

CREATE INDEX [IX_Subjects_ManagedByUserId] ON [Subjects] ([ManagedByUserId]);

ALTER TABLE [Subjects] ADD CONSTRAINT [FK_Subjects_Users_ManagedBy] FOREIGN KEY ([ManagedByUserId]) REFERENCES [Users] ([UserId]) ON DELETE SET NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260603021701_AddManagedByUserIdToSubject', N'9.0.17');

ALTER TABLE [Subjects] DROP CONSTRAINT [FK_Subjects_Users_ManagedBy];

DROP INDEX [IX_Subjects_ManagedByUserId] ON [Subjects];

DECLARE @var sysname;
SELECT @var = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Subjects]') AND [c].[name] = N'ManagedByUserId');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [Subjects] DROP CONSTRAINT [' + @var + '];');
ALTER TABLE [Subjects] DROP COLUMN [ManagedByUserId];

CREATE TABLE [SubjectTeachers] (
    [SubjectId] int NOT NULL,
    [UserId] int NOT NULL,
    [IsSubjectHead] bit NOT NULL,
    CONSTRAINT [PK_SubjectTeachers] PRIMARY KEY ([SubjectId], [UserId]),
    CONSTRAINT [FK_SubjectTeachers_Subjects] FOREIGN KEY ([SubjectId]) REFERENCES [Subjects] ([SubjectId]) ON DELETE CASCADE,
    CONSTRAINT [FK_SubjectTeachers_Users] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
);

CREATE INDEX [IX_SubjectTeachers_UserId] ON [SubjectTeachers] ([UserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260603025320_AddSubjectTeacherRelationTable', N'9.0.17');

ALTER TABLE [Users] ADD [WeeklyTokenLimit] int NOT NULL DEFAULT 250000;

ALTER TABLE [Documents] ADD [FileHash] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260714134511_AddFileHashAndWeeklyTokenLimit', N'9.0.17');

ALTER TABLE [Users] ADD [PurchasedTokenBalance] int NOT NULL DEFAULT 0;

ALTER TABLE [Users] ADD [PurchasedTokenExpiry] datetime2 NULL;

CREATE TABLE [SubscriptionPackages] (
    [PackageId] int NOT NULL IDENTITY,
    [PackageName] nvarchar(100) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [ExtraTokenAmount] int NOT NULL,
    [DurationUnit] nvarchar(20) NOT NULL DEFAULT N'Month',
    [DurationValue] int NOT NULL DEFAULT 1,
    CONSTRAINT [PK_SubscriptionPackages] PRIMARY KEY ([PackageId])
);

CREATE TABLE [UserTransactions] (
    [TransactionId] uniqueidentifier NOT NULL,
    [UserId] int NOT NULL,
    [PackageId] int NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [PaymentGateway] nvarchar(50) NOT NULL DEFAULT N'MoMo',
    [TransactionStatus] nvarchar(20) NOT NULL DEFAULT N'Pending',
    [CreatedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    CONSTRAINT [PK_UserTransactions] PRIMARY KEY ([TransactionId]),
    CONSTRAINT [FK_UserTransactions_SubscriptionPackages_PackageId] FOREIGN KEY ([PackageId]) REFERENCES [SubscriptionPackages] ([PackageId]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserTransactions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
);

CREATE INDEX [IX_UserTransactions_PackageId] ON [UserTransactions] ([PackageId]);

CREATE INDEX [IX_UserTransactions_UserId] ON [UserTransactions] ([UserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260715040049_AddSubscriptionAndMomo', N'9.0.17');

COMMIT;
GO


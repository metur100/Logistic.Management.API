-- Nova tabela za sistem podrške/tiketa (SupportTickets).
-- Zalijepiti u Adminer -> SQL command -> Execute.

CREATE TABLE [SupportTickets] (
    [Id] int NOT NULL IDENTITY,
    [Subject] nvarchar(200) NOT NULL,
    [Description] nvarchar(2000) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [Priority] nvarchar(max) NOT NULL,
    [CreatedByUserId] int NOT NULL,
    [AssignedToUserId] int NULL,
    [Resolution] nvarchar(2000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    [ResolvedAt] datetime2 NULL,
    CONSTRAINT [PK_SupportTickets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SupportTickets_Users_AssignedToUserId] FOREIGN KEY ([AssignedToUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_SupportTickets_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_SupportTickets_AssignedToUserId] ON [SupportTickets] ([AssignedToUserId]);
CREATE INDEX [IX_SupportTickets_CreatedByUserId] ON [SupportTickets] ([CreatedByUserId]);

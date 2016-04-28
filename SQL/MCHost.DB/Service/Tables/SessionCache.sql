CREATE TABLE [dbo].[SessionCache]
(
	[Key] VARCHAR(128) NOT NULL PRIMARY KEY, 
    [IP] VARCHAR(16) NOT NULL, 
    [UserId] INT NOT NULL, 
    [ExpireDate] DATETIME NOT NULL, 
    CONSTRAINT [FK_SessionCache_Users] FOREIGN KEY ([UserId]) REFERENCES [Users]([Id])
)

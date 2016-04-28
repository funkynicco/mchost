CREATE TABLE [dbo].[UserLog]
(
	[UserId] INT NOT NULL, 
    [Time] DATETIME NOT NULL, 
    [Message] TEXT NOT NULL, 
    CONSTRAINT [FK_UserLog_Users] FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) 
)

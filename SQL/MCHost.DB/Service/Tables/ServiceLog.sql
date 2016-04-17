CREATE TABLE [dbo].[ServiceLog]
(
	[Service] INT NOT NULL , 
    [Date] DATETIME NOT NULL DEFAULT (GETUTCDATE()), 
    [Text] TEXT NOT NULL
)

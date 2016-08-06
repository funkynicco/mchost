CREATE TABLE [dbo].[Users]
(
	[Id] INT NOT NULL PRIMARY KEY, 
    [Email] VARCHAR(255) NOT NULL, 
	[DisplayName] VARCHAR(255) NOT NULL,
    [PasswordHash] VARCHAR(128) NOT NULL, 
    [Active] BIT NOT NULL, 
    [Role] INT NOT NULL DEFAULT 1, 
    [TimeZone] VARCHAR(128) NULL
)

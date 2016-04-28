CREATE PROCEDURE [dbo].[AddUserCookie]
	@key VARCHAR(128),
	@ip VARCHAR(16),
	@userId INT,
	@expireDate DATETIME
AS
BEGIN

	SET NOCOUNT ON

	DELETE FROM [SessionCache] WHERE [Key] = @key

	INSERT INTO [SessionCache] (
		[Key],
		[IP],
		[UserId],
		[ExpireDate]
	) VALUES (
		@key,
		@ip,
		@userId,
		@expireDate
	)

END
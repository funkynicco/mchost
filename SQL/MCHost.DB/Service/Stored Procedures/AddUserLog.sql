CREATE PROCEDURE [dbo].[AddUserLog]
	@userId INT,
	@message TEXT
AS
BEGIN

	SET NOCOUNT ON

	INSERT INTO [UserLog] (
		[UserId],
		[Time],
		[Message]
	) VALUES (
		@userId,
		GETUTCDATE(),
		@message
	)

END
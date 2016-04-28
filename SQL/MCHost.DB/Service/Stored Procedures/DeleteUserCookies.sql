CREATE PROCEDURE [dbo].[DeleteUserCookies]
	@userId INT
AS
BEGIN

	SET NOCOUNT ON

	DELETE FROM [SessionCache] WHERE [UserId] = @userId

END
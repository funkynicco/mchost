CREATE PROCEDURE [dbo].[DeleteUserCookie]
	@key VARCHAR(128)
AS
BEGIN

	SET NOCOUNT ON

	DELETE FROM [SessionCache] WHERE [Key] = @key

END
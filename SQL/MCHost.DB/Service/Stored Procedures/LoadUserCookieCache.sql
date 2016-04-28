CREATE PROCEDURE [dbo].[LoadUserCookieCache]
AS
BEGIN

	SET NOCOUNT ON

	DELETE FROM [SessionCache] WHERE GETUTCDATE() > [ExpireDate]

	SELECT
		[Key],
		[IP],
		[UserId],
		[ExpireDate]
	FROM
		[SessionCache]

END
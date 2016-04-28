CREATE PROCEDURE [dbo].[UpdateUserCookieExpireDate]
	@key VARCHAR(128),
	@expireDate DATETIME
AS
BEGIN

	SET NOCOUNT ON

	UPDATE
		[SessionCache]
	SET
		[ExpireDate] = @expireDate
	WHERE
		[Key] = @key

END
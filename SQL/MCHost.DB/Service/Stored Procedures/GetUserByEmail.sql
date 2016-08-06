CREATE PROCEDURE [dbo].[GetUserByEmail]
	@email VARCHAR(255)
AS
BEGIN

	SET NOCOUNT ON

	SELECT
		[Id],
		[Email],
		[DisplayName],
		[Role],
		[TimeZone]
	FROM
		[Users]
	WHERE
		[Email] = @email

END
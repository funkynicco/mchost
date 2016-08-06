CREATE PROCEDURE [dbo].[GetUsers]
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
		[Active] = 1

END
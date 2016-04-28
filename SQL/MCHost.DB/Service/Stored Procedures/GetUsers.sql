CREATE PROCEDURE [dbo].[GetUsers]
AS
BEGIN

	SET NOCOUNT ON

	SELECT
		[Id],
		[Email],
		[DisplayName],
		[Role]
	FROM
		[Users]
	WHERE
		[Active] = 1

END
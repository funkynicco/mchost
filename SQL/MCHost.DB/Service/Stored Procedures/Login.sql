CREATE PROCEDURE [dbo].[Login]
	@email VARCHAR(255),
	@passwordHash VARCHAR(128)
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
		[Active] = 1 AND
		[Email] = @email AND
		[PasswordHash] = @passwordHash

END
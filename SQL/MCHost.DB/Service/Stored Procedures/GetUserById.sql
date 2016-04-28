CREATE PROCEDURE [dbo].[GetUserById]
	@id INT
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
		[Id] = @id

END
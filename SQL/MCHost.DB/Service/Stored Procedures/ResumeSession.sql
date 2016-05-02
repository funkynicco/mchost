CREATE PROCEDURE [dbo].[ResumeSession]
	@key VARCHAR(128)
AS
BEGIN

	SET NOCOUNT ON

	SELECT
		b.[Id],
		b.[Email],
		b.[DisplayName],
		b.[Role]
	FROM
		[SessionCache] a
	INNER JOIN
		[Users] b
	ON
		a.[UserId] = b.[Id]
	WHERE
		a.[Key] = @key AND
		b.[Active] = 1

END
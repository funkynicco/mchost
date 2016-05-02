CREATE PROCEDURE [dbo].[GetPackage]
	@name VARCHAR(64)
AS
BEGIN

	SET NOCOUNT ON

	SELECT
		[Name],
		[Description],
		[Filename]
	FROM
		[Packages]
	WHERE
		[Name] = @name

END
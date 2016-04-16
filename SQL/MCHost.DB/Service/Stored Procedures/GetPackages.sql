CREATE PROCEDURE [dbo].[GetPackages]
AS
BEGIN
	SET NOCOUNT ON
	
	SELECT
		Name,
		[Description],
		[Filename]
	FROM
		Packages
END

CREATE PROCEDURE [dbo].[AddLog]
	@service int,
	@text text
AS
BEGIN
	SET NOCOUNT ON

	INSERT INTO ServiceLog (
		[Service],
		[Date],
		[Text]
	) VALUES (
		@service,
		GETUTCDATE(),
		@text
	)
END

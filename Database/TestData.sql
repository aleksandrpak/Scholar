/****** Script for SelectTopNRows command from SSMS  ******/
SELECT TOP 1000 [RequestId]
      ,[SessionId]
      ,[Request]
      ,[Response]
      ,[IsParsed]
      ,[IsMatched]
      ,[Language]
      ,[Search]
      ,[PageLimit]
      ,[IsHidden]
  FROM [ScholarNetDatabase].[dbo].[Requests]
  where RequestId >= 9337

 -- delete from [ScholarNetDatabase].[dbo].[Requests] where  RequestId > 10242 and requestid <> 10345

 -- insert into [ScholarNetDatabase].[dbo].[Requests] values('00000000-0000-0000-0000-000000000000', 'http://www.sciencedirect.com/science/article/pii/S0960894X12001357',
	--'Yang;Bioorganic & Medicinal Chemistry;1238;108;9337', 0, 0, 'ru', null, 0, 0)

	 --insert into [ScholarNetDatabase].[dbo].[Requests] values('00000000-0000-0000-0000-000000000000', 'http://www.nature.com/nchembio/journal/v1/n4/abs/nchembio727.html',
     --'Kagan;Nature chemical;1242;185;10242', 0, 0, 'en', null, 0, 0)
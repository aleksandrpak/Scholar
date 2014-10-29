using System;
using System.Collections.Generic;
using System.Data;
using System.Data.EntityClient;
using System.Data.Objects;
using System.Linq;
using System.Windows.Forms;
using Scholar.Common.Tools;

namespace Scholar.Common.Database
{
    static public class DatabaseUpdate
    {
        private static T ExecuteScalarCommand<T>(this ObjectContext context, string command)
        {
            var connection = ((EntityConnection)context.Connection).StoreConnection;
            if (connection.State == ConnectionState.Closed)
                connection.Open();

            var connectionCommand = connection.CreateCommand();
            connectionCommand.CommandText = command;
            connectionCommand.CommandType = CommandType.Text;
            
            return (T)connectionCommand.ExecuteScalar();
        }

        private static void ExecuteNonQuery(this ObjectContext context, string command)
        {
            var connection = ((EntityConnection)context.Connection).StoreConnection;
            if (connection.State == ConnectionState.Closed)
                connection.Open();

            var connectionCommand = connection.CreateCommand();
            connectionCommand.CommandText = command;
            connectionCommand.CommandType = CommandType.Text;

            connectionCommand.ExecuteNonQuery();
        }

        static public bool Update()
        {
            var updates = new Dictionary<int, string>
            {
	            {1, "ALTER TABLE [dbo].[Requests] ADD [IsHidden] [bit] DEFAULT(0) NOT NULL"},
	            {2, "ALTER TABLE [dbo].[Editions] ADD [IsList] [bit] DEFAULT(0) NOT NULL"},
	            {3, "ALTER TABLE [dbo].[Results] DROP CONSTRAINT [FK_Results_Requests]"},

	            {4, @"
                ALTER TABLE [dbo].[Results] ADD CONSTRAINT
	            [FK_Results_Requests] FOREIGN KEY([RequestId])
                REFERENCES [dbo].[Requests]([RequestId])
                ON UPDATE NO ACTION
                ON DELETE CASCADE"},

	            {5, @"
                CREATE TABLE [dbo].[Keywords]
                (
	                [KeywordId] [int] NOT NULL PRIMARY KEY IDENTITY(1, 1),
	                [Keyword] [nvarchar](512) NOT NULL
                )"},

	            {6, @"
                CREATE TABLE [dbo].[ArticleKeywords] 
                (
	                [ArticleId] [int] NOT NULL,
	                [KeywordId] [int] NOT NULL
                )"},

	            {7, @"
                ALTER TABLE [dbo].[ArticleKeywords] ADD  CONSTRAINT [PK_ArticleKeywords] PRIMARY KEY CLUSTERED 
                (
	                [ArticleId] ASC,
	                [KeywordId] ASC
                )"},

	            {8, @"
                ALTER TABLE [dbo].[ArticleKeywords] ADD CONSTRAINT
                [FK_ArticleKeywords_Articles] FOREIGN KEY([ArticleId])
                REFERENCES [dbo].[Articles]([ArticleId])
                ON UPDATE NO ACTION
                ON DELETE CASCADE"},

	            {9, @"
                ALTER TABLE [dbo].[ArticleKeywords] ADD CONSTRAINT
                [FK_ArticleKeywords_Keywords] FOREIGN KEY([KeywordId])
                REFERENCES [dbo].[Keywords]([KeywordId])
                ON UPDATE NO ACTION
                ON DELETE CASCADE"},

	            {10, @"CREATE UNIQUE NONCLUSTERED INDEX [UX_Keywords_Keyword] ON [dbo].[Keywords]([Keyword]);"},

	            {11, @"
                CREATE UNIQUE NONCLUSTERED INDEX [UX_Names_LastName_Initials] ON [dbo].[Names]
                (
	                [LastName] ASC,
	                [Initials] ASC
                )"},

	            {12, "ALTER TABLE [dbo].[Results] DROP CONSTRAINT [FK_Results_Articles]"},

	            {13, @"
                ALTER TABLE [dbo].[Results] ADD CONSTRAINT
	            [FK_Results_Articles] FOREIGN KEY([ArticleId])
                REFERENCES [dbo].[Articles]([ArticleId])
                ON UPDATE NO ACTION
                ON DELETE CASCADE"},

	            {14, @"
                DELETE FROM [dbo].[Articles]
                WHERE [Article] IN (SELECT [Article] FROM [dbo].[Articles] GROUP BY [Article] HAVING COUNT([Article]) > 1)"
	            },

	            {15, @"
                CREATE UNIQUE NONCLUSTERED INDEX [UX_Articles_Article_Year_Cited] ON [dbo].[Articles]
                (
	                [Article] ASC,
	                [Year] ASC,
	                [Cited] ASC
                )"},

	            {16, @"
                CREATE NONCLUSTERED INDEX [IX_Editions_Field_IsList_IsReferal] ON [dbo].[Editions]
                (
	                [Field] ASC,
	                [IsList] ASC,
	                [IsReferal] ASC
                )"},

	            {17, @"
                -- =============================================
                -- Author:		Aleksandr Pak
                -- Create date: 2012-10-14
                -- =============================================
                CREATE PROCEDURE [dbo].[InsertKeyword]
	                @Keyword NVARCHAR(512)
                AS
                BEGIN
	                SET NOCOUNT ON;

                    IF NOT EXISTS (SELECT * FROM [dbo].[Keywords] WHERE [Keyword] = @Keyword)
	                BEGIN
		                INSERT INTO [dbo].[Keywords]([Keyword])
		                VALUES (@Keyword);
	                END

	                SELECT [KeywordId]
	                FROM [dbo].[Keywords]
	                WHERE [Keyword] = @Keyword
                END"},

	            {18, @"
                -- =============================================
                -- Author:		Aleksandr Pak
                -- Create date: 2012-10-14
                -- =============================================
                CREATE PROCEDURE [dbo].[InsertArticleKeyword]
	                @ArticleId INT,
	                @KeywordId INT
                AS
                BEGIN
	                SET NOCOUNT ON;

                    IF NOT EXISTS (SELECT * FROM [dbo].[ArticleKeywords]
		                WHERE [ArticleId] = @ArticleId AND [KeywordId] = @KeywordId)
	                BEGIN
		                INSERT INTO [dbo].[ArticleKeywords]([ArticleId], [KeywordId])
		                VALUES (@ArticleId, @KeywordId);
	                END
                END"},

	            {19, @"
                CREATE TABLE [dbo].[Workspaces]
                (
	                [WorkspaceId] [int] PRIMARY KEY IDENTITY(1, 1) NOT NULL,
	                [Workspace] [nvarchar](255) NOT NULL
                )"},

	            {20, @"
                TRUNCATE TABLE [dbo].[Workspaces]

                INSERT INTO [dbo].[Workspaces] ([Workspace])
                VALUES (N'Default')"},

	            {21, @"
                -- =============================================
                -- Author:		Aleksandr Pak
                -- Create date: 2012-02-17
                -- =============================================
                ALTER PROCEDURE [dbo].[InsertArticle]
	                @Article NVARCHAR(512),
	                @EditionId INT,
	                @Year INT,
	                @Cited INT
                AS
                BEGIN
	                SET NOCOUNT ON;

                    IF NOT EXISTS (SELECT * FROM [dbo].[Articles]
		                WHERE LOWER(LTRIM(RTRIM([Article]))) = LOWER(LTRIM(RTRIM(@Article))) AND [EditionId] = @EditionId
			                AND [Year] = @Year AND [Cited] = @Cited)
	                BEGIN
		                INSERT INTO [dbo].[Articles]([Article], [EditionId], [Year], [Cited])
		                VALUES (@Article, @EditionId, @Year, @Cited);
	                END
	
	                SELECT [ArticleId]
	                FROM [dbo].[Articles]
	                WHERE [Article] = @Article AND [EditionId] = @EditionId
		                AND [Year] = @Year AND [Cited] = @Cited
                END"},

	            {22, @"ALTER TABLE [dbo].[Requests] ADD [StartTime] [datetime] DEFAULT(GETDATE()) NOT NULL"},

	            {23, @"CREATE TYPE dbo.IntArray AS TABLE ([Item] [int] PRIMARY KEY NOT NULL)"},

	            {24, @"
                -- =============================================
                -- Author:		Aleksandr Pak
                -- Create date: 2012-10-21
                -- =============================================
                CREATE PROCEDURE [dbo].[GetNamesResults]
	                @NameIds IntArray READONLY
                AS
                BEGIN
	                SET NOCOUNT ON;

	                SELECT
		                R.[NameId],
		                R.[ArticleId],
		                R.[RequestId],
		                N.[Initials],
		                N.[LastName],
		                A.[Cited],
		                A.[Year],
		                E.[Field]
	                FROM [dbo].[Results] AS R
	                INNER JOIN @NameIds AS NI
		                ON R.[NameId] = NI.[Item]
	                INNER JOIN [dbo].[Names] AS N
		                ON R.[NameId] = N.[NameId]
	                INNER JOIN [dbo].[Articles]	AS A
		                ON R.[ArticleId] = A.[ArticleId]
	                INNER JOIN [dbo].[Editions]	AS E
		                ON A.[EditionId] = E.[EditionId]
                END"},

	            {25, @"ALTER TABLE [dbo].[Publishers] ADD [Country] [nvarchar](255) NULL"},
	            {26, @"ALTER TABLE [dbo].[Editions] ADD [Rating] [decimal](18, 5) NULL"},
	            {27, @"ALTER TABLE [dbo].[Editions] ADD [ISSN] [bigint] NULL"},
	            {28, @"DELETE FROM [dbo].[Results]"},
	            {29, @"DELETE FROM [dbo].[Requests]"},
	            {30, @"DELETE FROM [dbo].[Articles]"},
	            {31, @"ALTER TABLE [dbo].[Articles] ADD [Url] [nvarchar](2048) NULL"},
	            {32, @"ALTER TABLE [dbo].[Articles] ADD [Abstract] [nvarchar](MAX) NULL"},
	            {33, @"ALTER TABLE [dbo].[Requests] ADD [IsAdvanced] [bit] DEFAULT(0) NOT NULL"},

	            {
		            34,
		            @"UPDATE [dbo].[Requests] SET [IsAdvanced] = 1 WHERE [SessionId] = '00000000-0000-0000-0000-000000000000'"
	            },

	            {
		            35,
		            @"UPDATE [dbo].[Requests] SET [IsHidden] = 1 WHERE [SessionId] = '00000000-0000-0000-0000-000000000000'"
	            },

	            {36, @"DELETE FROM [dbo].[Results]"},
	            {37, @"DELETE FROM [dbo].[Requests]"},
	            {38, @"DELETE FROM [dbo].[Articles]"}
            };

	        try
            {
                using (var entities = new ScholarDatabaseEntities { CommandTimeout = 600 })
                {
                    var isUpdateTableExist = entities.ExecuteScalarCommand<bool>(
                    @"
                        IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'CurrentVersion')
                        BEGIN
                            SELECT CAST(1 AS [bit])
                        END
                        ELSE
                        BEGIN
                            SELECT CAST(0 AS [bit])
                        END
                    ");

                    if (!isUpdateTableExist)
                    {
                        entities.ExecuteNonQuery("CREATE TABLE [dbo].[CurrentVersion] ([Version] [int] NOT NULL)");
                        entities.ExecuteNonQuery("INSERT INTO [dbo].[CurrentVersion] VALUES (0)");
                    }

                    var version = entities.ExecuteScalarCommand<int>("SELECT [Version] FROM [dbo].[CurrentVersion]");

                    foreach (var update in updates.Where(i => i.Key > version).OrderBy(i => i.Key))
                    {
                        entities.ExecuteNonQuery(update.Value);
                        entities.ExecuteNonQuery(string.Format("UPDATE [dbo].[CurrentVersion] SET [Version] = {0}",
                                                               update.Key));
                    }
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.ToString());
                Log.Current.Error(exception);
                return false;
            }

            return true;
        }
    }
}

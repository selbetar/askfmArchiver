using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using askfmArchiver.Enums;
using askfmArchiver.Utils;
using CommandLine;
using Microsoft.Data.Sqlite;

namespace askfmArchiver
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            var dbTask = CreateTables();

            string userId = "", pageIterator = "", input = "", output = "";
            var stopAt = DateTime.Now;
            var type = JobType.NONE;

            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed(opts =>
                {
                    userId = opts.UserId.ToLower();
                    pageIterator = opts.PageIterator;
                    stopAt = opts.StopAt;
                    input = opts.Input;
                    output = opts.Output;

                    var tmp = opts.Type.ToLower();
                    switch (tmp)
                    {
                        case "p":
                        case "parse":
                            type = JobType.PARSE;
                            break;
                        case "m":
                        case "md":
                        case "markdown":
                            type = JobType.MARKDOWN;
                            break;
                    }
                })
                .WithNotParsed(HandleParseError);
            CommandLine.Parser.Default.ParseArguments<Options>(args);

            var fm = new FileManager();
            fm.CheckDir(output);

            var logFileName = "Log_" + userId + "_" + DateTime.Now.ToString("yy-MM-dd_HH-mm").
                Replace("-", "") + ".txt";
            var logFile = Path.Combine(output, logFileName);
            Logger.SetLogFile(logFile);

            await dbTask;
            switch (type)
            {
                case JobType.PARSE:
                    var askfmParser = new Parser(userId, output, pageIterator, stopAt);
                    await askfmParser.Parse();
                    break;
                case JobType.MARKDOWN:
                    var markDown = new MarkDown(userId, output);
                    await markDown.Generate();
                    break;
                case JobType.NONE:
                    Logger.WriteLine("Error in specified type. Run --help for supported job types.");
                    Environment.Exit(1);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void HandleParseError(IEnumerable<Error> errs)
        {
            var enumerable = errs as Error[] ?? errs.ToArray();
            foreach (var error in enumerable)
            {
                if (!enumerable.IsVersion() && !enumerable.IsHelp())
                {
                    Console.WriteLine(error.ToString());
                }
            }
            Environment.Exit(1);
        }

        private static async Task CreateTables()
        {
            var con = new SqliteConnectionStringBuilder()
            {
                DataSource = @"data.db",
                RecursiveTriggers = true,
                ForeignKeys = true
            }.ToString();

            await using var connection = new SqliteConnection(con);
            string[] queries =
            {
                @"CREATE TABLE IF NOT EXISTS Users(
	                UserID TEXT PRIMARY KEY,
	                UserName TEXT,
	                LastQuestion DateTime DEFAULT 0,
	                FirstQuestion DateTime DEFAULT 0)"
                ,
                @"CREATE TABLE IF NOT EXISTS Answers (
					UserID TEXT NOT NULL,
   					AnswerID TEXT NOT NULL,
   					AnswerText TEXT,
					QuestionText TEXT NOT NULL,
					AuthorID TEXT,
					AuthorName TEXT,
					Date DateTime NOT NULL,
					Likes INTEGER NOT NULL,
					VisualID  TEXT,
					VisualType INTEGER,
					VisualUrl TEXT,
					VisualExt TEXT,
					VisualHash TEXT,
					ThreadID TEXT NOT NULL,
					PageID TEXT,
					PRIMARY KEY (AnswerID),
				    FOREIGN KEY (UserID) 
				        REFERENCES Users (UserID) 
				            ON DELETE CASCADE 
				            ON UPDATE NO ACTION)"
                ,
                @"CREATE TABLE IF NOT EXISTS PDFGen (UserID TEXT, StopAt DateTime, AnswerID Text, PRIMARY KEY (UserID) 
				FOREIGN KEY (UserID)
        		REFERENCES Users (UserID))",
            };

            connection.Open();
            var command = connection.CreateCommand();
            var tasks = new List<Task<int>>();
            foreach (var query in queries)
            {
                command.CommandText = query;
                var tsk = command.ExecuteNonQueryAsync();
                tasks.Add(tsk);
            }

            await Task.WhenAll(tasks);
            command.Dispose();
            connection.Close();
        }
    }
}
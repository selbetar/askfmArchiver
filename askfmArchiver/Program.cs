using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using askfmArchiver.Models;
using askfmArchiver.Utils;
using CommandLine;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Options = askfmArchiver.Utils.Options;

namespace askfmArchiver
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {

            var archive = false;
            IHost host = null;
            var configDir = "";
            var dbSource = "";
            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed(options =>
                {
                    options.UserId = options.UserId.ToLower();

                    options.Output = options.Output == ""
                        ? Path.Combine(Directory.GetCurrentDirectory(), "output")
                        : Path.GetFullPath(options.Output);
                    Directory.CreateDirectory(options.Output);

                    options.Config = options.Config == ""
                        ? Path.Combine(Directory.GetCurrentDirectory(), "config")
                        : Path.GetFullPath(options.Config);
                    Directory.CreateDirectory(options.Config);
                    configDir = options.Config;

                    var builder = BuildConfig(options);
                    WriteDefaultConfig(Path.Combine(configDir, "appsettings.json"));

                    options.DbFile = options.DbFile == null ? Path.Combine(options.Config, "data.db") : options.DbFile;
                    dbSource = options.DbFile;

                    var logFileName = "askfmArchiver-" + DateTime.Now.ToString("yyyy-MM-dd") + ".log";
                    var logFile = Path.Combine(options.Output, "log", logFileName);
                    Log.Logger = new LoggerConfiguration()
                        .ReadFrom.Configuration(builder.Build())
                        .Enrich.FromLogContext()
                        .WriteTo.Console()
                        .WriteTo.File("log_askfm.log", LogEventLevel.Warning)
                        .CreateLogger();


                    host = Host.CreateDefaultBuilder()
                        .ConfigureServices((context, services) =>
                        {


                            if (options.Archive)
                            {
                                services.AddTransient<IParser, Parser>();
                                services.AddSingleton<INetworkManager, NetworkManager>();
                                archive = true;
                            }
                            else
                            {
                                services.AddTransient<IMarkDown, MarkDown>();
                            }

                            services.AddDbContext<MyDbContext>(opts =>
                                opts.UseSqlite());
                            services.AddSingleton<IFileManager, FileManager>();
                            services.AddSingleton<IOptions>(options);
                        })
                        .UseSerilog()
                        .Build();

                })
                .WithNotParsed(HandleParseError);

            CreateTables(dbSource);

            if (archive)
            {
                Log.Logger.Information("Application is Starting: Archival Service");
                var svc = ActivatorUtilities.CreateInstance<Parser>(host.Services);
                await svc.Parse();
                Log.Logger.Information("Application is Done: Archival Service");
            }
            else
            {
                Log.Logger.Information("Application is Starting: Markdown Service");
                var svc = ActivatorUtilities.CreateInstance<MarkDown>(host.Services);
                await svc.Generate();
                Log.Logger.Information("Application is Done: Markdown Service");
            }
        }

        private static ConfigurationBuilder BuildConfig(Options options)
        {
            var builder = new ConfigurationBuilder();

            var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environments.Development;
            builder.SetBasePath(options.Config)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env}.json",
                    optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            return builder;
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

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                    services.AddDbContext<MyDbContext>(opts =>
                        opts.UseSqlite(context.Configuration.GetConnectionString("DefaultConnection"))));

        private static void CreateTables(string dataSource)
        {
            var conStr = new SqliteConnectionStringBuilder()
            {
                DataSource = dataSource,
                RecursiveTriggers = true,
                ForeignKeys = true,
            }.ToString();

            using var connection = new SqliteConnection(conStr);
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
            foreach (var query in queries)
            {
                command.CommandText = query;
                command.ExecuteNonQuery();
            }

            command.Dispose();
            connection.Close();
        }


        private static void WriteDefaultConfig(string configFile)
        {
            const string config = "{\"Serilog\":{\"MinimumLevel\":{\"Default\":\"Information\",\"Override\":{\"Microsoft\":\"Warning\",\"System\":\"Warning\"}},\"Enrich\":[\"FromLogContext\"]}}";

            if (File.Exists(configFile)) return;

            File.WriteAllText(configFile, config);
        }
    }
}
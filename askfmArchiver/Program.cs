using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using askfmArchiver.Models;
using askfmArchiver.Utils;
using CommandLine;
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
            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed(options =>
                {
                    options.UserId = options.UserId.ToLower();
                    var builder = BuildConfig(options);

                    Log.Logger = new LoggerConfiguration()
                        .ReadFrom.Configuration(builder.Build())
                        .Enrich.FromLogContext()
                        .WriteTo.Console()
                        .WriteTo.File("log_askfm.txt", LogEventLevel.Information)
                        .CreateLogger();

                    host = Host.CreateDefaultBuilder()
                        .ConfigureServices((context, services) =>
                        {
                            services.AddSingleton<IFileManager, FileManager>();
                            services.AddSingleton<IOptions>(options);
                            services.AddDbContext<MyDbContext>(opts =>
                                opts.UseSqlite(context.Configuration.GetConnectionString("DefaultConnection")));
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
                        })
                        .UseSerilog()
                        .Build();

                })
                .WithNotParsed(HandleParseError);

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
            options.Output = options.Output == ""
                ? Path.Combine(Directory.GetCurrentDirectory(), "output")
                : Path.GetFullPath(options.Output);

            Directory.CreateDirectory(options.Output);

            var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environments.Development;
            builder.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
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
    }
}
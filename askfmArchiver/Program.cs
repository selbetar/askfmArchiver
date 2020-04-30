using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using askfmArchiver.Enums;
using askfmArchiver.Utils;
using CommandLine;

namespace askfmArchiver
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            var storageManager = StorageManager.GetInstance();
            
            string username     = "", pageIterator = "";
            var    parseThreads = false;
            var    endDate      = DateTime.Now;
            var    title        = "";
            var    md           = false;
            CommandLine.Parser.Default.ParseArguments<Options>(args)
                       .WithParsed(opts =>
                        {
                            username     = opts.Username;
                            pageIterator = opts.PageIterator;
                            parseThreads = opts.ParseThreads;
                            endDate      = opts.EndDate;
                            title        = opts.title;
                            md           = opts.md;
                        })
                       .WithNotParsed(HandleParseError);
            CommandLine.Parser.Default.ParseArguments<Options>(args);

            if (md)
            {
                var fm = new FileManager();
                await fm.PopulateStorage(DataTypes.Archive, username, "*.json");
                var markdown = new MarkDown(storageManager.Archive);
                await markdown.Generate();
                return;
            }
            
            var askfmParser = new Parser(username, title,pageIterator, endDate, parseThreads);
            await askfmParser.Parse();
            
        }

        private static void HandleParseError(IEnumerable<Error> errs)
        {
            foreach (var error in errs)
            {
                if (!errs.IsVersion())
                {
                    Console.WriteLine(error.ToString());
                }
            }

            Environment.Exit(-1);
        }
    }
}
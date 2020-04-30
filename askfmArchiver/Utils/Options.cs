using System;
using CommandLine;

namespace askfmArchiver.Utils
{
    public class Options
    {
        [Option('u', "user", Required = true,
                HelpText              = "The username of the askfm account to be parsed")]
        public string Username { get; set; }

        [Option('h', "title", Required = false,
                HelpText               = "The title of the markdown file")]
        public string title { get; set; } = "";

        [Option('p', "page", Required = false,
                HelpText              = "A number - The page iterator (id) at which parsing should start")]
        public string PageIterator { get; set; } = "";

        [Option('S', "stop-at", Required = false,
                HelpText =
                    "The date at which parsing should stop. Format of date should be: yyyy''MM''ddTHH''mm''ss")]
        public DateTime EndDate { get; set; }

        [Option('t', "threads", Required = false,
                HelpText = "Use this options if you want thread ids and answers associated with the thread" +
                           "to be stored in a separate file")]
        public bool ParseThreads { get; set; }
        
        [Option('m', "markdown", Required = false)]
        public bool md { get; set; }

        [Option('i', "input", Required = false)]
        public string inputPath { get; set; } = @"input/";
    }
}
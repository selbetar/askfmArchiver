using System;
using CommandLine;

namespace askfmArchiver.Utils
{
    public class Options : IOptions
    {
        [Option('u', "user", Required = true,
            HelpText = "The userid of the askfm profile")]
        public string UserId { get; set; }


        [Option('o', "out", Required = false, Default = "",
            HelpText = "Specify the output folder where any downloaded or generated files will be saved.")]
        public string Output { get; set; }

        [Option('c', "config", Required = false, Default = "",
            HelpText = "Specify the config folder app configuration is saved.")]
        public string Config { get; set; }

        [Option('a', "archive", Required = true, SetName = "parse",
            HelpText = "Execute an archival job for the specified user.")]
        public bool Archive { get; set; }

        [Option('p', "page", Required = false, SetName = "parse",
            HelpText = "The page iterator (id) at which archiving should start. Useful if parsing" +
                                    " was interrupted.", Default = "")]
        public string PageIterator { get; set; }

        [Option('s', "stop-at", Required = false, SetName = "parse",
            HelpText =
                "The date at which parsing should stop. Date should be in the following format: " +
                "yyyy''MM''ddTHH''mm''ss")]
        public DateTime StopAt { get; set; }

        [Option('m', "markdown", Required = true, SetName = "markdown",
            HelpText = "Generate markdown file(s) for the specified user.")]
        public bool Markdown { get; set; }

        [Option('d', "db", Required = false,
            HelpText = "Path to the database file.")]
        public string DbFile { get; set; }

        [Option('D', "descending", Required = false, Default = false,
    HelpText = "Specify output folder where any downloaded or generated files will be saved.")]
        public bool Descending { get; set; }

        [Option('r', "reset", Required = false, Default = false,
            HelpText = "Specify output folder where any downloaded or generated files will be saved.")]
        public bool RestMd { get; set; }

    }
}

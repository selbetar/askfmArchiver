using System;
using CommandLine;

namespace AskfmArchiver.Utils
{
    public class Options
    {
        [Option('u', "user USER", Required = true,
            HelpText = "The userid of the askfm account")]
        public string UserId { get; set; }
        
        [Option('t', "type TYPE", Required = true,
            HelpText = "Specify job type: 'parse', 'markdown'")] 
        public string Type { get; set; }

        [Option('p', "page ITERATOR", Required = false,
            HelpText              = "The page iterator (id) at which parsing should start. Useful if parsing" +
                                    " was interrupted.", Default = "")]
        public string PageIterator { get; set; }

        [Option('s', "stop-at", Required = false,
            HelpText =
                "The date at which parsing should stop. Date should be in the following format: " +
                "yyyy''MM''ddTHH''mm''ss")]
        public DateTime StopAt { get; set; }
        

        [Option('i', "input", Required = false, Default = @"./input/", Hidden = true)]
        public string Input { get; set; }
        
        [Option('o', "out FOLDER", Required = false, Default = @"./output/", 
            HelpText = "Specify output folder where any downloaded or generated files will be saved.")]
        public string Output { get; set; }
        
    }
}
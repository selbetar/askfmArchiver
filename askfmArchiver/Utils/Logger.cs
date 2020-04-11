using System;
using System.IO;

namespace askfmArchiver.Utils
{
    internal static class Logger
    {
        private const string Path = @"output";

        internal static void Write(string msg)
        {
            Directory.CreateDirectory(Path);
            File.AppendAllText(@"output/Log" + DateTime.Now.ToString("yyyy''MM''ddTHH''mm''ss"),
                               msg + "\n");
        }
    }
}
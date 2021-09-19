using System;
using System.IO;

namespace askfmArchiver.Utils
{
    internal static class Logger
    {

        private static string _file;
        private static readonly TextWriter ErrorWriter = Console.Error;

        internal static void SetLogFile(string file)
        {
            _file = file;
        }

        internal static void WriteLine(string line)
        {
            ErrorWriter.WriteLine(line);
            File.AppendAllText(_file, line);
        }
        internal static void WriteLine(string line, Exception e)
        {
            var error = line + Environment.NewLine + e.Message + Environment.NewLine + e.StackTrace;
            ErrorWriter.WriteLine(error);
            File.AppendAllText(_file, error);
        }
    }
}
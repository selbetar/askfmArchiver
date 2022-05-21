using System;

namespace askfmArchiver.Utils
{
    public interface IOptions
    {
        string UserId { get; }
        string Output { get; }
        string Config { get; }
        bool Archive { get; }
        string PageIterator { get; }
        DateTime StopAt { get; }
        bool Markdown { get; }
        string DbFile { get; }
        bool RestMd { get; }
        bool Descending { get; }

    }
}

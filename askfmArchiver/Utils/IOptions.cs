using System;

namespace askfmArchiver.Utils
{
    public interface IOptions
    {
        string UserId { get; }
        string Output { get; }
        bool Archive { get; }
        string PageIterator { get; }
        DateTime StopAt { get; }
        bool Markdown { get; }
        string DbFile { get; }

    }
}
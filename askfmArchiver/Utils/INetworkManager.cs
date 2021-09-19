using System.Threading.Tasks;

namespace askfmArchiver
{
    public interface INetworkManager
    {
        Task DownloadMedia(string url, string file);
        Task<string> HttpRequest(string url);
    }
}
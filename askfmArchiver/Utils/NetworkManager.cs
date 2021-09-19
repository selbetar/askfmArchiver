using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace askfmArchiver.Utils
{

    public class NetworkManager : INetworkManager
    {
        private readonly HttpClient _requestClient;
        private readonly HttpClient _downloadClient;
        public NetworkManager()
        {
            _requestClient = new HttpClient()
            {
                Timeout = new TimeSpan(0, 0, 2, 0)
            };

            _downloadClient = new HttpClient()
            {
                Timeout = new TimeSpan(0, 0, 0, 15)
            };
        }

        // throws an error on failure
        public async Task<string> HttpRequest(string url)
        {
            var response = await _requestClient.GetStringAsync(new Uri(url));
            return response;
        }

        // throws an error on failure
        public async Task DownloadMedia(string url, string file)
        {
            var response = await _downloadClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(file, response);
        }
    }
}
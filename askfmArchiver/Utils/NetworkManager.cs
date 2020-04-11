using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace askfmArchiver.Utils
{
    public class NetworkManager
    {
        private readonly string _path = @"output/visuals";

        public NetworkManager(string userName)
        {
            _path += "_" + userName + "/";
        }

        public async Task<string> HttpRequest(string url)
        {
            var response = "";
            var client = new HttpClient
                         {
                             Timeout = new TimeSpan(0, 0, 2, 0)
                         };

            try
            {
                response = await client.GetStringAsync(new Uri(url));
            }
            catch (Exception e)
            {
                throw e;
            }

            return response;
        }

        public async Task Download(string url, string fileName)
        {
            var client = new HttpClient
                         {
                             Timeout = new TimeSpan(0, 0, 0, 20)
                         };

            try
            {
                Directory.CreateDirectory(_path);
                var response = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(_path + fileName, response);
            }
            catch (Exception e)
            {
                Logger.Write(e.Message + "\n" + e.StackTrace);
                throw e;
            }
        }
    }
}
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace askfmArchiver.Utils
{
    public class NetworkManager
    {
        private static string _outDir;
        public NetworkManager()
        {
            _outDir = "";
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
                Logger.WriteLine("HttpRequest Error: Url is: " + url, e);
            }

            return response;
        }
        
        // returns file name on success
        // empty string otherwise
        public async Task<string> DownloadMedia(string url, string file)
        {
            var dir = Path.GetDirectoryName(file);
            if (_outDir == "")
            {
                try
                {
                    Directory.CreateDirectory(dir);

                }
                catch (Exception e)
                {
                    Logger.WriteLine("DownloadMedia() Exception: Specified path is bad: " + dir, e);
                    dir = Path.Join(@"./output/", Path.GetRandomFileName());
                    Directory.CreateDirectory(dir);
                    Logger.WriteLine("Visual will be saved to: " + dir);
                    _outDir = dir;
                }
            }

            file = Path.GetFileName(file);
            file = Path.Join(dir, file);
            
            var client = new HttpClient
            {
                Timeout = new TimeSpan(0, 0, 0, 20)
            };

            try
            {
                var response = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(file, response);
            }
            catch (Exception e)
            {
                Logger.WriteLine("DownloadMedia() Exception: URL is: " + url, e);
                file = "";
            }

            return file;
        }
        
    }
}
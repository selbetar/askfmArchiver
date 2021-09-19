using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using askfmArchiver.Enums;

namespace askfmArchiver.Utils
{
    public class FileManager : IFileManager
    {
        private static string _outDir;
        public FileManager()
        {
            _outDir = "";
        }

        public string ComputeHash(string file)
        {
            HashAlgorithm sha1Hash = SHA1.Create();
            byte[] hashValue;
            try
            {
                var fStream = File.OpenRead(file);
                fStream.Position = 0;
                hashValue = sha1Hash.ComputeHash(fStream);
                fStream.Close();
            }
            catch (Exception e)
            {
                Logger.WriteLine("ComputeHash Exception: ", e);
                return "";
            }

            var sBuilder = new StringBuilder();
            foreach (var by in hashValue)
            {
                sBuilder.Append(by.ToString("x2"));
            }

            return sBuilder.ToString();
        }

        public async Task SaveData<T>(T data, string file, FileType type)
        {
            var dir = Path.GetDirectoryName(file);

            if (_outDir != "" || !CheckDir(dir))
            {
                var filename = Path.GetFileName(file);
                file = Path.Combine(_outDir, filename);
            }

            switch (type)
            {
                case FileType.JSON:
                    file += ".json";
                    await SaveJson(data, file);
                    break;
                case FileType.MARKDOWN:
                    file += ".md";
                    await SaveMarkDown(data, file);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        private async Task SaveJson<T>(T data, string file)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            var json = JsonSerializer.Serialize(data, options);
            await File.WriteAllTextAsync(file, json, Encoding.UTF8);
        }
        private async Task SaveMarkDown<T>(T data, string file)
        {
            var lines = (List<string>)(object)data;
            await File.WriteAllLinesAsync(file, lines, Encoding.UTF8);
        }
        public bool CheckDir(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (Exception e)
            {
                var errorWriter = Console.Error;
                Logger.WriteLine("SaveData Error: ", e);
                errorWriter.WriteLine("Data will be written to ./output");
                Directory.CreateDirectory(@"output");
                _outDir = "output";
                return false;
            }

            return true;
        }

    }
}
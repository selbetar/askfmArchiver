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
        public FileManager()
        {
        }

        public string ComputeHash(string file)
        {
            HashAlgorithm sha1Hash = SHA1.Create();
            byte[] hashValue;

            var fStream = File.OpenRead(file);
            fStream.Position = 0;
            hashValue = sha1Hash.ComputeHash(fStream);
            fStream.Close();


            var sBuilder = new StringBuilder();
            foreach (var by in hashValue)
            {
                sBuilder.Append(by.ToString("x2"));
            }

            return sBuilder.ToString();
        }

        public async Task<bool> SaveData<T>(T data, string file, FileType type)
        {
            var dir = Path.GetDirectoryName(file);

            CheckDir(dir);

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

            return true;
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
        public void CheckDir(string dir)
        {
            Directory.CreateDirectory(dir);
        }

    }
}
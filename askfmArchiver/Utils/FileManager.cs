using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using askfmArchiver.Enums;
using askfmArchiver.Objects;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace askfmArchiver.Utils
{
    public class FileManager
    {
        private static readonly StorageManager Storage = StorageManager.GetInstance();
        private const string Path = @"output/";

        public async Task SaveData<T>(T data, string fileName, FileType type)
        {
            Directory.CreateDirectory(Path);
            
            switch (type)
            {
                case FileType.JSON:
                    fileName += ".json";
                    await SaveJson(data, fileName);
                    break;
                case FileType.MARKDOWN:
                    fileName += ".md";
                    await SaveMarkDown(data, fileName);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public async Task PopulateStorage(DataTypes type, string username, 
                                          string searchPattern = "", string path = @"input/")
        {
            if (!Directory.Exists(path))
            {
                var writer = Console.Error;
                writer.WriteLine("Invalid Path: " + path);
                return;
            }

            if (searchPattern == "")
            {
                searchPattern = "*" + type + "_" + username;
            }
            
            var files    = Directory.GetFiles(path, searchPattern);
            var tasks = new List<Task<string>>();
            tasks.AddRange(files.Select(file => File.ReadAllTextAsync(file)));
            var filesData = await Task.WhenAll(tasks);
            switch (type)
            {
                case DataTypes.Archive:
                    PopulateArchiveList(filesData);
                    break;
                case DataTypes.Threads:
                    PopulateThreadMap(filesData);
                    break;
                case DataTypes.Visuals:
                    PopulateVisualMapMap(filesData);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void PopulateThreadMap(IEnumerable<string> filesData)
        {
            foreach (var data in filesData)
            {
                var obj = JsonSerializer.Deserialize<Dictionary<string, HashSet<string>>>(data);
                foreach (var (key, value) in obj)
                {
                    Storage.ThreadMap.TryAdd(key, value);
                }
            }
        }

        private void PopulateVisualMapMap(IEnumerable<string> filesData)
        {
            foreach (var data in filesData)
            {
                var obj = JsonSerializer.Deserialize<Dictionary<string, string>>(data);
                foreach (var (key, value) in obj)
                {
                    Storage.VisualMap.TryAdd(key, value);
                }
            }
        }

        private void PopulateArchiveList(IEnumerable<string> filesData)
        {
            foreach (var data in filesData)
            {
                var obj = JsonSerializer.Deserialize<List<DataObject>>(data);
                Storage.AnswerData.AddRange(obj);
                ;
            }
        }

        private async Task SaveJson<T>(T data, string fileName)
        {
            var options = new JsonSerializerOptions
                          {
                              WriteIndented = true,
                              Encoder       = JavaScriptEncoder.Create(UnicodeRanges.All)
                          };
            var json = JsonSerializer.Serialize(data, options);
            await File.WriteAllTextAsync(fileName, json, Encoding.UTF8);
        }

        private async Task SaveMarkDown<T>(T data, string fileName)
        {
            File.AppendAllText(fileName, data.ToString(), Encoding.UTF8);
        }
    }
}
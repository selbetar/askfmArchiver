using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
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

        public async Task SaveData<T>(T data, string filename, FileType type)
        {
            Directory.CreateDirectory(Path);
            
            switch (type)
            {
                case FileType.JSON:
                    filename += ".json";
                    await SaveJson(data, filename);
                    break;
                case FileType.MARKDOWN:
                    filename += ".md";
                    await SaveMarkDown(data, filename);
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
                searchPattern = "*" + type + "_" + username + "*.json";
            }
            
            var files = Directory.GetFiles(path, searchPattern);
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
            var      answers = new List<DataObject>();
            string   user    = "",           header = "", other = "";
            int      qcount  = 0,            vcount = 0;
            DateTime first   = DateTime.Now, last   = DateTime.Now;
            
            foreach (var data in filesData)
            {
                var obj = JsonSerializer.Deserialize<Archive>(data);
                
                user   =  obj.User;
                header =  obj.Header;
                other  =  obj.Other;
                qcount += obj.QuestionCount;
                vcount += obj.VisualCount;
                first  =  first.CompareTo(obj.FirstQuestionDate) < 0 ? first : obj.FirstQuestionDate;
                last   =  last.CompareTo(obj.LastQuestionDate) > 0 ? last : obj.LastQuestionDate;
                
                answers.AddRange(obj.Data);
            }
            
            var archive = new Archive
                          {
                              Data              = answers,
                              User              = user,
                              Header            = header,
                              Other             = other,
                              QuestionCount     = qcount,
                              VisualCount       = vcount,
                              FirstQuestionDate = first,
                              LastQuestionDate  = last
                          };
            
            Storage.Archive = archive;
        }

        private async Task SaveJson<T>(T data, string filename)
        {
            var options = new JsonSerializerOptions
                          {
                              WriteIndented = true,
                              Encoder       = JavaScriptEncoder.Create(UnicodeRanges.All)
                          };
            var json = JsonSerializer.Serialize(data, options);
            await File.WriteAllTextAsync(filename, json, Encoding.UTF8);
        }

        private async Task SaveMarkDown<T>(T data, string filename)
        {
            List<string> lines = (List<string>) (object) data;
            File.WriteAllLines(filename, lines, Encoding.UTF8);
        }
    }
}
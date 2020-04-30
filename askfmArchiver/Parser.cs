using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using askfmArchiver.Enums;
using askfmArchiver.Objects;
using askfmArchiver.Utils;
using HtmlAgilityPack;

namespace askfmArchiver
{
    public class Parser
    {
        private const string BaseUrl = "https://ask.fm/";

        private readonly StorageManager _storageManager;
        private readonly NetworkManager _client;
        private readonly FileManager _fm;

        private readonly string _header;
        private readonly string _searchPattern;
        private readonly string _inputPath;
        private readonly string _username;
        private readonly string _baseUrl;
        private readonly string _pageIterator;
        private readonly DateTime _endDate;
        private readonly bool _parseThreads;

        private bool _isDone;
        private int _vcount;
        private int _acount;
        
        private readonly object _lock = new object();
        private readonly object _storageLock = new object();

        public Parser(string username, string header, string pageIterator = "",
                      DateTime endDate = default,  bool parseThreads = false, 
                      string inputPath = "input", string searchPattern = "")
        {
            _username       = username;
            _header         = header;
            _storageManager = StorageManager.GetInstance();
            _fm             = new FileManager();
            _client         = new NetworkManager(username);
            _baseUrl        = BaseUrl + username;
            _pageIterator   = pageIterator;
            _parseThreads   = parseThreads;
            _isDone         = false;
            _endDate        = endDate;

            _inputPath     = inputPath;
            _searchPattern = searchPattern;
        }

        public async Task Parse()
        {
            if (_parseThreads)
                await _fm.PopulateStorage(DataTypes.Threads, _username, "", _inputPath);

            await _fm.PopulateStorage(DataTypes.Visuals, _username, "", _inputPath);
            var url = _baseUrl;
            if (_pageIterator != "")
                url += "?older=" + _pageIterator;

            try
            {
                var htmlDoc = await GetHtmlDoc(url);
                await ParsePage(htmlDoc);
            }
            catch (Exception e)
            {
                Logger.Write("Message:\n" + e.Message + "\nStackTrace:\n" + e.StackTrace);
                await WriteToDisk();
                Environment.Exit(1);
            }

            await WriteToDisk();
            
            var md = new MarkDown(_storageManager.Archive);
            await md.Generate();
        }

        private async Task ParsePage(HtmlDocument html)
        {
            var currentPageId = _pageIterator;
            while (true)
            {
                var pageOb       = new DataObject();
                var nextHtmlTask = GetNextPage(html, pageOb);

                // Get the node that contains all of the questions on this page
                var articleNodes = html.DocumentNode.SelectNodes("//div[@class='item-page']")
                                       .First()
                                       .SelectNodes("//article");

                var dataTask = new List<Task<DataObject>>();
                try
                {
                    foreach (var article in articleNodes)
                    {
                        if (IsAPhotoPoll(article)) continue;
                        var dataObject = new DataObject {CurrentPageID = currentPageId};
                        ParseUniqueInfo(article, dataObject);
                        if (_isDone) break;
                        var task = ParseArticle(article, dataObject);
                        dataTask.Add(task);
                        _acount++;
                    }
                }
                catch (Exception e)
                {
                    Logger.Write(e.Message + "\n" + e.StackTrace);
                    throw e;
                }

                var data = await Task.WhenAll(dataTask);
                _storageManager.Archive.Data.AddRange(data);
                
                Task writeTask = null;
                lock (_storageLock)
                {
                    if (_storageManager.Archive.Data.Count >= 10000)
                    {
                        writeTask = WriteToDisk(true);
                        _storageManager.Archive.Data.Clear();
                    }
                }
                
                if (writeTask != null)
                    await writeTask;
                
                html = await nextHtmlTask;

                if (_isDone)
                    break;

                currentPageId = pageOb.NextPageID;
                Console.WriteLine("# of Pages Parsed: " + _acount / 25);
            }

            Console.WriteLine("Answer Count: " + _acount);
        }
        
        private async Task<DataObject> ParseArticle(HtmlNode question, DataObject dataObject)
        {
            var tTask = ParseThreadInfo(question, dataObject);
            var qTask = ParseQuestion(question, dataObject);
            var aTask = ParseAnswer(question, dataObject);
            var vTask = ParseVisuals(question, dataObject);
            var lTask = ParseLikes(question, dataObject);

            await tTask;
            await qTask;
            await aTask;
            await vTask;
            await lTask;
            return dataObject;
        }

        /**
         * If the option -d --endDate is used
         * _isDone will be set to true here once the specified date
         * is reached.
         **/
        private void ParseUniqueInfo(HtmlNode question, DataObject dataObject)
        {
            var nodes = question.SelectNodes(question.XPath + "//a[@class='streamItem_meta']");
            var node = nodes.FirstOrDefault(nd => nd.Attributes.Contains("href")
                                               && nd.Attributes.Contains("title")
                                               && nd.Attributes.Contains("class")
                                               && nd.GetAttributeValue("href", "") != "");

            var date = node.FirstChild.Attributes.First().Value;
            var id   = node.GetAttributeValue("href", "").Split("/").Last().Trim();
            dataObject.AnswerID = id;
            dataObject.Date     = DateTime.ParseExact(date, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            dataObject.Link     = _baseUrl + "/answers/" + dataObject.AnswerID;
            var dateCompare = DateTime.Compare(dataObject.Date, _endDate);

            if (_isDone) return;
            lock (_lock)
            {
                _isDone = dateCompare <= 0;
            }
        }

        private async Task ParseThreadInfo(HtmlNode thread, DataObject dataObject)
        {
            var id    = "";
            var count = "0";
            if (HasThreads(thread))
            {
                var threadNode = thread.SelectNodes(thread.XPath + "//a[@class='streamItem_threadDetails keep-asking']")
                                       .First();
                id    = threadNode.GetAttributeValue("href", "").Split("/").Last();
                count = threadNode.InnerText.Trim().Split(" ")[0];

                await ProcessThreads(id, dataObject.AnswerID);
            }

            dataObject.ThreadID     = id;
            dataObject.NumResponses = int.Parse(count);
        }

        private async Task ParseQuestion(HtmlNode article, DataObject dataObject)
        {
            var node        = article.SelectSingleNode(article.XPath + "//header[@class='streamItem_header']");
            var contentNode = node.SelectSingleNode(node.XPath + "//h2").ChildNodes;
            var authorNode  = node.SelectSingleNode(node.XPath + "//a[@class='author']");

            if (authorNode != null)
            {
                // remove the "/" from href
                dataObject.AuthorID   = authorNode.GetAttributeValue("href", "").Substring(1);
                dataObject.AuthorName = authorNode.InnerText.Trim();
            }

            var question = contentNode.Aggregate("", (current, child)
                                                     => current + child.Name switch
                                                     {
                                                         "#text" => child.InnerText,
                                                         "a"     => "<link>" + child.InnerText + "<\\link>",
                                                         _       => child.InnerText
                                                     });
            dataObject.Question = question.Trim();
        }

        private async Task ParseAnswer(HtmlNode article, DataObject dataObject)
        {
            var node = article.SelectSingleNode(article.XPath + "//div[@class='streamItem_content']") ??
                       article.SelectSingleNode(article.XPath + "//div[@class='asnwerCard_text']");

            if (node == null) return;
            // elements are wrapped in <span> if the language isn't english
            if (node.FirstChild.Name == "span")
                node = node.ChildNodes.FirstOrDefault();

            var answer = node.ChildNodes.Aggregate("", (current, child)
                                                       => current + child.Name switch
                                                       {
                                                           "#text" => child.InnerText,
                                                           "a"     => "<link>" + child.InnerText + "<\\link>",
                                                           "hr"    => "\n\n",
                                                           "span"  => "",
                                                           _       => "\n"
                                                       });

            dataObject.Answer = answer.Trim();
        }

        private async Task ParseVisuals(HtmlNode article, DataObject dataObject)
        {
            var srcUrl = "";
            var node   = article.SelectSingleNode(article.XPath + "//div[@class='streamItem_visual']");
            if (node == null) return;
            var videoNode = node.SelectSingleNode(node.XPath + "//div[@class='rsp-eql-desktop']");

            // visual is a video
            if (videoNode != null)
            {
                var srcNode = videoNode.FirstChild;
                srcUrl                = srcNode.GetAttributeValue("src", "");
                dataObject.VisualType = FileType.VIDEO;
            }
            else
            {
                node = node.SelectSingleNode(node.XPath + "//a");
                if (node == null) {
                    Console.WriteLine("Error Parsing Visuals: " + dataObject.AnswerID);
                    return;
                }
                var visualType = node.GetAttributeValue("data-action", "");
                dataObject.VisualType = visualType.Contains("Gif") ? FileType.GIF : FileType.IMG;
                var attrName = visualType.Contains("Gif") ? "data-src" : "src";
                srcUrl = node.FirstChild.GetAttributeValue(attrName, "");
            }

            if (_storageManager.VisualMap.TryGetValue(srcUrl, out var value))
            {
                dataObject.Visuals = value;
            }
            else
            {
                var extension = srcUrl.Split(".").Last().Trim();
                var fileName  = dataObject.AnswerID;
                fileName           += "." + extension.Trim();
                dataObject.Visuals =  fileName;
                _storageManager.VisualMap.Add(srcUrl, dataObject.Visuals);
                await _client.Download(srcUrl, fileName);
            }

            _vcount++;
        }

        private async Task ParseLikes(HtmlNode article, DataObject dataObject)
        {
            var node      = article.SelectSingleNode(article.XPath + "//div[@class='heartButton']");
            var likesCunt = "";
            foreach (var child in node.ChildNodes)
            {
                if (child.GetAttributeValue("class", "") != "counter")
                {
                    likesCunt = node.InnerText.Trim() == "" ? "0" : node.InnerText.Trim();
                }
            }

            dataObject.Likes = int.Parse(likesCunt);
        }

        

        private async Task ProcessThreads(string threadID, string answerID)
        {
            if (_parseThreads)
            {
                await ParseThreadIDs(threadID);
            }
            else
            {
                lock (_lock)
                {
                    if (ThreadExists(threadID))
                    {
                        if (!_storageManager.ThreadMap[threadID].Contains(answerID))
                            _storageManager.ThreadMap[threadID].Add(answerID);
                    }
                    else
                    {
                        var set = new HashSet<string> {answerID};
                        _storageManager.ThreadMap.Add(threadID, set);
                    }
                }
            }
        }
        
        /*
         * if the option -t --thread is used
         * this function will be called on threads
         * that doesn't exist on the disk database
         * to parse all answer ids associated with that thread
         */
        private async Task ParseThreadIDs(string threadID)
        {
            var set         = new HashSet<string>();
            var url         = _baseUrl + "/threads/" + threadID;
            var html        = await GetHtmlDoc(url);
            var answerNodes = html.DocumentNode.SelectNodes("//a[@class='streamItem_meta']");

            // start at i = 1 to skip the "updated x days ago" node
            for (var i = 1; i < answerNodes.Count; i++)
            {
                var node     = answerNodes[i];
                var answerId = node.GetAttributeValue("href", "").Split("/").Last();
                set.Add(answerId);
            }

            lock (_lock)
            {
                if (!ThreadExists(threadID))
                {
                    _storageManager.ThreadMap.Add(threadID, set);
                }
                else
                {
                    _storageManager.ThreadMap[threadID].UnionWith(set);
                }
            }
        }

        private async Task<HtmlDocument> GetNextPage(HtmlDocument html, DataObject dataObject)
        {
            var nextPageNode = html.DocumentNode.SelectNodes("//a[@class='item-page-next']");
            if (nextPageNode == null)
            {
                _isDone = true;
                return null;
            }
            var nextPageUri = nextPageNode.First().GetAttributeValue("href", "");
            dataObject.NextPageID = nextPageUri.Split("=").Last();
            var nextHtml = await GetHtmlDoc(BaseUrl + nextPageUri);
            
            return nextHtml;
        }

        private async Task<HtmlDocument> GetHtmlDoc(string url)
        {
            var htmlDoc = new HtmlDocument();
            var html    = "";
            try
            {
                html = await _client.HttpRequest(url);
            }
            catch (Exception e)
            {
                Logger.Write("Message:\n" + e.Message + "\nStackTrace:\n" + e.StackTrace);
                await WriteToDisk();
                Environment.Exit(1);
            }

            htmlDoc.LoadHtml(html);
            return htmlDoc;
        }
        
        private bool HasThreads(HtmlNode question)
        {
            var threadNode =
                question.SelectNodes(question.XPath + "//a[@class='streamItem_threadDetails keep-asking']");
            return threadNode != null;
        }

        private bool IsAPhotoPoll(HtmlNode question)
        {
            var node = question.SelectSingleNode(question.XPath + "//div[@class='streamItem_visual photopoll']");
            return node != null;
        }

        private bool ThreadExists(string id)
        {
            return _storageManager.ThreadMap.ContainsKey(id);
        }

        private async Task WriteToDisk(bool dataOnly = false)
        {
            var  archive  = _storageManager.Archive;
            Task dataTask = null, threadsTask = null, visualTask = null;
            
            if (archive.Data.Count != 0)
            {
                var filename = _username + "_" + archive.Data.First().
                                                   Date.ToString("yy-MM-dd_HH-mm");

                archive.User              = _username;
                archive.Header            = _header;
                archive.QuestionCount     = archive.Data.Count;
                archive.VisualCount       = _vcount;
                archive.FirstQuestionDate = archive.Data.Last().Date;
                archive.LastQuestionDate  = archive.Data.First().Date;
                dataTask                  = _fm.SaveData(archive, filename, FileType.JSON);
            }

            if (!dataOnly)
            {
                var threadFileName = DataTypes.Threads + "_" + _username;
                var visualFileName = DataTypes.Visuals + "_" + _username;

                if (_storageManager.ThreadMap.Count != 0)
                    threadsTask = _fm.SaveData(_storageManager.ThreadMap, threadFileName, FileType.JSON);
                if (_storageManager.VisualMap.Count != 0)
                    visualTask = _fm.SaveData(_storageManager.VisualMap, visualFileName, FileType.JSON);
            }

            if (dataTask != null)
                await dataTask;
            if (threadsTask != null)
                await threadsTask;
            if (visualTask != null)
                await visualTask;
        }
    }
}
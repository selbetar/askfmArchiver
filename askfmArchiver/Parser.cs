using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using askfmArchiver.Enums;
using askfmArchiver.Models;
using askfmArchiver.Utils;
using HtmlAgilityPack;

namespace askfmArchiver
{
    public class Parser
    {
        private const string BaseUrl = "https://ask.fm/";

        private readonly string _userId;
        private readonly string _baseUrl;
        private readonly string _pageIterator;
        private readonly DateTime _stopAt;
        private readonly string _output;
        
        private string _userName;
        private readonly List<Answer> _answers;

        private readonly NetworkManager _requestClient;
        
        private bool _isDone;
        private bool _isLastPage;
        private int _ansCount;
        
        private readonly object _lock = new object();
        private readonly object _logLock = new object();
        
        public Parser(string userId, string output, string pageIterator = "",
            DateTime endDate = default)
        {
            _baseUrl = BaseUrl + userId;
            _userId       = userId.ToLower();
            _pageIterator   = pageIterator;
            _stopAt        = endDate;
            _output = output;
            
            _answers = new List<Answer>();
            _requestClient = new NetworkManager();

            _isDone     = false;
            _isLastPage = false;
        }
        
        public async Task Parse()
        {
            var url = _baseUrl;
            if (_pageIterator != "")
                url += "?older=" + _pageIterator;

            try
            {
                if (!DoesUserExist())
                    InsertUser();
            }
            catch (Exception e)
            {
                Logger.WriteLine("Parse() Exception: ", e);
                Environment.Exit(1);
            }
            try
            {
                var htmlTask =  GetHtmlDoc(url);
                var htmlDoc = await htmlTask;
                await ParsePage(htmlDoc);
                Console.Write("\rProgress: {0}%   ", 100);
                Console.WriteLine("Finished Parsing {0} answers.", _ansCount);
            }
            catch (Exception e)
            {
                lock(_logLock)
                {
                    Logger.WriteLine("Parse() Exception: ", e);
                    Logger.WriteLine("Attempting to commit " + _answers.Count + " parsed answers...");
                }

                if (_answers.Count == 0)
                    Environment.Exit(1);
                
                var dbTask = WriteToDb();
                await dbTask;
                
                Environment.Exit(1);
            }

            await WriteToDb();
        }
        
        private async Task ParsePage(HtmlDocument html)
        {
            var currentPageId = _pageIterator;
            var totalAnswerCount = GetAnswerCount(html);
            SetUserName(html);
            
            while (true)
            {
                var pageOb       = new Answer();
                var nextHtmlTask = GetNextPage(html, pageOb);

                // Get the node that contains all of the questions on this page
                var articleNodes = html.DocumentNode.SelectNodes("//div[@class='item-page']")
                    .First()
                    .SelectNodes("//article");

                var dataTask = new List<Task<Answer>>();
                try
                {
                    foreach (var article in articleNodes)
                    {
                        if (IsAPhotoPoll(article)) continue;
                        var dataObject = new Answer {UserId =  _userId, PageId = currentPageId};
                        ParseUniqueInfo(article, dataObject);
                        if (_isDone) break;
                        var task = ParseArticle(article, dataObject);
                        dataTask.Add(task);
                        _ansCount++;
                    }
                }
                catch (Exception e)
                {
                    lock (_logLock)
                    {
                        Logger.WriteLine("ParsePage() Exception: ", e);
                    }

                    if (dataTask.Count != 0)
                    {
                        var dataErr = await Task.WhenAll(dataTask);
                        _answers.AddRange(dataErr);
                    }
                    
                    throw;
                }

                var data = await Task.WhenAll(dataTask);
                _answers.AddRange(data);
                
                html = await nextHtmlTask;
                if (_isLastPage || _isDone)
                    break;

                currentPageId = pageOb.PageId;
                PrintProgress(totalAnswerCount);
            }
        }
        
        private async Task<Answer> ParseArticle(HtmlNode question, Answer dataObject)
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

        private void ParseUniqueInfo(HtmlNode question, Answer dataObject)
        {
            var nodes = question.SelectNodes(question.XPath + "//a[@class='streamItem_meta']");
            var node = nodes.First(nd => nd.Attributes.Contains("href")
                                         && nd.Attributes.Contains("title")
                                         && nd.Attributes.Contains("class")
                                         && nd.GetAttributeValue("href", "") != "");

            var date = node.FirstChild.Attributes.First().Value;
            var id   = node.GetAttributeValue("href", "").Split("/").Last().Trim();
            dataObject.AnswerId = id;
            dataObject.Date     = DateTime.ParseExact(date, "yyyy-MM-ddTHH:mm:ss", 
                CultureInfo.InvariantCulture);
            var dateCompare = DateTime.Compare(dataObject.Date, _stopAt);

            if (_isDone) return;
            lock (_lock)
            {
                _isDone = dateCompare <= 0 || DoesAnswerExist(id);
            }
        }
        
        private async Task ParseThreadInfo(HtmlNode thread, Answer dataObject)
        {
            // threadID equals first ansID
            var id    = dataObject.AnswerId;
            
            if (HasThreads(thread))
            {
                var threadNode = thread.SelectNodes(thread.XPath + 
                                                    "//a[@class='streamItem_threadDetails keep-asking']")
                    .First();
                id    = threadNode.GetAttributeValue("href", "").Split("/").Last();
            }
            
            dataObject.ThreadId     = id;
        }

        private async Task ParseQuestion(HtmlNode article, Answer dataObject)
        {
            var node        = article.SelectSingleNode(article.XPath + "//header[@class='streamItem_header']");
            var contentNode = node.SelectSingleNode(node.XPath + "//h2").ChildNodes;
            var authorNode  = node.SelectSingleNode(node.XPath + "//a[@class='author']");

            if (authorNode != null)
            {
                // remove the "/" from href
                dataObject.AuthorId   = authorNode.GetAttributeValue("href", "").Substring(1);
                dataObject.AuthorName = authorNode.InnerText.Trim();
            }

            var question = contentNode.Aggregate("", (current, child)
                => current + child.Name switch
                {
                    "#text" => child.InnerText,
                    "a"     => "<link>" + child.InnerText + "<\\link>",
                    _       => child.InnerText
                });
            dataObject.QuestionText = question.Trim();
        }

        private async Task ParseAnswer(HtmlNode article, Answer dataObject)
        {
            var node = article.SelectSingleNode(article.XPath + "//div[@class='streamItem_content']") ??
                       article.SelectSingleNode(article.XPath + "//div[@class='asnwerCard_text']");

            if (node == null) return;
            // elements are wrapped in <span> if the language is RTL
            if (node.FirstChild.Name == "span")
                node = node.ChildNodes.First();

            var answer = node.ChildNodes.Aggregate("", (current, child)
                => current + child.Name switch
                {
                    "#text" => child.InnerText,
                    "a"     => "<link>" + child.InnerText + "<\\link>",
                    "hr"    => "\n\n",
                    "span"  => "",
                    _       => "\n"
                });

            dataObject.AnswerText = answer.Trim();
        }

        private async Task ParseVisuals(HtmlNode article, Answer dataObject)
        {
            string srcUrl;
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
                    lock (_logLock)
                    {
                        Logger.WriteLine("ParseVisuals() Error: Couldn't parse visual with answerId: " 
                                         + dataObject.AnswerId);
                    }
                    return;
                }
                var visualType = node.GetAttributeValue("data-action", "");
                dataObject.VisualType = visualType.Contains("Gif") ? FileType.GIF : FileType.IMG;
                var attrName = visualType.Contains("Gif") ? "data-src" : "src";
                srcUrl = node.FirstChild.GetAttributeValue(attrName, "");
            }

            var extension = srcUrl.Split(".").Last().Trim();
            dataObject.VisualId =  dataObject.AnswerId;
            dataObject.VisualUrl = srcUrl;
            dataObject.VisualExt = extension;
           
            var client = new NetworkManager();
            var fm = new FileManager();
            
            var fileName  = dataObject.AnswerId + "." + extension.Trim();
            var file = Path.Combine(_output,"visuals_" + _userId, fileName);

            file = await client.DownloadMedia(srcUrl, file);
            if (file == "") return;
            
            var hash = fm.ComputeHash(file);
            dataObject.VisualHash = hash;
            if (hash == "") return;
            
            var duplicate = IsVisualDuplicate(hash);
            if (duplicate == null) return;
               
            File.Delete(file);
            dataObject.VisualId = duplicate.VisualId;
            dataObject.VisualExt = duplicate.VisualExt;
            dataObject.VisualHash = duplicate.VisualHash;
        }
        
        private async Task ParseLikes(HtmlNode article, Answer dataObject)
        {
            var node      = article.SelectSingleNode(article.XPath + "//div[@class='heartButton']");
            node = node.SelectSingleNode("//a[@class='counter']");
            var likesCount = node.InnerText.Trim() == "" ? "0" : node.InnerText.Trim();
            likesCount = Regex.Replace(likesCount, "[^0-9]", "");
            
            if (!int.TryParse(likesCount, out var count))
            {
                Logger.WriteLine("Couldn't parse likes count for answer with answerId: " + dataObject.AnswerId);
                count = 0;
            }
            dataObject.Likes = count;
        }

        private async Task<HtmlDocument> GetNextPage(HtmlDocument html, Answer dataObject)
        {
            var nextPageNode = html.DocumentNode.SelectNodes("//a[@class='item-page-next']");
            if (nextPageNode == null)
            {
                _isLastPage = true;
                return null;
            }
            var nextPageUri = nextPageNode.First().GetAttributeValue("href", "");
            dataObject.PageId = nextPageUri.Split("=").Last();
            var nextHtml = await GetHtmlDoc(BaseUrl + nextPageUri);
            
            return nextHtml;
        }

        private async Task<HtmlDocument> GetHtmlDoc(string url)
        {
            var htmlDoc = new HtmlDocument();
            var html    = "";
            try
            {
                html = await _requestClient.HttpRequest(url);
            }
            catch (Exception e)
            {
                lock (_logLock)
                {
                    Logger.WriteLine("GetHtmlDoc() Exception: ", e);
                    Logger.WriteLine("Terminating Application..");
                    Environment.Exit(1);
                }
            }

            htmlDoc.LoadHtml(html);
            return htmlDoc;
        }

        private int GetAnswerCount(HtmlDocument html)
        {
            using var db = new MyDbContext();
            var node = html.
                DocumentNode.
                SelectSingleNode("//div[@class='profileStats_number profileTabAnswerCount']");
            var text = node.GetAttributeValue("title", "");
            var count = Regex.Replace(text, "[^0-9]", "");
            
            var answerCount = int.Parse(count);

            if (!DoesUserExist()) return answerCount;
            var parsedCount = db.Answers.Count(u => u.UserId == _userId);
            answerCount = Math.Abs(parsedCount - answerCount);
            return answerCount;
        }
        private void SetUserName(HtmlDocument html)
        {
            _userName = html.DocumentNode.SelectSingleNode("//div[@class='userName_status']")
                .FirstChild.FirstChild.InnerText;
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

        private Answer IsVisualDuplicate(string hash)
        {
            using var db = new MyDbContext();

            var result = db.Answers
                .Where(a => a.VisualHash == hash)
                .OrderBy(a => a.Date)
                .FirstOrDefault();
            
            return result;
        }

        private bool DoesAnswerExist(string ansId)
        {
            using var db = new MyDbContext();
            var exists = db.Answers
                .Any(a => a.AnswerId == ansId);
            return exists;
        }

        private async Task WriteToDb()
        {
            if (_answers.Count == 0)
                return;

            await using var db = new MyDbContext();
            foreach (var c in _answers)
            {
                try
                {
                    db.Add(c);
                }
                catch (Exception e)
                {
                    lock (_logLock)
                    {
                        Logger.WriteLine("WriteToDb Exception: ", e);
                        Logger.WriteLine("Writing data to disk..");
                    }
                    await WriteToDisk();
                    Environment.Exit(1);
                }
            }

            var dbTask =  db.SaveChangesAsync();
            UpdateUser();
            
            await dbTask;
        }
        
        private async Task WriteToDisk()
        {
            Task dataTask = null;
            var fm = new FileManager();
            
            if (_answers.Count != 0)
            {
                var filename = _userId + "_" + _answers.Last().
                    Date.ToString("yy-MM-dd_HH-mm")
                    .Replace("-", "");

                var file = Path.Combine(_output, filename);
                dataTask                  = fm.SaveData(_answers, file, FileType.JSON);
            }
            
            if (dataTask != null)
                await dataTask;
        }

        private void UpdateUser()
        {
            using var db = new MyDbContext();
            var user = db.Users.First(u => u.UserId == _userId);
            user.UserName = _userName;
            user.LastQuestion = _answers.First().Date;
            user.FirstQuestion = user.FirstQuestion == default ? _answers.Last().Date : user.FirstQuestion;
            db.SaveChanges();
        }

        private void InsertUser()
        {
            using var db = new MyDbContext();
            db.Add(new User
            {
                UserId =  _userId, FirstQuestion = default, LastQuestion = default, UserName = _userName
            });

            db.SaveChanges();
        }

        private bool DoesUserExist()
        {
            using var db = new MyDbContext();

            return db.Users.FirstOrDefault(u => u.UserId == _userId) != null;
        }
        
        private void PrintProgress(double totalCount)
        {
            var percent =  _ansCount / totalCount * 100;
            if (percent >= 100)
                percent = 95.00;
            Console.Write("\rProgress: {0}%   ", Math.Round(percent, 2));
        }

    }
}
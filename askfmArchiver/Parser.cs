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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace askfmArchiver
{
    public class Parser : IParser
    {
        private const string BaseUrl = "https://ask.fm/";
        private readonly List<Answer> _answers;

        private string _userName;

        private readonly MyDbContext _dbContext;
        private readonly ILogger<Parser> _log;
        private readonly IOptions _options;
        private readonly INetworkManager _networkManager;
        private readonly IFileManager _fileManager;


        private bool _isDone;
        private bool _isLastPage;

        private int _ansCount;
        private int _totalAnswerCount;

        public Parser(MyDbContext dbContext, ILogger<Parser> logger, IOptions options, INetworkManager networkManager, IFileManager fileManager)
        {
            _answers = new List<Answer>();


            _dbContext = dbContext;
            _log = logger;
            _options = options;
            _networkManager = networkManager;
            _fileManager = fileManager;

            _dbContext.Database.Migrate();

            _isDone = false;
            _isLastPage = false;

            _totalAnswerCount = 0;
        }

        public async Task Parse()
        {

            var url = CreateUrl(BaseUrl, _options.UserId);
            if (_options.PageIterator != "")
                url = CreateUrl(url, "?older=" + _options.PageIterator);

            HtmlDocument html = null;
            try
            {
                html = await GetHtmlDoc(url);
            }
            catch (Exception e)
            {
                _log.LogCritical("Parse(): Terminating the application " +
                                 "due to an error in retrieving the html doc. {errorMsg}\n{errorStack}",
                    e.Message, e.StackTrace);
                Environment.Exit(-1);
            }

            if (!DoesUserExist())
            {
                if (!TryInsertUser())
                {
                    _log.LogCritical("Parse(): Inserting User {user} into the database " +
                                  "has failed. Aborting program execution.",
                        _options.UserId);
                    Environment.Exit(-1);
                }
            }

            SetUserName(html);
            _totalAnswerCount = ExtractAnswerCount(html);
            if (_totalAnswerCount == 0)
            {
                _log.LogInformation("The user {user} has 0 new answers.", _options.UserId);
                return;
            }

            try
            {
                await ParsePage(html);
            }
            catch (Exception e)
            {
                _log.LogCritical("{criticalMsg}\n{criticalStackTrance}", e.Message, e.StackTrace);

                if (_answers.Count == 0)
                    Environment.Exit(-1);

                _log.LogInformation("Attempting to commit {answerCount} answers to the database.", _answers.Count);
                var dbWriteCount = WriteToDb();
                var msg = "";
                if (dbWriteCount == _answers.Count)
                {
                    msg = "Successfully committed {dbWriteCount} answers to the database.";
                }
                else
                {
                    msg = "Failed to commit to the database. Number of rows comnitted: {dbWriteCount}";
                }

                _log.LogInformation(msg, dbWriteCount);
                Environment.Exit(-1);
            }

            WriteToDb();

            Console.Write("\rProgress: {0}%   ", 100);
            Console.WriteLine("Finished Parsing {0} answers.", _ansCount);
        }

        private async Task ParsePage(HtmlDocument html)
        {
            var currentPageId = _options.PageIterator;
            while (true)
            {
                var nextPageTupleTask = GetNextPage(html);

                var articleNodes = GetQuestionNodes(html);
                if (articleNodes == null)
                {
                    _log.LogError("ParsePage(): articleNodes are null. html is:\n{html}", html);
                    break;
                }

                var dataTask = new List<Task<Answer>>();
                foreach (var article in articleNodes)
                {
                    if (IsAPhotoPoll(article)) continue;

                    var dataObject = new Answer { UserId = _options.UserId, PageId = currentPageId };
                    if (!ParseUniqueInfo(article, dataObject) || _isDone) break;

                    var task = ParseArticle(article, dataObject);
                    dataTask.Add(task);

                    _ansCount++;
                }

                var data = await Task.WhenAll(dataTask); ;
                _answers.AddRange(data);

                var (nextHtml, nextPageId) = await nextPageTupleTask;
                if (_isLastPage || _isDone) break;

                html = nextHtml;
                currentPageId = nextPageId;
                PrintProgress(_totalAnswerCount);
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

        private bool ParseUniqueInfo(HtmlNode question, Answer dataObject)
        {
            int dateCompare;
            string id;
            try
            {
                var nodes = question.SelectNodes(question.XPath + "//a[@class='streamItem_meta']");
                var node = nodes.First(nd => nd.Attributes.Contains("href")
                                             && nd.Attributes.Contains("title")
                                             && nd.Attributes.Contains("class")
                                             && nd.GetAttributeValue("href", "") != "");

                var date = node.FirstChild.Attributes.First().Value;
                id = node.GetAttributeValue("href", "").Split("/").Last().Trim();
                dataObject.AnswerId = id;
                dataObject.Date = DateTime.ParseExact(date, "yyyy-MM-ddTHH:mm:ss",
                    CultureInfo.InvariantCulture);
                dateCompare = DateTime.Compare(dataObject.Date, _options.StopAt);
            }
            catch (Exception e)
            {
                _log.LogError("Error during executing ParseUniqueInfo(): {errMsg}\n{stackTrace} ", e.Message, e.StackTrace);
                return false;
            }

            _isDone = dateCompare <= 0 || DoesAnswerExist(id);

            return true;
        }

        private async Task ParseThreadInfo(HtmlNode thread, Answer dataObject)
        {
            // threadID equals first ansID
            var id = dataObject.AnswerId;

            if (HasThreads(thread))
            {
                var threadNode = thread.SelectNodes(thread.XPath +
                                                    "//a[@class='streamItem_threadDetails keep-asking']")
                    .First();
                id = threadNode.GetAttributeValue("href", "").Split("/").Last();
            }

            dataObject.ThreadId = id;
        }

        private async Task ParseQuestion(HtmlNode article, Answer dataObject)
        {
            var node = article.SelectSingleNode(article.XPath + "//header[@class='streamItem_header']");
            var contentNode = node.SelectSingleNode(node.XPath + "//h3").ChildNodes;
            var authorNode = node.SelectSingleNode(node.XPath + "//a[@class='author ']");

            if (authorNode != null)
            {
                // remove the "/" from href
                dataObject.AuthorId = authorNode.GetAttributeValue("href", "").Substring(1);
                dataObject.AuthorName = authorNode.InnerText.Trim();
            }

            var question = contentNode.Aggregate("", (current, child)
                => current + child.Name switch
                {
                    "#text" => child.InnerText,
                    "a" => "<link>" + child.InnerText + "<\\link>",
                    _ => child.InnerText
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
                    "a" => "<link>" + child.InnerText + "<\\link>",
                    "hr" => "\n\n",
                    "span" => "",
                    _ => "\n"
                });

            dataObject.AnswerText = answer.Trim();
        }

        private async Task ParseVisuals(HtmlNode article, Answer dataObject)
        {
            var node = article.SelectSingleNode(article.XPath + "//div[@class='streamItem_visual']");
            if (node == null) return;
            dataObject.VisualId = dataObject.AnswerId;

            var videoNode = node.SelectSingleNode(node.XPath + "//div[@class='rsp-eql-desktop']");

            var srcUrl = videoNode != null ?
                ExtractVideo(videoNode, dataObject)
                : ExtractPhoto(node, dataObject);

            if (srcUrl == "")
            {
                _log.LogWarning("Failed to extract visuals for {userId}:{answerId}.", dataObject.UserId, dataObject.AnswerId);
                return;
            }

            var extension = srcUrl.Split(".").Last().Trim();
            dataObject.VisualUrl = srcUrl;
            dataObject.VisualExt = extension;

            var fileName = dataObject.AnswerId + "." + extension.Trim();
            var file = Path.Combine(_options.Output, "visuals_" + _options.UserId, fileName);

            try
            {
                await _networkManager.DownloadMedia(srcUrl, file);
            }
            catch (Exception e)
            {
                _log.LogWarning("ParseVisuals(): Failed to download media for {userId}:{visualId} with url {url}." +
                              "{errMsg}\n{stackTrace}",
                    dataObject.UserId, dataObject.VisualId, srcUrl, e.Message, e.StackTrace);
                return;
            }

            var hash = _fileManager.ComputeHash(file);
            dataObject.VisualHash = hash;
            if (hash == "") return;

            var duplicate = IsVisualDuplicate(hash);
            if (duplicate == null) return;

            File.Delete(file);
            dataObject.VisualId = duplicate.VisualId;
            dataObject.VisualExt = duplicate.VisualExt;
            dataObject.VisualHash = duplicate.VisualHash;
        }

        private string ExtractVideo(HtmlNode videoNode, Answer dataObject)
        {
            var srcNode = videoNode.FirstChild;
            var srcUrl = srcNode.GetAttributeValue("src", "");
            dataObject.VisualType = FileType.VIDEO;

            if (srcUrl == "")
            {
                _log.LogWarning("ExtractVideo(): Failed to extract video for {userId}:{answerId}",
                    dataObject.UserId, dataObject.AnswerId);
            }
            return srcUrl;
        }

        private string ExtractPhoto(HtmlNode visualNode, Answer dataObject)
        {
            var srcUrl = "";
            var node = visualNode.SelectSingleNode(visualNode.XPath + "//a");
            if (node == null)
            {
                _log.LogWarning("ExtractPhoto(): Failed to extract visual for {userId}:{answerId}",
                    dataObject.UserId, dataObject.AnswerId);
                return "";
            }

            var visualType = node.GetAttributeValue("data-action", "");
            switch (visualType)
            {
                case "GifToggle":
                    dataObject.VisualType = FileType.GIF;
                    var gifNode = node.FirstChild;
                    if (gifNode != null)
                        srcUrl = gifNode.GetAttributeValue("data-src", "");
                    break;

                case "ImageOpen":
                    dataObject.VisualType = FileType.IMG;
                    var picNode = node.SelectSingleNode(node.XPath + "//picture/source");
                    if (picNode != null)
                        srcUrl = picNode.GetAttributeValue("srcset", "");
                    break;

                default:
                    _log.LogWarning("ExtractPhoto(): Failed to extract visual " +
                                  "and visual type for {userId}:{answerId}",
                        dataObject.UserId, dataObject.AnswerId);
                    break;
            }

            if (srcUrl == "")
            {
                _log.LogWarning("ExtractPhoto(): Failed to extract srcUrl {userId}:{answerId}.",
                    dataObject.UserId, dataObject.AnswerId);
            }
            return srcUrl;
        }

        private async Task ParseLikes(HtmlNode article, Answer dataObject)
        {
            var node = article.SelectSingleNode(article.XPath + "//div[@class='heartButton']");
            node = node.SelectSingleNode(node.XPath + "//a[@class='counter']");
            var likesCount = node.InnerText.Trim() == "" ? "0" : node.InnerText.Trim();
            likesCount = Regex.Replace(likesCount, "[^0-9]", "");

            if (!int.TryParse(likesCount, out var count))
            {
                Logger.WriteLine("Couldn't parse likes count for answer with answerId: " + dataObject.AnswerId);
                count = 0;
            }
            dataObject.Likes = count;
        }

        private async Task<Tuple<HtmlDocument, string>> GetNextPage(HtmlDocument html)
        {
            var nextPageNode = html.DocumentNode.SelectNodes("//a[@class='item-page-next']");
            if (nextPageNode == null)
            {
                _isLastPage = true;
                return null;
            }

            var nextPageUri = nextPageNode.First().GetAttributeValue("href", "");
            var nextPageId = nextPageUri.Split("=").Last();
            var url = CreateUrl(BaseUrl, nextPageUri);
            var nextHtml = await GetHtmlDoc(url);

            return new Tuple<HtmlDocument, string>(nextHtml, nextPageId);
        }

        private async Task<HtmlDocument> GetHtmlDoc(string url)
        {
            var html = new HtmlDocument();
            var htmlStr = await _networkManager.HttpRequest(url);
            html.LoadHtml(htmlStr);

            return html;
        }

        private int ExtractAnswerCount(HtmlDocument html)
        {
            var node = html.
                DocumentNode.
                SelectSingleNode("//div[@class='profileStats_number profileTabAnswerCount']");

            if (node == null)
            {
                _log.LogWarning("ExtractAnswerCount(): Couldn't extract the answer count. " +
                                "Progress Info can't be reported.");
                return -1;
            }

            var text = node.GetAttributeValue("title", "");
            var count = Regex.Replace(text, "[^0-9]", "");

            var answerCount = int.Parse(count);
            if (!DoesUserExist() || answerCount == 0) return answerCount;

            var parsedCount = _dbContext.Answers.Count(u => u.UserId == _options.UserId);
            answerCount = Math.Abs(parsedCount - answerCount);
            return answerCount;
        }

        private void SetUserName(HtmlDocument html)
        {
            _userName = html.DocumentNode.SelectSingleNode("//h1[@class='userName_status']")
                .FirstChild.InnerText;
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
            var result = _dbContext.Answers
                .FirstOrDefault(a => a.VisualHash == hash);

            return result;
        }

        private bool DoesAnswerExist(string ansId)
        {
            var exists = _dbContext.Answers
                .Any(a => a.AnswerId == ansId);
            return exists;
        }

        private int WriteToDb()
        {
            if (_answers.Count == 0) return 0;

            var count = -1;
            _dbContext.AddRange(_answers);
            try
            {
                count = _dbContext.SaveChanges();
                UpdateUser();
            }
            catch (Exception e)
            {
                _log.LogCritical("{dbErrorMsg}\n{dbErrorStackTrace}",
                    e.Message, e.StackTrace);
            }

            return count;
        }

        private async Task WriteToDisk()
        {
            Task dataTask = null;
            var fm = new FileManager();

            if (_answers.Count != 0)
            {
                var filename = _options.UserId + "_" + _answers.Last().
                    Date.ToString("yy-MM-dd_HH-mm")
                    .Replace("-", "");

                var file = Path.Combine(_options.Output, filename);
                dataTask = fm.SaveData(_answers, file, FileType.JSON);
            }

            if (dataTask != null)
                await dataTask;
        }

        private void UpdateUser()
        {
            var user = _dbContext.Users.First(u => u.UserId == _options.UserId);
            user.UserName = _userName;
            user.LastQuestion = _answers.First().Date;
            user.FirstQuestion = user.FirstQuestion == default ? _answers.Last().Date : user.FirstQuestion;
            _dbContext.SaveChanges();
        }

        private bool TryInsertUser()
        {
            _dbContext.Add(new User
            {
                UserId = _options.UserId,
                FirstQuestion = default,
                LastQuestion = default,
                UserName = _userName
            });

            try
            {
                _dbContext.SaveChanges();
            }
            catch (Exception e)
            {
                _log.LogError("TryInsertUser():\n {errMsg}\n{stackTrace}", e.Message, e.StackTrace);
                return false;
            }

            return true;
        }

        private bool DoesUserExist()
        {
            return _dbContext.Users.FirstOrDefault(u => u.UserId == _options.UserId) != null;
        }


        private void PrintProgress(double totalCount)
        {
            var percent = _totalAnswerCount / totalCount * 100;
            if (percent >= 100)
                percent = 95.00;
            Console.Write("\rProgress: {0}%   ", Math.Round(percent, 2));
        }

        private string CreateUrl(string baseUrl, params string[] paths)
        {
            var uri = new Uri(baseUrl);
            foreach (var str in paths)
            {
                uri = new Uri(uri, str);
            }

            return uri.ToString();
        }

        private HtmlNodeCollection GetQuestionNodes(HtmlDocument html)
        {
            var nodes = html.DocumentNode.SelectNodes("//div[@class='item-page']");
            if (nodes == null) return null;

            nodes = nodes.First().SelectNodes("//article");
            return nodes;
        }

    }
}
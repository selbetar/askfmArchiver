using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using askfmArchiver.Enums;
using askfmArchiver.Models;
using askfmArchiver.Utils;

namespace askfmArchiver
{
    public class MarkDown
    {
        private  readonly string _userId;
        private readonly string _outDir;
    
        // tid, thread count
        private readonly Dictionary<string, int> _threadMap;
        
        public MarkDown(string userId, string outDir = @"./output")
        {
            _userId = userId;
            _outDir = outDir;
            _threadMap = new Dictionary<string, int>();
        }
        public async Task Generate()
        {
            var answers = GetRecord();

            if (answers.Count == 0)
            {
                Console.WriteLine("Nothing to generate.");
                return;
            }

            var lines = new List<string>();
            var filename = _userId + "_";
            var fileCount = 0;
            string file;
            foreach (var content in answers.Select(ProcessData))
            {
                lines.Add(content);
                if (lines.Count < 5000) continue;
                file = filename + fileCount.ToString("D3");
                await SaveFile(lines, file);
                lines.Clear();
                fileCount++;
            }

            file = filename + fileCount.ToString("D3");
            if (lines.Count != 0)
                await SaveFile(lines, file);

            var info = GenerateHeader();
            UpdatePdfTable(answers.Last().AnswerId, answers.Last().Date);
            await SaveFile(new List<string> {info}, "info_" + _userId);
            Console.WriteLine("Markdown generation has finished: Generated {0} files", fileCount);
        }

        private async Task SaveFile(List<string> lines, string filename)
        {
            var fm = new FileManager();
            var  file = Path.Combine(_outDir, filename);
           await fm.SaveData(lines, file, FileType.MARKDOWN);

        }
        
        private string ProcessData(Answer ans)
        {
            var answer   = ProcessMainText(ans.AnswerText, true);
            var question = ProcessMainText(ans.QuestionText, false);
            var info     = ProcessAnswerInfo(ans);
            var visuals  = "\n";
            if (!string.IsNullOrEmpty(ans.VisualId))
                visuals += ProcessVisuals(ans, ans.VisualType);
            var content = question + answer + "\n" + visuals + info + "***" + "\n";
            return content;
        }


        private string ProcessMainText(string text, bool isAnswer)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var processedText = text;
            var isArabic      = Regex.IsMatch(text, @"\p{IsArabic}");

            if (ContainsLink(processedText))
            {
                processedText = FormatLinks(processedText);
            }

            processedText = processedText.Replace("\n", "<br>");

            if (isArabic)
            {
                // wrap in span so text appears correctly from rtl
                processedText = "<span dir=\"rtl\">" + processedText + "</span>";
            }

            processedText = isAnswer ? ProcessAnswerText(processedText) : ProcessQuestionText(processedText);
            return processedText;
        }

        private string ProcessQuestionText(string text)
        {
            var isArabic = Regex.IsMatch(text, @"\p{IsArabic}");
            if (isArabic)
            {
                text = "<pre style= \"text-align: right\">" + text + "</pre>";
            }
            else
            {
                text = "<pre>" + text + "</pre>";
            }

            text += "\n";
            return text;
        }

        private string ProcessAnswerText(string text)
        {
            var isArabic = Regex.IsMatch(text, @"\p{IsArabic}");
            var temp     = text;
            text = "<div class=\"answer\">" + text + "</div>";

            if (isArabic)
            {
                text = "<div align=\"right\" class=\"answer\">" + temp + "</div>";
            }

            text += "\n";
            return text;
        }

        private string FormatLinks(string text)
        {
            const string pattern = @"<link>(.*?)<\\link>";
            var          matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
            foreach (var match in matches)
            {
                var str = match.ToString().Replace("<link>", "")
                    .Replace("<\\link>", "");
                var alias = str;
                if (str.StartsWith("@"))
                {
                    str = "https://ask.fm/" + str.Substring(1);
                }

                var href = "<a target=\"_blank\" href=\"" + str + "\">" + alias + "</a>";
                text = text.Replace(match.ToString(), href);
            }

            return text;
        }
        
        private string ProcessVisuals(Answer ans, FileType type)
        {
            string visuals;
            var visualFile = ans.VisualId + "." + ans.VisualExt;
            var path = Path.Combine(_outDir,"visuals_" + ans.UserId, visualFile);
            
            if (type != FileType.IMG)
            {
                var answerLink = "https://ask.fm/" + ans.UserId + "/answers/" +  ans.AnswerId;
                var url = string.IsNullOrEmpty(ans.VisualUrl) ? answerLink : ans.VisualUrl;
                visuals = "<a target=\"_blank\" href=\"" + url + "\">Visual: " + type + "</a>";
            }
            else
            {
                visuals = "![MISSING: Visuals Folder](" + path + ")";
            }

            return visuals + "\n\n";
        }
        
        private string ProcessAnswerInfo(Answer answer)
        {
            var processedText = "";
            const string spacing       = "&emsp;&emsp;";
            var link = "https://ask.fm/" + answer.UserId + "/answers/" +  answer.AnswerId; 
            processedText += "<a target=\"_blank\" href=\"" + link + "\">" +
                             answer.Date.ToString("yyyy-MM-dd HH:mm") + "</a>" + spacing;
            processedText += "Likes: " + answer.Likes + spacing;
            processedText += "ThreadCount: " + _threadMap[answer.ThreadId] + "  " + spacing;
            if (string.IsNullOrEmpty(answer.AuthorId)) return processedText + "\n";
            var authorLink = "https://ask.fm/" + answer.AuthorId;
            processedText += "Question By: " + "<a target=\"_blank\" href=\"" + authorLink + "\">"
                             + answer.AuthorId + "</a>";
            return processedText + "\n";
        }

        private string GenerateHeader()
        {
            using var db = new MyDbContext();

            var user = db.Users.First(u => u.UserId == _userId);
            var qCount = db.Answers.Count(u => u.UserId == _userId);
            var vCount = db.Answers.Count(v => v.VisualId != null && v.UserId == _userId);
            var headerText = "";
            headerText += "# " + user.UserName + " Askfm Archive\n";
            headerText += "## File Details:\n";
            headerText += "First Question Date: " + user.FirstQuestion + "\n\n";
            headerText += "Last Question Date: " + user.LastQuestion + "\n\n";
            headerText += "Number of Questions: " + qCount + "\n\n";
            headerText += "Number of Visuals: " + vCount + "\n\n";
            headerText += "---";
            return headerText;
        }
        
        private bool ContainsLink(string text)
        {
            return text.Contains("<link>");
        }
        
        private List<Answer> GetRecord()
        {
            using var db = new MyDbContext();
            var pdfGen = db.PdfGen.FirstOrDefault(u => u.UserId == _userId);
            var stopAt = pdfGen?.StopAt ?? DateTime.MinValue;

            var answers = db.Answers.
                Where(u => u.UserId == _userId && DateTime.Compare(u.Date, stopAt) > 0)
                .OrderBy(u => u.Date).
                ToList();

            if (answers.Count == 0)
                return new List<Answer>();
            
            foreach (var answer in answers.Where(answer => !_threadMap.TryAdd(answer.ThreadId, 0)))
            {
                _threadMap[answer.ThreadId] = _threadMap[answer.ThreadId] + 1;
            }

            return answers;
        }

        private void UpdatePdfTable(string answerId, DateTime stopAt)
        {
            using var db = new MyDbContext();
            var pdfGen = db.PdfGen.FirstOrDefault(u => u.UserId == _userId);
            if (pdfGen != null)
            {
                pdfGen.AnswerId = answerId;
                pdfGen.StopAt = stopAt;
            }
            else
            {
                pdfGen = new PdfGen
                {
                    UserId = _userId,
                    StopAt = stopAt,
                    AnswerId = answerId 
                };
                db.Add(pdfGen);
            }
            db.SaveChanges();
        }
    }
}
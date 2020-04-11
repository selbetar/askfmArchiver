using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using askfmArchiver.Enums;
using askfmArchiver.Objects;
using askfmArchiver.Utils;

namespace askfmArchiver
{
    /**
     * Generate a markdown file
     * with css styles defined in markdown-pdf.css
     */
    public class MarkDown
    {
        public string HeaderName { get; set; }

        private readonly FileManager _fm;
        private readonly List<DataObject> _archive;
        private readonly string _userName, _fileName;

        private int _parsedCount, _visualCount;

        public MarkDown(string userName)
        {
            _userName    = userName;
            _archive     = StorageManager.GetInstance().AnswerData;
            _fileName    = userName + "_" + _archive.First().Date.ToString("yyyy''MM''ddTHH''mm''ss");
            _parsedCount = 0;
            _visualCount = 0;
            _fm          = new FileManager(userName);
        }

        public async Task Generate()
        {
            foreach (var content in _archive.Select(ProcessAnswer))
            {
                await _fm.SaveData(content, _fileName, FileType.MARKDOWN);
            }

            var info = ProcessHeader();
            await _fm.SaveData(info, "Info_" + _fileName, FileType.MARKDOWN);
            Console.WriteLine("parsedCount: " + _parsedCount);
        }

        private string ProcessAnswer(DataObject dataObject)
        {
            var answer   = ProcessMainText(dataObject.Answer, true);
            var question = ProcessMainText(dataObject.Question, false);
            var info     = ProcessAnswerInfo(dataObject);
            var visuals  = "\n";
            if (!string.IsNullOrEmpty(dataObject.Visuals))
                visuals += ProcessVisuals(dataObject.Visuals, dataObject.VisualType);
            _parsedCount++;
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

        private bool ContainsLink(string text)
        {
            return text.Contains("<link>");
        }

        private string ProcessVisuals(string visualID, FileType type)
        {
            var visuals = "";
            var path    = "visuals_" + _userName + "/" + visualID;
            if (type != FileType.IMG)
            {
                visuals = "<a target=\"_blank\" href=\"" + path + "\">Visual Attachment</a>";
            }
            else
            {
                visuals = "![MISSING: Visuals Folder](" + path + ")";
            }

            _visualCount++;
            return visuals + "\n\n";
        }

        private string ProcessAnswerInfo(DataObject answer)
        {
            var processedText = "";
            var spacing       = "&emsp;&emsp;";

            processedText += "<a target=\"_blank\" href=\"" + answer.Link + "\">" +
                             answer.Date.ToString("yyyy-MM-dd HH:mm") + "</a>" + spacing;
            processedText += "Likes: " + answer.Likes + spacing;
            processedText += "ThreadCount: " + answer.NumResponses + "  " + spacing; // + "\n";
            if (string.IsNullOrEmpty(answer.AuthorID)) return processedText + "\n";
            var authorLink = "https://ask.fm/" + answer.AuthorID;
            processedText += "Question By: " + "<a target=\"_blank\" href=\"" + authorLink + "\">"
                           + answer.AuthorID + "</a>";
            return processedText + "\n";
        }

        private string ProcessHeader()
        {
            var headerText = "";
            headerText += "# " + HeaderName + " Answers Archive\n";
            headerText += "## File Details:\n";
            headerText += "First Question Date: " + _archive.Last().Date + "\n\n";
            headerText += "Last Question Date: " + _archive.First().Date + "\n\n";
            headerText += "Number of Question: " + _archive.Count + "\n\n";
            headerText += "Number of Visuals: " + _visualCount + "\n\n";
            headerText += "---\n";
            headerText += "# Questions & Answers\n ";
            return headerText;
        }
    }
}
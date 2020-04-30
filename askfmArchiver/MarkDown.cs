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
        private readonly Archive _archive;
        private readonly FileManager _fm;

        private int _answerCount;
        
        public MarkDown(Archive archive)
        {
            _archive     = archive;
            _answerCount = 0;
            _fm          = new FileManager();
        }

        public async Task Generate()
        {
            var date     = _archive.lastQuestionDate.ToString("yy-MM-dd_HH-mm");
            var filename = _archive.user + "_" + date;
            foreach (var content in _archive.data.Select(ProcessData))
            {
                await _fm.SaveData(content, filename, FileType.MARKDOWN);
            }

            var info = GenerateHeader();
            await _fm.SaveData(info, "INFO_" + filename, FileType.MARKDOWN);
            Console.WriteLine("Processed Answers Count: " + _answerCount);
        }

        private string ProcessData(DataObject dataObject)
        {
            var answer   = ProcessMainText(dataObject.Answer, true);
            var question = ProcessMainText(dataObject.Question, false);
            var info     = ProcessAnswerInfo(dataObject);
            var visuals  = "\n";
            if (!string.IsNullOrEmpty(dataObject.Visuals))
                visuals += ProcessVisuals(dataObject.Visuals, dataObject.VisualType);
            _answerCount++;
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
        
        private string ProcessVisuals(string visualID, FileType type)
        {
            var visuals = "";
            var path    = "visuals_" + _archive.user + "/" + visualID;
            if (type != FileType.IMG)
            {
                visuals = "<a target=\"_blank\" href=\"" + path + "\">Visual Attachment</a>";
            }
            else
            {
                visuals = "![MISSING: Visuals Folder](" + path + ")";
            }

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
        
        private string GenerateHeader()
        {
            var headerText = "";
            headerText += "# " + _archive.header + " Answers Archive\n";
            headerText += "## File Details:\n";
            headerText += "First Question Date: " + _archive.data.Last().Date + "\n\n";
            headerText += "Last Question Date: " + _archive.data.First().Date + "\n\n";
            headerText += "Number of Question: " + _archive.questionCount + "\n\n";
            headerText += "Number of Visuals: " + _archive.visualCount + "\n\n";
            headerText += "---\n";
            headerText += "# Questions & Answers\n ";
            return headerText;
        }
        
        private bool ContainsLink(string text)
        {
            return text.Contains("<link>");
        }

    }
}
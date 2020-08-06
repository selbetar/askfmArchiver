using System;
using askfmArchiver.Enums;

namespace askfmArchiver.Models
{
    public class Answer
    {
        public string UserId { get; set; }
        public string AnswerId { get; set; }
        public string AnswerText { get; set; }
        public string QuestionText { get; set; }
        public string AuthorId { get; set; }
        public string AuthorName { get; set; }
        public DateTime Date { get; set; }
        public int Likes { get; set; }
        public string VisualId { get; set; }
        public FileType VisualType { get; set; }
        public string VisualUrl { get; set; }
        public string VisualExt { get; set; }
        public string VisualHash { get; set; }
        public string ThreadId { get; set; }
        public string PageId { get; set; }
    }
}
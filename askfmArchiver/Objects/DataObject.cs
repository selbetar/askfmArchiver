using System;
using askfmArchiver.Enums;

namespace askfmArchiver.Objects
{
    public class DataObject
    {
        public string Question { get; set; }
        public string AuthorID { get; set; }
        public string AuthorName { get; set; }
        public string Answer { get; set; }
        public string AnswerID { get; set; }
        public DateTime Date { get; set; }
        public string Visuals { get; set; }
        public string Link { get; set; }
        public string CurrentPageID { get; set; }
        public string NextPageID { get; set; }
        public int Likes { get; set; }
        public int NumResponses { get; set; }
        public string ThreadID { get; set; }
        public FileType VisualType { get; set; }
    }
}
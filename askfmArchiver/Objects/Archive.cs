using System;
using System.Collections.Generic;

namespace askfmArchiver.Objects
{
    public class Archive
    {
        public string User { get; set; }
        public string Header { get; set; }
        public string Other { get; set; }
        
        public DateTime LastQuestionDate { get; set; }
        public DateTime FirstQuestionDate { get; set; }
        public int QuestionCount { get; set; }
        public int VisualCount { get; set; }
        
        public List<DataObject> Data { get; set; }
        
    }
}
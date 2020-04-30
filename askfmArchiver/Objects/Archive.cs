using System;
using System.Collections.Generic;

namespace askfmArchiver.Objects
{
    public class Archive
    {
        public string user { get; set; }
        public string header { get; set; }
        public string other { get; set; }
        
        public DateTime lastQuestionDate { get; set; }
        public DateTime firstQuestionDate { get; set; }
        public int questionCount { get; set; }
        public int visualCount { get; set; }
        
        public List<DataObject> data { get; set; }
        
    }
}
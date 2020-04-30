using System.Collections.Generic;
using askfmArchiver.Objects;

namespace askfmArchiver.Utils
{
    public class StorageManager
    {
        private static StorageManager _instance;

        public Archive Archive { get; set; } 
        // key: threadID, value: set of answers found in this thread
        public Dictionary<string, HashSet<string>> ThreadMap { get; set; }
        
        // key: downloadUrl, value: first answer id associated with that link
        public Dictionary<string, string> VisualMap { get; set; }

        private StorageManager()
        {
            ThreadMap = new Dictionary<string, HashSet<string>>();
            VisualMap = new Dictionary<string, string>();
            Archive = new Archive
                      {
                          Data = new List<DataObject>()
                      };
        }

        public static StorageManager GetInstance()
        {
            return _instance ??= new StorageManager();
        }
    }
}
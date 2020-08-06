using System;

namespace askfmArchiver.Models
{
    public class User
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public DateTime LastQuestion { get; set; }
        public DateTime FirstQuestion { get; set; }
    }
}
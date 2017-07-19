using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MarkdownRepository.Models
{
    public class Document
    {
        public int rowid { get; set; }
        public string title { get; set; }
        public string content { get; set; }
        public string category { get; set; }
        public DateTime? creat_at { get; set; }
        public DateTime? update_at { get; set; }
        public string creator { get; set; }
        public DocumentAccess is_public { get; set; }
    }

    public class DocumentOwner
    {
        public long id { get; set; }
        public string creator { get; set; }
        public DateTime creat_at { get; set; }
        public DateTime update_at { get; set; }
    }

    public enum DocumentAccess
    { 
        PUBLIC=1,
        PRIVATE=0
    }
}
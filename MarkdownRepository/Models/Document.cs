using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MarkdownRepository.Models
{
    public class Document
    {
        public long rowid { get; set; }
        public string title { get; set; }
        public string content { get; set; }
        public string category { get; set; }
        public DateTime? creat_at { get; set; }
        public DateTime? update_at { get; set; }
        public string creator { get; set; }
        public DocumentAccess is_public { get; set; }
        public int read_count { get; set; }

        public long ref_book_id { get; set; }
        public long ref_book_directory_id { get; set; }
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

    public class Book
    {
        public long id { get; set; }
        public string creator { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string category { get; set; }
        public string image_url { get; set; }
        public DateTime creat_at { get; set; }
        public DateTime update_at { get; set; }

        public DocumentAccess is_public { get; set; }
    }

    public class BookDirectory
    {
        public long id { get; set; }
        public long book_id { get; set; }        
        public string title { get; set; }
        public string description { get; set; }
        public long parent_id { get; set; }
        public long document_id { get; set; }
        public int seq { get; set; }
    }

    public class BookOwner
    {
        public long id { get; set; }
        public long book_id { get; set; }
        public string user_id { get; set; }
        public bool is_owner { get; set; }
    }

    public class BookVm
    {
        public Book Book { get; set; }
        public IList<BookOwner> BookOwner { get; set; }
        public IList<BookDirectory> BookDirectory { get; set; }
    }
}
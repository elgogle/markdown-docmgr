using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.SQLite;
using System.Data.Common;
using Dapper;
using MarkdownRepository.Models;
using System.Text.RegularExpressions;

//TODO: 对书籍目录顺序的调整

namespace MarkdownRepository.Lib
{
    public class DocumentManager
    {
        private string _dbPath = null;
        private IndexManager _indexMgr = null;
        private static object _lock = new object();

        const string SQLITE_PATH = "~/App_Data";
        const string SQLITE_BACKUP_PATH = "~/App_Data/DataBackup";
        const string INDEX_PATH = "~/App_Data/Index/";

        public DocumentManager()
        {
            this._dbPath = System.IO.Path.Combine(System.Web.HttpContext.Current.Server.MapPath(SQLITE_PATH), "Documents.db3");
            IndexManager.IndexPath = System.Web.HttpContext.Current.Server.MapPath(INDEX_PATH);
            this._indexMgr = IndexManager.IndexMgr;
        }

        public void BackupFile()
        {
            var backupPath = System.Web.HttpContext.Current.Server.MapPath(SQLITE_BACKUP_PATH);

            if (System.IO.Directory.Exists(backupPath) == false) System.IO.Directory.CreateDirectory(backupPath);

            var backupFileName = System.IO.Path.Combine(backupPath, string.Format("Documents_{0}.db3", DateTime.Now.ToString("yyyyMMdd")));

            if (System.IO.File.Exists(backupFileName)) return;

            if (!System.IO.File.Exists(this._dbPath)) return;

            System.IO.File.Copy(this._dbPath, backupFileName);

            // 删除10天前的备份文件
            var allFiles = System.IO.Directory.GetFiles(backupPath);
            var old = DateTime.Now.AddDays(-10).ToString("yyyyMMdd");
            foreach (var f in allFiles)
            {
                var fname = System.IO.Path.GetFileNameWithoutExtension(f);
                var day = fname.Right(8);

                if (day.IsNum() && old.CompareTo(day) > 0)
                {
                    System.IO.File.Delete(f);
                }
            }
        }

        public DocumentManager(string dbPath, string indexPath)
        {
            this._dbPath = dbPath;
            IndexManager.IndexPath = indexPath;
            this._indexMgr = IndexManager.IndexMgr;
        }

        private void CreateDBIfNotExist()
        {
            if (!System.IO.File.Exists(this._dbPath))
            {
                SQLiteConnection.CreateFile(this._dbPath);
            }
        }

        private DbConnection OpenDb()
        {
            this.CreateDBIfNotExist();
            return new SQLiteConnection("data source=" + this._dbPath);
        }

        /// <summary>
        /// 创建 Table
        /// </summary>
        private void CreateTableIfNotExist()
        {
            using (var db = this.OpenDb())
            {
                var sql = @"
create VIRTUAL table if not exists documents USING fts3(title, content, category);
create table if not exists documents_owner(id int primary key, creator nvarchar(100) not null, creat_at datetime default (datetime('now', 'localtime')), update_at datetime default (datetime('now', 'localtime')), is_public int default(0));
create table if not exists documents_category(id INTEGER primary key, category nvarchar(100) not null, doc_id int not null);
create table if not exists documents_file(id INTEGER primary key, file_path nvarchar(512) not null, doc_id int not null);
create table if not exists documents_read_count(id INTEGER primary key, count int not null, doc_id int not null);
create table if not exists documents_follow(id INTEGER primary key, user_id nvarchar(100) not null, doc_id int not null);
create table if not exists user(id INTEGER PRIMARY KEY, user_id nvarchar(100) not null, user_name nvarchar(100) not null);
create table if not exists books(id INTEGER PRIMARY KEY, creator nvarchar(100) not null, name nvarchar(256) not null, description nvarchar(512), category nvarchar(256), image_url nvarchar(512), creat_at datetime default (datetime('now', 'localtime')), update_at datetime default (datetime('now', 'localtime')), is_public int default(0));
create table if not exists book_directories(id INTEGER PRIMARY KEY, book_id int not null, title nvarchar(256) not null, description nvarchar(512), parent_id int, document_id int, seq int);
create table if not exists book_owner(id INTEGER PRIMARY KEY, book_id int not null, user_id nvarchar(100) not null, is_owner int not null);
                ";
                db.Execute(sql);
            }
        }

        /// <summary>
        /// 获取用户名
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public string GetUserName(string userId)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var userName = db.Query<string>("select user_name from user where user_id=@userId", new { userId = userId }).FirstOrDefault();
                return userName;
            }
        }

        /// <summary>
        /// 获取所有账号
        /// </summary>
        /// <returns></returns>
        public IList<UserModel> GetAllUserId()
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var users = db.Query<UserModel>("select user_id, user_name from user").ToList();
                return users;
            }
        }

        /// <summary>
        /// 保存用户名
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="userName"></param>
        public void SaveUserName(string userId, string userName)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                db.Execute("insert or replace into user(user_id,user_name) values(@userId, @userName)",
                    new { userId = userId, userName = userName });
            }
        }

        /// <summary>
        /// 创建文档
        /// </summary>
        /// <param name="content"></param>
        /// <param name="title"></param>
        /// <param name="category"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public Document Create(string content, string title, string category, string userId, DocumentAccess access)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                var max = db.Query<int?>("select max(id) id from documents_owner").FirstOrDefault();
                var document = new Document { category = category, content = content, creator = userId, rowid = (max ?? 0) + 1, title = title };

                SaveCategory(document.rowid, category, db);

                db.Execute("insert into documents_owner(id, creator, is_public) values(@id, @creator, @isPublic)",
                    new { id = document.rowid, creator = document.creator, isPublic = access });

                db.Execute("insert into documents(rowid, title, content, category) values(@rowid, @title, @content, @category)",
                    new { rowid = document.rowid, title = title, content = content, category = category });

                _indexMgr.AddOrUpdateDocIndex(new Doc { Id = document.rowid.ToString(), Category = category, Content = content, Title = title, Operate = Operate.AddOrUpdate });
                return document;
            }
        }

        /// <summary>
        /// 创建 Book
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="category"></param>
        /// <param name="image_url"></param>
        /// <returns></returns>
        public long CreateBook(string userId, string name, string description, string category, string image_url, DocumentAccess access)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new Exception("书名不能为空");

            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                lock (_lock)
                {
                    var bookid = (db.Query<long?>("select max(id) as id from books").Single()??0) + 1;

                    db.Execute(@"
insert into books(id, creator, name, description, category, image_url, is_public) 
values(@id, @creator, @name, @description, @category, @image_url, @is_public);

insert into book_owner(book_id, user_id, is_owner) 
values(@id, @creator, 1);
",
                        new { id = bookid, creator = userId, name, description, category, image_url, is_public = access });

                    _indexMgr.AddOrUpdateDocIndex(new Doc { Id = bookid.ToString(), Category = category, Content = description, Title = name, Operate = Operate.AddOrUpdate });

                    return bookid;
                }
            }
        }

        /// <summary>
        /// 更新书本
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="bookid"></param>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="category"></param>
        /// <param name="image_url"></param>
        public void UpdateBook(string userid, long bookid, string name, string description, string category, string image_url, DocumentAccess access)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new Exception("书名不能为空");

            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                CheckPermissionForUpdateBook(userid, db, bookid);

                db.Execute("update books set name=@name, description=@description, category=@category, image_url=@image_url, is_public = @is_public, update_at = datetime('now', 'localtime')  where id=@id",
                    new { name = name, description = description, category = category, image_url = image_url, id = bookid, is_public = access });

                _indexMgr.AddOrUpdateDocIndex(new Doc { Id = bookid.ToString(), Category = category, Content = description, Title = name, Operate = Operate.AddOrUpdate });
            }
        }

        /// <summary>
        /// 创建书籍目录
        /// </summary>
        /// <param name="bookid"></param>
        /// <param name="title"></param>
        /// <param name="description"></param>
        /// <param name="parentid"></param>
        /// <param name="documentid"></param>
        /// <returns></returns>
        public long CreateBookDirectory(long bookid, string title, string description, long parentid, long documentid, int seq, string userid)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new Exception("目录名称不能为空");

            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                CheckPermissionForUpdateBook(userid, db, bookid);
            
                lock (_lock)
                {
                    var id = (db.Query<long?>("select max(id) as id from book_directories").Single()??0) + 1;

                    db.Execute(@"
insert into book_directories(id, book_id, title, description, parent_id, document_id, seq) 
values(@id, @book_id, @title, @description, @parent_id, @document_id, @seq);",
                        new { id = @id, book_id = bookid, title = title, description = description, parent_id = parentid, document_id = documentid, seq = seq });

                    return id;
                }
            }
        }

        /// <summary>
        /// 更新书籍目录
        /// </summary>
        /// <param name="bookid"></param>
        /// <param name="directoryid"></param>
        /// <param name="title"></param>
        /// <param name="description"></param>
        /// <param name="userid"></param>
        public void UpdateBookDirectory(long bookid, long directoryid, string title, string description, int seq, string userid, long documentid=0)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new Exception("目录名称不能为空");

            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                CheckPermissionForUpdateBook(userid, db, bookid);

                if (documentid > 0)
                {
                    db.Execute("update book_directories set title=@title, description = @description, document_id = @document_id, seq = @seq  where id=@id;",
                            new { id = directoryid, title = title, description = description, document_id = documentid, seq = seq });
                }
                else
                {
                    db.Execute("update book_directories set title=@title, description = @description, seq = @seq where id=@id;",
                            new { id = directoryid, title = title, description = description, seq = seq });
                }
            }
        }

        /// <summary>
        /// 创建或更新书籍文章
        /// </summary>
        /// <param name="directoryid"></param>
        /// <param name="content"></param>
        /// <param name="title"></param>
        /// <param name="userId"></param>
        public void CreateOrUpdateBookArticle(long directoryid, string content, string title, string userId)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                var directory = db.Query<BookDirectory>("select * from book_directories where id=@id", new { id = directoryid }).Single();
                CheckPermissionForUpdateBook(userId, db, directory.book_id);

                var book = db.Query<Book>("select * from books where id=@book_id", new { book_id = directory.book_id }).FirstOrDefault();
                var articleId = db.Query<long>("select document_id from book_directories where id=@id", new { id = directoryid }).FirstOrDefault();
                var docTitle = directory.title.Trim() +
                    (string.IsNullOrWhiteSpace(title)
                    ? ""
                    : ("/" + title.Trim()));
                var docCategory = book.name;

                if (articleId == 0)
                {
                    // 创建文章
                    var doc = Create(content, docTitle, docCategory, userId, book.is_public);
                    // 将文章关联到目录
                    UpdateBookDirectory(directory.book_id, directoryid, directory.title, directory.description, 0, userId, doc.rowid);
                }
                else
                {
                    // 更新文章
                    Update(articleId, content, docTitle, docCategory, book.is_public, userId);
                }

                db.Execute("update books set update_at = datetime('now', 'localtime')  where id=@id", new {  id = book.id });
            }
        }

        /// <summary>
        /// 检查是否有权对书籍更新
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="db"></param>
        /// <param name="bookid"></param>
        private static void CheckPermissionForUpdateBook(string userId, DbConnection db, long bookid)
        {
            var hasPermission = db.Query<bool>("select 1 from book_owner where book_id=@book_id and user_id = @user_id and is_owner=1",
                   new { book_id = bookid, user_id = userId }).FirstOrDefault();
            if (!hasPermission)
            {
                throw new Exception("你无权更新");
            }
        }

        /// <summary>
        /// 删除书籍目录
        /// </summary>
        /// <param name="bookDirId"></param>
        public void DeleteBookDirectory(long bookDirId, string userid)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                var directory = db.Query<BookDirectory>("select * from book_directories where id = @id", new { id = bookDirId }).FirstOrDefault();
                if(directory != null)
                {
                    CheckPermissionForUpdateBook(userid, db, directory.book_id);

                    var bookId = db.Query<long>("select document_id from book_directories where id=@id", new { id = bookDirId }).FirstOrDefault();
                    db.Execute("delete from book_directories where id=@id;", new { id = bookDirId });

                    if (bookId > 0)
                    {
                        Delete(bookId);
                    }
                }
            }
        }

        /// <summary>
        /// 删除一本书籍
        /// </summary>
        /// <param name="bookid"></param>
        public void DeleteBook(long bookid, string userid)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                CheckPermissionForUpdateBook(userid, db, bookid);

                var sql = @"
BEGIN TRANSACTION;
delete from documents_owner where exists(select 1 from book_directories b where b.document_id = documents_owner.id and b.book_id=@book_id); 
delete from documents where exists(select 1 from book_directories b where b.document_id = documents.rowid and b.book_id=@book_id);
delete from documents_category where exists(select 1 from book_directories b where b.document_id = documents_category.doc_id and b.book_id=@book_id);
delete from documents_follow where exists(select 1 from book_directories b where b.document_id = documents_follow.doc_id and b.book_id=@book_id);
delete from book_owner where book_id=@book_id;
delete from book_directories where book_id=@book_id;
delete from books where id=@book_id;
COMMIT;
";
                db.Execute(sql, new { book_id = bookid });
            }
        }

        /// <summary>
        /// 设置书籍是否公开
        /// </summary>
        /// <param name="bookid"></param>
        /// <param name="access"></param>
        public void SetBookState(long bookid, DocumentAccess access)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                db.Execute("update books set is_public= @is_public where id=@id", new { id = bookid, is_public = access });
            }
        }

        /// <summary>
        /// 转让书籍所有权
        /// </summary>
        /// <param name="bookid"></param>
        /// <param name="owner"></param>
        /// <param name="transferid"></param>
        public void TransferBookOwner(long bookid, string owner, string transferid)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                CheckPermissionForUpdateBook(owner, db, bookid);

                db.Execute("update book_owner set user_id=@transferid where book_id = @book_id and is_owner = 1 and user_id = @owner", new { book_id = bookid, owner = owner, transferid = transferid });
            }
        }

        /// <summary>
        /// 获取一本书籍的全部信息
        /// </summary>
        /// <param name="bookid"></param>
        /// <returns></returns>
        public BookVm GetBook(long bookid, string userid)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                var hasPermission = db.Query<bool>(@"
select 1 from books a where is_public=1 
or exists(select 1 from book_owner b where b.book_id = a.id and user_id = @user_id)", 
                    new { user_id = userid }).FirstOrDefault();

                if(!hasPermission)
                {
                    throw new Exception("你无权查看");
                }

                var book = db.Query<Book>("select * from books where id=@id", new { id = bookid }).Single();
                var directories = db.Query<BookDirectory>("select * from book_directories where book_id = @book_id", new { book_id = bookid }).ToList();
                var owner = db.Query<BookOwner>("select * from book_owner where book_id = @id", new { id = bookid }).ToList();
                var result = new BookVm { Book = book, BookDirectory = directories, BookOwner = owner };

                return result;
            }
        }

        /// <summary>
        /// 获取所有书本的拥有人
        /// </summary>
        /// <returns></returns>
        public IEnumerable<BookOwner> GetBookOwner()
        {
            using (var db = this.OpenDb())
            {
                var result = db.Query<BookOwner>("select * from book_owner ");
                return result;
            }
        }

        /// <summary>
        /// 通过文章获取一本书
        /// </summary>
        /// <param name="docId"></param>
        /// <returns></returns>
        public BookVm GetBookByDoc(long docId, string userid)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var directories = db.Query<BookDirectory>("select * from book_directories where document_id = @document_id", new { document_id = docId }).FirstOrDefault();
                var book = GetBook(directories.book_id, userid);
                return book;
            }
        }

        /// <summary>
        /// 通过目录查找文章
        /// </summary>
        /// <param name="directoryid"></param>
        /// <returns></returns>
        public Document GetDocumentByDirectory(long directoryid)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                var directory = db.Query<BookDirectory>("select * from book_directories where id = @id", new { id = directoryid }).FirstOrDefault();
                if (directory == null || directory.document_id == 0)
                    return null;

                var doc = Get(directory.document_id);
                doc.ref_book_directory_id = directory.id;
                doc.ref_book_id = directory.document_id;
                return doc;
            }
        }

        /// <summary>
        /// 获取全部书籍
        /// </summary>
        /// <returns></returns>
        public IList<Book> GetBooks()
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                var books = db.Query<Book>("select * from books a where is_public=1").ToList();
                return books;
            }
        }

        /// <summary>
        /// 获取我的书籍
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public IList<Book> GetMyBooks(string userid)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                var books = db.Query<Book>("select * from books a where exists(select 1 from book_owner b where b.book_id = a.id and b.user_id = @user_id and is_owner=1)", 
                    new { user_id = userid }).ToList();
                return books;
            }
        }

        /// <summary>
        /// 搜索书籍，返回文章
        /// </summary>
        /// <param name="keyword"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public IList<Book> SearchBook(string keyword, string userId)
        {
            var result = new List<Book>();
            var indexResult = _indexMgr.Search(keyword);
            if (indexResult != null && indexResult.Count > 0)
            {
                using (var db = this.OpenDb())
                {
                    CreateTableIfNotExist();

                    var books = db.Query<Book>(@"
select *
from books a
WHERE 1 = 1
    and a.id in @list    
    and (a.is_public = 1 or exists(select 1 from book_owner bo where bo.book_id = a.id and bo.user_id = @userId))                                             
",
                    new { list = indexResult.Select(t => t.Id).ToList(), userId = userId });

                    //TODO: 需要改善，这里又重排序，达到与 index 搜索一致
                    foreach (var i in indexResult)
                    {
                        var first = books.FirstOrDefault(t => t.id.ToString() == i.Id);
                        if (first != null) result.Add(first);
                    }

                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// 保存文档类别
        /// </summary>
        /// <param name="id"></param>
        /// <param name="category"></param>
        /// <param name="db"></param>
        public void SaveCategory(long id, string category, DbConnection db)
        {
            var ca = category.Split(',');

            db.Execute("delete from documents_category where doc_id=@id", new { id = id });

            if (string.IsNullOrWhiteSpace(category)) return;

            foreach (var c in ca)
            {                
                db.Execute(@"insert or replace into documents_category(category, doc_id) values(@category, @id)",
                        new { category = c.Trim(), id = id });
            }
        }

        /// <summary>
        /// 获取我的文档的类别
        /// </summary>
        /// <returns></returns>
        public List<dynamic> GetMyCategory(string userId)
        {
            using (var db = this.OpenDb())
            {
                var sql = @"
select category, count(*) hint 
from documents_category a, documents_owner b 
where a.doc_id = b.id and b.creator=@creator
and not exists(
    select 1 from book_directories d where d.document_id = b.id
)
group by category 
order by count(*) desc";

                return db.Query<dynamic>(sql, new { creator = userId }).ToList();
            }
        }

        /// <summary>
        /// 获取文档所有类别
        /// </summary>
        /// <returns></returns>
        public List<dynamic> GetCategory()
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var sql = @"
select category, count(*) hint 
from documents_category a, documents_owner b 
where a.doc_id = b.id and b.is_public=1 
and not exists(
    select 1 from book_directories d where d.document_id = b.id
)
group by category 
order by count(*) desc";

                return db.Query<dynamic>(sql).ToList();
            }
        }

        /// <summary>
        /// 获取关注的文章类别
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<dynamic> GetFollowCategory(string userId)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                return db.Query<dynamic>(@"select category, count(*) hint from documents_category a, documents_owner b, documents_follow c  
                                            where a.doc_id = b.id and b.is_public=1 and c.doc_id = a.doc_id
                                                and c.user_id = @userId
                                            group by category order by count(*) desc", new { userId = userId }).ToList();
            }
        }

        /// <summary>
        /// 获取创建人
        /// </summary>
        /// <returns></returns>
        public List<string> GetCreator()
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                return db.Query<string>("select distinct creator from documents_owner order by creator asc").ToList();
            }
        }

        /// <summary>
        /// 创建或更新文档
        /// </summary>
        /// <param name="id"></param>
        /// <param name="content"></param>
        /// <param name="title"></param>
        /// <param name="category"></param>
        public void Update(long id, string content, string title, string category, DocumentAccess access, string userId)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                SaveCategory(id, category, db);

                db.Execute("update documents_owner set update_at=datetime('now', 'localtime'), is_public=@access, creator=@userId where id=@id",
                    new { id = id, access = access, userId = userId });

                db.Execute("update documents set title=@title, content=@content, category=@category where rowid=@rowid",
                    new { rowid = id, title = title, content = content, category = category });

                _indexMgr.AddOrUpdateDocIndex(new Doc { Id = id.ToString(), Category = category, Content = content, Title = title, Operate = Operate.AddOrUpdate });
                //ClearNotExistsAtachFile(id);
            }
        }

        /// <summary>
        /// 删除文档
        /// </summary>
        /// <param name="id"></param>
        public void Delete(long id)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var sql = @"
delete from documents_owner where id=@id; 
delete from documents where rowid=@id;
delete from documents_category where doc_id=@id;
delete from documents_follow where doc_id=@id;
";
                db.Execute(sql, new { id = id });
                _indexMgr.AddOrUpdateDocIndex(new Doc { Id = id.ToString(), Operate = Operate.Delete });
                //DeleteAtachFile(id);
            }
        }

        /// <summary>
        /// 删除附件
        /// </summary>
        /// <param name="id"></param>
        private void DeleteAtachFile(long id)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var filePath = db.Query<string>("select file_path from documents_file where doc_id=@id",
                    new { id = id });
                foreach (var f in filePath)
                {
                    if (System.IO.File.Exists(f))
                    {
                        System.IO.File.Delete(f);
                    }
                }

                db.Execute("delete from documents_file where doc_id=@id", new { id = id });
            }
        }

        private void ClearNotExistsAtachFile(long id)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var content = db.Query<string>("select content from documents where rowid=@id", new { id = id }).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(content))
                    DeleteAtachFile(id);
                else
                {
                    var filePath = db.Query<string>("select file_path from documents_file where doc_id=@id",
                        new { id = id });
                    foreach (var f in filePath)
                    {
                        if (!content.Contains(f))
                        {
                            if (System.IO.File.Exists(f)) System.IO.File.Delete(f);
                            db.Execute("delete from documents_file  where doc_id=@id and file_path=@path",
                                new { id = id, path = f });
                        }
                    }
                }
            }
        }

        private void AddAtachFile(long id, string path)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                db.Execute("insert into documents_file(file_path, doc_id) values(@path, @id)", new { path = path, id = id });
            }
        }

        /// <summary>
        /// 通过文档id获取文档
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Document Get(long id)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var document = db.Query<Document>(@"select b.id as rowid, a.title, a.content, a.category, b.creat_at, b.update_at, b.creator, b.is_public, 
                                                        ifnull(c.count,1) as read_count, ifnull(d.id, 0) as ref_book_directory_id, ifnull(d.book_id, 0) as ref_book_id
                                                    from documents a 
                                                    inner join documents_owner b on a.rowid = b.id
                                                    left outer join documents_read_count c on b.id = c.doc_id 
                                                    left outer join book_directories d on d.document_id = b.id
                                                    where b.id=@id;

                                                    insert into documents_read_count(count, doc_id) select 1, @id where not exists(select 1 from documents_read_count where doc_id=@id);
                                                    update documents_read_count set count = count + 1 where doc_id=@id;
                                                    ",
                                                                                      new { id = id }).FirstOrDefault();
                if (!_indexMgr.Exists(id.ToString()))
                    _indexMgr.AddOrUpdateDocIndex(new Doc { Title = document.title, Content = document.content, Category = document.category, Id = id.ToString(), Operate = Operate.AddOrUpdate });

                return document;
            }
        }

        /// <summary>
        /// 获取关注的所有文章
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<Document> GetFollowDocuments(string userId)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var docs = db.Query<Document>(@"
select distinct b.id as rowid, 
    a.title, 
    a.content, 
    a.category,
    b.creat_at, 
    b.update_at, 
    b.creator
from documents a, documents_owner b, documents_category c, documents_follow d 
where a.rowid = b.id 
    and c.doc_id = b.id 
    and d.doc_id = b.id
    and d.user_id = @userId;
                ", new { userId = userId });
                return docs.ToList();
            }
        }

        /// <summary>
        /// 获取最近更新的文件
        /// </summary>
        /// <param name="days">过去几天</param>
        /// <returns></returns>
        public List<Document> GetLatestDocuments(int days = -15)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var docs = db.Query<Document>(@"
select distinct b.id as rowid, 
    a.title, 
    a.content, 
    a.category,
    b.creat_at, 
    b.update_at, 
    b.creator
from documents a, documents_owner b
where a.rowid = b.id
    and b.is_public = 1
    and b.update_at > @updateAt
", new { updateAt = DateTime.Now.AddDays(days) });
                return docs.ToList();
            }
        }

        /// <summary>
        /// 关注文章
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="docId"></param>
        public void FollowDocument(string userId, long docId)
        {
            using (var db = this.OpenDb())
            {
                db.Execute(@"insert or replace into documents_follow(user_id, doc_id) values(@userId, @id);",
                    new { userId = userId, id = docId });
            }
        }

        /// <summary>
        /// 取消文章的关注
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="docId"></param>
        public void CancelFollow(string userId, long docId)
        {
            using (var db = this.OpenDb())
            {
                db.Execute(@"delete from documents_follow where user_id=@userId and doc_id=@id;",
                    new { userId = userId, id = docId });
            }
        }

        /// <summary>
        /// 检查是否已经关注文章
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="docId"></param>
        /// <returns></returns>
        public bool IsFollowed(string userId, long docId)
        {
            using (var db = this.OpenDb())
            {
                var isExists = db.Query<int>("select 1 from documents_follow where user_id=@userId and doc_id=@id;",
                    new { userId = userId, id = docId }).FirstOrDefault();
                return isExists == 1 ? true : false;
            }
        }

        /// <summary>
        /// 通过文档Category索引文档
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public List<Document> SearchByCategory(string category, string userId="")
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var documents = db.Query<Document>(@"select distinct b.id as rowid, title, 
                                                        content, 
                                                        a.category,
                                                        creat_at, update_at, creator
                                                    from documents a, documents_owner b, documents_category c 
                                                    WHERE a.rowid = b.id and c.doc_id = b.id 
                                                        and c.category = @category 
                                                        and (b.creator = @userId or (@userId='' and b.is_public=1) )
                                                        and not exists(select 1 from book_directories d where document_id = a.rowid)",
                                                                                               new { category = category, userId = userId });
                return documents.ToList();
            }
        }

        /// <summary>
        /// 搜索文档
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public List<Document> Search(string queryText, string userId = "")
        {
            // TODO: 搜索书籍
            var result = new List<Document>();
            var indexResult = _indexMgr.Search(queryText);
            if (indexResult != null && indexResult.Count > 0)
            {
                using (var db = this.OpenDb())
                {
                    CreateTableIfNotExist();

                    var documents = db.Query<Document>(@"
select b.id as rowid, 
    a.title, 
    a.content, 
    a.category,
    b.creat_at, 
    b.update_at, 
    b.creator,
    ifnull(d.id, 0) as ref_book_directory_id,
    ifnull(d.book_id, 0) as ref_book_id
from documents a
inner join documents_owner b on b.id = a.rowid
left outer join book_directories d on d.document_id = b.id
WHERE  b.id in @list 
    and (b.creator = @userId 
        or (b.creator <> @userId and b.is_public=1) 
        or (
            select 1 from book_directories c 
            inner join book_owner d on d.book_id = c.book_id
            where c.document_id = a.rowid
                and d.user_id = @userId
            )
    )
",
                                     new { list = indexResult.Select(t => t.Id).ToList(), userId = userId });
                    //TODO: 需要改善，这里又重排序，达到与 index 搜索一致
                    foreach (var i in indexResult)
                    {
                        var first = documents.FirstOrDefault(t => t.rowid.ToString() == i.Id);
                        if (first != null) result.Add(first);
                    }

                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// 我的文档
        /// </summary>
        /// <returns></returns>
        public List<Document> MyDocument(string userId)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                var documents = db.Query<Document>(@"select id as rowid, title, content, category, creat_at, update_at 
                                                    from documents a, documents_owner b 
                                                    where a.rowid = b.id and b.creator=@creator
                                                        and not exists(select 1 from book_directories d where document_id = a.rowid)
                                                    order by update_at desc",
                                                                                                new { creator = userId });
                return documents.ToList();
            }
        }

        /// <summary>
        /// 获取全部公开文章
        /// </summary>
        /// <param name="orderBy"></param>
        /// <returns></returns>
        public IEnumerable<Document> AllDocument(string orderBy)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var sql = @"
select id as rowid, title, content, category, creat_at, update_at, creator
from documents a, documents_owner b 
where a.rowid = b.id and b.is_public=1
    and not exists(select 1 from book_directories d where document_id = a.rowid)
order by update_at desc
";
                if(orderBy.IsNullOrEmpty() == false && orderBy.ToLower().Equals("read_count"))
                {
                    sql = @"
select distinct b.id as rowid, a.title, a.content, a.category, b.creat_at, b.update_at, b.creator
from documents a
inner join documents_owner b on a.rowid = b.id and b.is_public=1
left outer join documents_read_count c on b.id = c.doc_id
where not exists(select 1 from book_directories d where document_id = a.rowid)
order by ifnull(c.count,1) desc
";
                }
               

                var documents = db.Query<Document>(sql);
                return documents;
            }
        }
    }
}

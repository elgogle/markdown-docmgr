using Dapper;
using MarkdownRepository.Models;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace MarkdownRepository.Lib
{
    public class DocumentManager
    {
        private string _dbPath = null;
        private DocumentSearchManager _indexMgr = null;
        private static object _lock = new object();
        private static bool _databaseCreated = false;
        const string SQLITE_PATH = "~/App_Data";
        const string SQLITE_BACKUP_PATH = "~/App_Data/DataBackup";
        const string INDEX_PATH = "~/App_Data/Index/";

        public DocumentSearchManager IndexManager
        {
            get
            {
                return this._indexMgr;
            }
        }

        public DocumentManager()
        {
            this._dbPath = System.IO.Path.Combine(System.Web.HttpContext.Current.Server.MapPath(SQLITE_PATH), "Documents.db3");
            DocumentSearchManager.IndexPath = System.Web.HttpContext.Current.Server.MapPath(INDEX_PATH);
            this._indexMgr = DocumentSearchManager.IndexMgr;
        }

        public DocumentManager(string dbPath, string indexPath)
        {
            this._dbPath = dbPath;
            DocumentSearchManager.IndexPath = indexPath;
            this._indexMgr = DocumentSearchManager.IndexMgr;
        }

        /// <summary>
        /// 添加附件信息到数据库表中
        /// </summary>
        /// <param name="uploadId"></param>
        /// <param name="id"></param>
        /// <param name="path"></param>
        public void AddAtachFile(string uploadId, long id, string path)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                db.Execute("insert into documents_file(file_path, doc_id, upload_id) values(@path, @id, @uploadId)", new { path = path, id = id, uploadId = uploadId });
            }
        }

        /// <summary>
        /// 添加用户到用户组
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="userGroupRowid"></param>
        public void AddUserToGroup(string creator, string userId, long userGroupRowid)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var isGroupOwner = db.Query<bool>("select 1 from user_group where rowid=@group_rowid and creator=@creator",
                    new { group_rowid = userGroupRowid, creator = creator }).FirstOrDefault();
                if (isGroupOwner)
                {
                    db.Execute("insert or replace into user_group_member(group_rowid, user_id, creator) values(@group_rowid, @user_id, @creator)",
                        new { group_rowid = userGroupRowid, user_id = userId, creator = creator });
                }
                else
                {
                    throw new Exception("这个用户组不是你创建，你无权操作");
                }
            }
        }

        /// <summary>
        /// 列出在某个用户组中的所有用户 id
        /// </summary>
        /// <param name="userGroupRowid"></param>
        /// <returns></returns>
        public List<string> ListUsersInUserGroup(string userId, long userGroupRowid)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var isGroupOwner = db.Query<bool>("select 1 from user_group where rowid=@group_rowid and creator=@creator",
                    new { group_rowid = userGroupRowid, creator = userId }).FirstOrDefault();
                if (isGroupOwner)
                {
                    var users = db.Query<string>("select user_id from user_group_member where group_rowid=@group_rowid",
                        new { group_rowid = userGroupRowid }).ToList();
                    return users;
                }
                else
                {
                    throw new Exception("这个用户组不是你创建，你无权操作");
                }
            }
        }

        /// <summary>
        /// 列出我所在的用户组
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<UserGroup> ListUserGroupsOfMe(string userId)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var groups = db.Query<UserGroup>(@"
select distinct u.rowid, u.group_name, u.group_description
from user_group u, user_group_member m
where m.user_id = @user_id
    and u.rowid = m.group_rowid
", new { user_id = userId }).ToList();

                return groups;
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

                #region sql
                var sql = @"
select id as rowid, title, content, category, creat_at, update_at, creator
from documents a, documents_owner b 
where a.rowid = b.id and b.is_public=1
    and not exists(select 1 from book_directories d where document_id = a.rowid)
order by update_at desc
";
                #endregion

                if (orderBy.IsNullOrEmpty() == false && orderBy.ToLower().Equals("read_count"))
                {
                    #region sql
                    sql = @"
select distinct b.id as rowid, a.title, a.content, a.category, b.creat_at, b.update_at, b.creator
from documents a
inner join documents_owner b on a.rowid = b.id and b.is_public=1
left outer join documents_read_count c on b.id = c.doc_id
where not exists(select 1 from book_directories d where document_id = a.rowid)
order by ifnull(c.count,1) desc
";
                    #endregion
                }


                var documents = db.Query<Document>(sql);
                return documents;
            }
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

        /// <summary>
        /// 调整目录顺序
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="bookId"></param>
        /// <param name="dirId"></param>
        /// <param name="seq"></param>
        /// <param name="parentId"></param>
        public void BookDirectoryMove(string userid, long bookId, long dirId, int seq, int oldSeq, long parentId, long oldParentId)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                CheckPermissionForUpdateBook(userid, db, bookId);

                var sql = @"
update book_directories set parent_id=@parent_id, seq=@seq where book_id=@book_id and id=@id;
";
                db.Execute(sql, new { parent_id = parentId, seq = seq, book_id = bookId, id = dirId });

                if (parentId == oldParentId)
                {
                    // 父目录没有改变的情况
                    if (seq < oldSeq)
                    {
                        // 往上移
                        var sql2 = @"
update book_directories set seq=seq+1 where book_id=@book_id and parent_id=@parent_id and seq between @seq and @oldSeq and id <> @id
";
                        db.Execute(sql2, new { parent_id = parentId, seq = seq, oldSeq = oldSeq, book_id = bookId, id = dirId });
                    }
                    else
                    {
                        // 往下移
                        var sql2 = @"
update book_directories set seq=seq-1 where book_id=@book_id and parent_id=@parent_id and seq between @oldSeq and @seq and id <> @id
";
                        db.Execute(sql2, new { parent_id = parentId, seq = seq, oldSeq = oldSeq, book_id = bookId, id = dirId });
                    }
                }
                else
                {
                    // 改变父目录

                    // 更改插入的目录中受影响的目录的顺序
                    var sql2 = @"
update book_directories set seq=seq+1 where book_id=@book_id and parent_id=@parent_id and seq >= @seq and id <> @id
";
                    db.Execute(sql2, new { parent_id = parentId, seq = seq, book_id = bookId, id = dirId });

                    // 更改原来所在的父目录中的所有目录顺序
                    var sql3 = @"
update book_directories set seq=seq-1 where book_id=@book_id and parent_id=@parent_id and seq > @seq
";
                    db.Execute(sql3, new { parent_id = oldParentId, seq = oldSeq, book_id = bookId });
                }
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

        public bool CanUpdateDocByUser(string userId, long docId)
        {
            using (var db = this.OpenDb())
            {
                var isDocOwner = db.Query<bool>("select 1 from documents_owner where id=@docId and creator=@creator",
                    new { creator = userId, docId = docId }).FirstOrDefault();
                if (isDocOwner) return true;

                var isDocShareToUser = db.Query<bool>(@"
select 1
from documents_share s, user_group_member u
where s.user_group_rowid = u.group_rowid
    and s.doc_id = @docId
    and u.user_id = @userId
", new { userId, docId }).FirstOrDefault();
                if (isDocShareToUser) return true;

                var book = GetBookByDoc(docId, userId);
                if (book != null)
                {
                    CheckPermissionForUpdateBook(userId, db, book.Book.id);
                    return true;
                }

                return false;
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
            var hasPermission = db.Query<bool>("select 1 from book_owner where book_id=@book_id and user_id = @user_id",
                   new { book_id = bookid, user_id = userId }).FirstOrDefault();
            if (!hasPermission)
            {
                throw new Exception("你无权更新");
            }
        }

        /// <summary>
        /// 检查是否有权删除书籍
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="db"></param>
        /// <param name="bookid"></param>
        private static void CheckIfIsBookOwner(string userId, DbConnection db, long bookid)
        {
            var hasPermission = db.Query<bool>("select 1 from book_owner where book_id=@book_id and user_id = @user_id and is_owner=1",
                   new { book_id = bookid, user_id = userId }).FirstOrDefault();
            if (!hasPermission)
            {
                throw new Exception("你无权更新");
            }
        }

        /// <summary>
        /// 是否为书籍拥有人
        /// </summary>
        /// <param name="bookId"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public bool IsBookOwner(long bookId, string userId)
        {
            using (var db = this.OpenDb())
            {
                var hasPermission = db.Query<bool>("select 1 from book_owner where book_id=@book_id and user_id = @user_id and is_owner=1",
                  new { book_id = bookId, user_id = userId }).FirstOrDefault();

                return hasPermission;
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

                var document = new Document { category = category, content = content, creator = userId, rowid = GetId("document"), title = title };

                SaveCategory(document.rowid, category, db);

                db.Execute("insert into documents_owner(id, creator, is_public) values(@id, @creator, @isPublic)",
                    new { id = document.rowid, creator = document.creator, isPublic = access });

                db.Execute("insert into documents(rowid, title, content, category) values(@rowid, @title, @content, @category)",
                    new { rowid = document.rowid, title = title, content = content, category = category });

                db.Execute("insert into documents_history(creator, doc_id, title, content, category) values(@creator, @docId, @title, @content, @category)",
                    new { creator = userId, docId = document.rowid, title = title, content = content, category = category });

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

                var bookid = GetId("book");

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

                var id = GetId("book_directories");

                lock (_lock)
                {
                    // 获取目录顺序号
                    var dbSeq = db.Query<int>("select count(*) as cnt from book_directories where book_id=@book_id and parent_id=@parent_id",
                        new { book_id = bookid, parent_id = parentid }).FirstOrDefault();

                    seq = dbSeq > seq ? dbSeq : seq;

                    db.Execute(@"
insert into book_directories(id, book_id, title, description, parent_id, document_id, seq) 
values(@id, @book_id, @title, @description, @parent_id, @document_id, @seq);",
                        new { id = @id, book_id = bookid, title = title, description = description, parent_id = parentid, document_id = documentid, seq = seq });
                }

                return id;
            }
        }

        private void CreateDBIfNotExist()
        {
            if (!System.IO.File.Exists(this._dbPath))
            {
                SQLiteConnection.CreateFile(this._dbPath);
            }
        }

        /// <summary>
        /// 创建或更新书籍文章
        /// </summary>
        /// <param name="directoryid"></param>
        /// <param name="content"></param>
        /// <param name="title"></param>
        /// <param name="userId"></param>
        /// <returns>文章 id</returns>
        public long CreateOrUpdateBookArticle(long directoryid, string content, string title, string userId)
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
                    articleId = doc.rowid;
                    // 将文章关联到目录
                    UpdateBookDirectory(directory.book_id, directoryid, directory.title, directory.description, 0, userId, doc.rowid);
                }
                else
                {
                    // 更新文章
                    Update(articleId, content, docTitle, docCategory, book.is_public, userId);
                }

                db.Execute("update books set update_at = datetime('now', 'localtime')  where id=@id", new { id = book.id });

                return articleId;
            }
        }

        /// <summary>
        /// 创建 Table
        /// </summary>
        private void CreateTableIfNotExist()
        {
            if (_databaseCreated == false)
            {
                using (var db = this.OpenDb())
                {
                    var sql = @"
create VIRTUAL table if not exists documents USING fts3(title, content, category);
create table if not exists documents_owner(id int primary key, creator nvarchar(100) not null, creat_at datetime default (datetime('now', 'localtime')), update_at datetime default (datetime('now', 'localtime')), is_public int default(0));
create table if not exists documents_category(id INTEGER primary key, category nvarchar(100) not null, doc_id int not null);
create table if not exists documents_file(id INTEGER primary key, file_path nvarchar(512) not null, doc_id int not null, upload_id varchar(50));
create table if not exists documents_read_count(id INTEGER primary key, count int not null, doc_id int not null);
create table if not exists documents_follow(id INTEGER primary key, user_id nvarchar(100) not null, doc_id int not null);
create table if not exists user(id INTEGER PRIMARY KEY, user_id nvarchar(100) not null, user_name nvarchar(100) not null);
create table if not exists books(id INTEGER PRIMARY KEY, creator nvarchar(100) not null, name nvarchar(256) not null, description nvarchar(512), category nvarchar(256), image_url nvarchar(512), creat_at datetime default (datetime('now', 'localtime')), update_at datetime default (datetime('now', 'localtime')), is_public int default(0));
create table if not exists book_directories(id INTEGER PRIMARY KEY, book_id int not null, title nvarchar(256) not null, description nvarchar(512), parent_id int, document_id int, seq int);
create table if not exists book_owner(id INTEGER PRIMARY KEY, book_id int not null, user_id nvarchar(100) not null, is_owner int not null);
create table if not exists id_generator(id INTEGER PRIMARY KEY, ikey nvarchar(100) not null, ivalue int not null);
create table if not exists documents_history(doc_id int not null, create_at datetime default (datetime('now', 'localtime')), creator nvarchar(100), title nvarchar(512), content text, category nvarchar(100));
create table if not exists user_group(group_name nvarchar(100) primary key, group_description nvarchar(255), creator nvarchar(100), create_at datetime default (datetime('now', 'localtime')));
create table if not exists user_group_member(group_rowid int not null, user_id not null, creator nvarchar(100), create_at datetime default (datetime('now', 'localtime')));
create table if not exists documents_share(doc_id int not null, user_group_rowid, creator nvarchar(100), create_at datetime default (datetime('now', 'localtime')));
                ";
                    db.Execute(sql);

                    try
                    {
                        // alter table
                        db.Execute("alter table documents_file add column upload_id varchar(50)");
                    }
                    catch { }

                    _databaseCreated = true;
                }
            }


        }

        /// <summary>
        /// 创建用户组
        /// </summary>
        /// <param name="creator"></param>
        /// <param name="groupName"></param>
        /// <param name="groupDescription"></param>
        public long CreateUserGroup(string creator, string groupName, string groupDescription)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                var id = GetId("user_group");
                db.Execute(@"
insert into user_group(rowid, group_name, group_description, creator) values(@group_rowid, @group_name, @group_description, @creator);
insert into user_group_member(group_rowid, user_id, creator) values(@group_rowid, @creator, @creator);
",
                    new { group_rowid = id, group_name = groupName, group_description = groupDescription, creator = creator });
                return id;
            }
        }

        /// <summary>
        /// 列出我创建的用户组
        /// </summary>
        /// <param name="creator"></param>
        /// <returns></returns>
        public List<UserGroup> ListMyUserGroup(string creator)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var myUserGroups = db.Query<UserGroup>("select rowid, group_name, group_description from user_group where creator=@creator",
                    new { creator }).ToList();

                return myUserGroups;
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
BEGIN TRANSACTION;
delete from documents_owner where id=@id; 
delete from documents where rowid=@id;
delete from documents_category where doc_id=@id;
delete from documents_follow where doc_id=@id;
COMMIT;
";
                db.Execute(sql, new { id = id });
                _indexMgr.AddOrUpdateDocIndex(new Doc { Id = id.ToString(), Operate = Operate.Delete });
                DeleteAtachFiles(id);
            }
        }

        /// <summary>
        /// 删除附件
        /// </summary>
        /// <param name="id"></param>
        private void DeleteAtachFiles(long id)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                var files = db.Query<DocumentFile>("select * from documents_file where doc_id=@doc_id",
                        new { doc_id = id }
                    ).ToList();

                foreach (var f in files)
                {
                    db.Execute("delete from documents_file where id=@id", new { f.id });

                    if (System.IO.File.Exists(f.file_path))
                    {
                        System.IO.File.Delete(f.file_path);
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

                CheckIfIsBookOwner(userid, db, bookid);

                var book = GetBook(bookid, userid);
                foreach (var d in book.BookDirectory)
                {
                    Delete(d.document_id);
                }

                var sql = @"
BEGIN TRANSACTION;
delete from book_owner where book_id=@book_id;
delete from book_directories where book_id=@book_id;
delete from books where id=@book_id;
COMMIT;
";
                db.Execute(sql, new { book_id = bookid });
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
                if (directory != null)
                {
                    CheckPermissionForUpdateBook(userid, db, directory.book_id);

                    var docId = db.Query<long>("select document_id from book_directories where id=@id", new { id = bookDirId }).FirstOrDefault();
                    db.Execute("delete from book_directories where id=@id;", new { id = bookDirId });

                    if (docId > 0)
                    {
                        Delete(docId);
                    }
                }
            }
        }

        /// <summary>
        /// 删除用户组
        /// </summary>
        /// <param name="groupRowid"></param>
        public void DeleteUserGroup(string creator, long groupRowid)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var isGroupOwner = db.Query<bool>("select 1 from user_group where rowid=@group_rowid and creator=@creator",
                    new { group_rowid = groupRowid, creator = creator }).FirstOrDefault();
                if (isGroupOwner)
                {
                    db.Execute(@"
BEGIN TRANSACTION;
delete from user_group where rowid = @rowid;
delete from user_group_member where group_rowid = @rowid;
delete from documents_share where user_group_rowid = @rowid;
COMMIT;
");
                }
                else
                {
                    throw new Exception("这个用户组不是你创建，你无权操作");
                }
            }
        }

        /// <summary>
        /// 将文档共享的用户组收回共享
        /// </summary>
        /// <param name="docId"></param>
        /// <param name="userGroupRowid"></param>
        public void DocRemoveShare(string creator, long docId, long userGroupRowid)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var isDocOwner = db.Query<bool>("select 1 from documents_owner where id=@doc_id and creator=@creator",
                    new { doc_id = docId, creator = creator }).FirstOrDefault();
                if (isDocOwner)
                {
                    db.Execute("delete from documents_share where doc_id=@doc_id and user_group_rowid=@user_group_rowid",
                        new { doc_id = docId, user_group_rowid = userGroupRowid });
                }
                else
                {
                    throw new Exception("你不是文档的创建者，不能执行此操作");
                }
            }
        }

        /// <summary>
        /// 将文档共享给用户组
        /// </summary>
        /// <param name="creator"></param>
        /// <param name="docId"></param>
        /// <param name="userGroupRowid"></param>
        public void DocShareToUserGroup(string creator, long docId, long userGroupRowid)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var isDocOwner = db.Query<bool>("select 1 from documents_owner where id=@doc_id and creator=@creator",
                    new { doc_id = docId, creator = creator }).FirstOrDefault();
                if (isDocOwner)
                {
                    db.Execute("insert into documents_share(doc_id, user_group_rowid, creator) values(@doc_id, @user_group_rowid, @creator)",
                    new { doc_id = docId, user_group_rowid = userGroupRowid, creator = creator });
                }
                else
                {
                    throw new Exception("你不是文档的创建者，不能执行此操作");
                }
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

                if (!hasPermission)
                {
                    throw new Exception("你无权查看");
                }

                var book = db.Query<Book>("select * from books where id=@id", new { id = bookid }).Single();
                var directories = db.Query<BookDirectory>("select * from book_directories where book_id = @book_id order by parent_id, seq", new { book_id = bookid }).ToList();
                var owner = db.Query<BookOwner>("select * from book_owner where book_id = @id", new { id = bookid }).ToList();
                var result = new BookVm { Book = book, BookDirectory = directories, BookOwner = owner };

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
        /// 取某一个版本的文档
        /// </summary>
        /// <param name="versionId"></param>
        /// <returns></returns>
        public Document GetByVersionId(long versionId)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var document = db.Query<Document>(@"
select a.doc_id as rowid, a.title, a.content, a.category, b.creat_at, a.create_at as update_at, a.creator, b.is_public
from documents_history a
left join documents_owner b on a.doc_id = b.id
where a.rowid = @versionId
",
                new { versionId = versionId }).FirstOrDefault();

                return document;
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
        /// 取文档的历史版本
        /// </summary>
        /// <param name="docId"></param>
        /// <returns></returns>
        public List<DocumentVersion> GetDocVersions(long docId)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var versions = db.Query<DocumentVersion>(@"select rowid, creator, create_at from documents_history where doc_id=@docId order by rowid desc",
                    new { docId = docId }).ToList();
                return versions;
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
        /// 获取 Id
        /// </summary>
        /// <param name="idType"></param>
        /// <returns></returns>
        public long GetId(string idType)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                lock (_lock)
                {
                    var sql = "select ifnull(ivalue, 9999) + 1 as ivalue from id_generator where ikey=@idType;";
                    var id = db.Query<long>(sql, new { idType = idType }).FirstOrDefault();
                    if (id == 0)
                    {
                        id = 10000;
                        db.Execute(@"insert into id_generator(ikey, ivalue) values(@idType, @id)",
                            new { idType = idType, id = id });
                    }
                    else
                    {
                        db.Execute(@"update id_generator set ivalue = @id where ikey = @idType",
                            new { idType = idType, id = id });
                    }

                    return id;
                }
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
    b.creator,
    ifnull(d.id, 0) as ref_book_directory_id,
    ifnull(d.book_id, 0) as ref_book_id
from documents a
inner join documents_owner b on b.id = a.rowid
left outer join book_directories d on d.document_id = b.id
where 1=1
    and b.is_public = 1
    and b.update_at > @updateAt
",
            new { updateAt = DateTime.Now.AddDays(days) });
                return docs.ToList();
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

        private DbConnection OpenDb()
        {
            this.CreateDBIfNotExist();
            return new SQLiteConnection("data source=" + this._dbPath);
        }

        /// <summary>
        /// 重建所有文档的索引
        /// </summary>
        public void ReCreateSearchIndex()
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                #region
                var sql = @"
select distinct b.id as rowid, a.title, a.content, a.category, b.creat_at, b.update_at, b.creator
from documents a
inner join documents_owner b on a.rowid = b.id
";
                #endregion

                var docs = db.Query<Document>(sql);
                foreach (var document in docs)
                {
                    _indexMgr.AddOrUpdateDocIndex(new Doc { Title = document.title, Content = document.content, Category = document.category, Id = document.rowid.ToString(), Operate = Operate.AddOrUpdate });
                }
            }
        }

        /// <summary>
        /// 将用户从组中移除
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="userGroupRowid"></param>
        public void RemoveUserFromGroup(string creator, string userId, long userGroupRowid)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var isGroupOwner = db.Query<bool>("select 1 from user_group where rowid=@group_rowid and creator=@creator",
                   new { group_rowid = userGroupRowid, creator = creator }).FirstOrDefault();
                if (isGroupOwner)
                {
                    db.Execute("delete from user_group_member where group_rowid=@group_rowid and user_id=@user_id",
                        new { group_rowid = userGroupRowid, user_id = userId });
                }
                else
                {
                    throw new Exception("这个用户组不是你创建，你无权操作");
                }
            }
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
        /// 搜索文档
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public List<Document> Search(string queryText, string userId = "", bool isOnlySearchMine = false)
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
        or (b.creator <> @userId and b.is_public=1 and @isOnlySearchMine=0) 
        or (
            select 1 
            from book_directories c 
            inner join book_owner d on d.book_id = c.book_id
            where c.document_id = a.rowid
                and d.user_id = @userId
            )
    )
",
                                     new { list = indexResult.Select(t => t.Id).ToList(), userId = userId, isOnlySearchMine = isOnlySearchMine });
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
        /// 通过文档Category索引文档
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public List<Document> SearchByCategory(string category, string userId = "")
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
                                                        and (b.creator = @userId or b.is_public=1)
                                                        and not exists(select 1 from book_directories d where document_id = a.rowid)",
                                                                                               new { category = category, userId = userId });
                return documents.ToList();
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
                CheckIfIsBookOwner(owner, db, bookid);

                db.Execute("update book_owner set user_id=@transferid where book_id = @book_id and is_owner = 1 and user_id = @owner", new { book_id = bookid, owner = owner, transferid = transferid });
            }
        }

        /// <summary>
        /// 将书籍分享给 id 实现共同修改
        /// </summary>
        /// <param name="bookid"></param>
        /// <param name="owner"></param>
        /// <param name="shareToUserId"></param>
        public void ShareBookTo(long bookid, string owner, string shareToUserId)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                CheckIfIsBookOwner(owner, db, bookid);

                db.Execute("insert or replace into book_owner(book_id, user_id, is_owner) values(@bookid, @shareToUserId, 0)", new { bookid, shareToUserId });
            }
        }

        /// <summary>
        /// 移除书籍共享人
        /// </summary>
        /// <param name="bookid"></param>
        /// <param name="owner"></param>
        /// <param name="shareToUserId"></param>
        public void RemoveBookShare(long bookid, string owner, string shareToUserId)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                CheckIfIsBookOwner(owner, db, bookid);

                db.Execute("delete from book_owner where book_id=@bookid and user_id=@shareToUserId", new { bookid, shareToUserId });
            }
        }


        /// <summary>
        /// 更改文章属主
        /// </summary>
        /// <param name="docId"></param>
        /// <param name="transferid"></param>
        public void TransferDocumentOwner(long docId, string transferid)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                db.Execute("update documents_owner set creator=@transferid where id=@docId", new { docId = docId, transferid = transferid });
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

                if (!CanUpdateDocByUser(userId, id))
                {
                    throw new Exception("你无权操作");
                }

                SaveCategory(id, category, db);

                db.Execute("update documents_owner set update_at=datetime('now', 'localtime'), is_public=@access where id=@id",
                    new { id = id, access = access, userId = userId });

                db.Execute("update documents set title=@title, content=@content, category=@category where rowid=@rowid",
                    new { rowid = id, title = title, content = content, category = category });

                db.Execute("insert into documents_history(creator, doc_id, title, content, category) values(@creator, @docId, @title, @content, @category)",
                    new { creator = userId, docId = id, title = title, content = content, category = category });

                _indexMgr.AddOrUpdateDocIndex(new Doc { Id = id.ToString(), Category = category, Content = content, Title = title, Operate = Operate.AddOrUpdate });
                //ClearNotExistsAtachFile(id);
            }
        }

        /// <summary>
        /// 刷新文章中的附件，如附件在文章中已删除，则将后台的附件也删除
        /// </summary>
        /// <param name="id"></param>
        /// <param name="uploadId"></param>
        public void UpdateAtachFiles(long id, string uploadId)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var content = db.Query<string>("select content from documents where rowid=@doc_id", 
                    new { doc_id = id }).FirstOrDefault();

                if (string.IsNullOrWhiteSpace(content))
                {
                    DeleteAtachFiles(id);
                }
                else
                {
                    var files = db.Query<DocumentFile>(
                        "select * from documents_file where doc_id=@doc_id or upload_id=@uploadId",
                        new { doc_id = id, uploadId = uploadId });

                    foreach (var f in files)
                    {
                        if (f.id > 0 && f.id != id) continue;

                        var fileName = System.IO.Path.GetFileName(f.file_path);
                        if (Regex.IsMatch(content, fileName, RegexOptions.IgnoreCase) == false)
                        {
                            db.Execute("delete from documents_file where id=@id", new { f.id });

                            if (System.IO.File.Exists(f.file_path)) System.IO.File.Delete(f.file_path);
                        }
                        else
                        {
                            db.Execute("update documents_file set doc_id=@doc_id where doc_id=0 and upload_id=@uploadId and file_path=@path",
                                new { doc_id = id, path = f.file_path, uploadId = uploadId });
                        }
                    }
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
        /// 更新书籍目录
        /// </summary>
        /// <param name="bookid"></param>
        /// <param name="directoryid"></param>
        /// <param name="title"></param>
        /// <param name="description"></param>
        /// <param name="userid"></param>
        public void UpdateBookDirectory(long bookid, long directoryid, string title, string description, int seq, string userid, long documentid = 0)
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
       

        public void LockEdit(long docId, string userId, string host)
        {
            var key = GetLockEditKey(docId);
            var lockBy = CacheHelper.Instance.GetCache<string>(key);

            if (lockBy.IsNullOrEmpty() || lockBy.ToLower() == userId.ToLower())
            {                
                CacheHelper.Instance.SetCache(key, userId, TimeSpan.FromMinutes(2));
            }
            else
            {
                throw new Exception($"文档正在被{AdAccount.GetUserNameById(lockBy)}编辑，请稍后再试");
            }
        }

        string GetLockEditKey(long docId)
        {
            return $"GetLockEditKey_{docId}";
        }
    }
}

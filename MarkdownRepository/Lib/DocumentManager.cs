using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.SQLite;
using System.Data.Common;
using Dapper;
using MarkdownRepository.Models;
using System.Text.RegularExpressions;

namespace MarkdownRepository.Lib
{
    public class DocumentManager
    {
        private string _dbPath = null;
        private IndexManager _indexMgr = null;


        public DocumentManager()
        {
            const string SQLITE_PATH = "~/App_Data";
            const string INDEX_PATH = "~/App_Data/Index/";

            this._dbPath = System.IO.Path.Combine(System.Web.HttpContext.Current.Server.MapPath(SQLITE_PATH), "Documents.db3");
            IndexManager.IndexPath = System.Web.HttpContext.Current.Server.MapPath(INDEX_PATH);
            this._indexMgr = IndexManager.IndexMgr;
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
create table if not exists user(id INTEGER primary key, user_id nvarchar(100) not null, user_name nvarchar(100) not null);
                ";
                db.Execute(sql);
            }
        }

        public string GetUserName(string userId)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var userName = db.Query<string>("select user_name from user where user_id=@userId", new { userId = userId }).FirstOrDefault();
                return userName;
            }
        }

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
        /// 保存文档类别
        /// </summary>
        /// <param name="id"></param>
        /// <param name="category"></param>
        /// <param name="db"></param>
        public void SaveCategory(long id, string category, DbConnection db)
        {
            var ca = category.Split(',');

            db.Execute("delete from documents_category where doc_id=@id", new { id = id });
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
                return db.Query<dynamic>(@"select category, count(*) hint 
                                           from documents_category a, documents_owner b 
                                            where a.doc_id = b.id and b.creator=@creator
                                            group by category order by count(*) desc", new { creator = userId }).ToList();
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
                return db.Query<dynamic>(@"select category, count(*) hint from documents_category a, documents_owner b 
                                            where a.doc_id = b.id and b.is_public=1 
                                            group by category order by count(*) desc").ToList();
            }
        }

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
        public void Update(long id, string content, string title, string category, DocumentAccess access)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                SaveCategory(id, category, db);

                db.Execute("update documents_owner set update_at=datetime('now', 'localtime'), is_public=@access where id=@id",
                    new { id = id, access = access });

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

                db.Execute("delete from documents_owner where id=@id;", new { id = id });
                db.Execute("delete from documents where rowid=@rowid;", new { rowid = id });
                db.Execute("delete from documents_category where doc_id=@id", new { id = id });
                db.Execute("delete from documents_follow where doc_id=@id", new { id = id });

                _indexMgr.AddOrUpdateDocIndex(new Doc { Id = id.ToString(), Operate = Operate.Delete });
                //DeleteAtachFile(id);
            }
        }

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
                var document = db.Query<Document>(@"select b.id as rowid, title, content, category, creat_at, update_at, creator, is_public, ifnull(c.count,1) as read_count
                                                    from documents a 
                                                    inner join documents_owner b on a.rowid = b.id
                                                    left outer join documents_read_count c on b.id = c.doc_id 
                                                    where b.id=@id;

                                                    insert or ignore into documents_read_count(count, doc_id) values(1, @id);
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
                                                    WHERE a.rowid = b.id and c.doc_id = b.id and c.category = @category and (b.creator = @userId or (@userId='' and b.is_public=1) )",
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
            var result = new List<Document>();
            var indexResult = _indexMgr.Search(queryText);
            if (indexResult != null && indexResult.Count > 0)
            {
                using (var db = this.OpenDb())
                {
                    CreateTableIfNotExist();

                    var documents = db.Query<Document>(@"select b.id as rowid, title, 
                                                        content, 
                                                        category,
                                                        creat_at, update_at, creator
                                                    from documents a, documents_owner b 
                                                    WHERE a.rowid = b.id and b.id in @list and (b.creator = @userId or (b.creator <> @userId and b.is_public=1))",
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
                                                    order by update_at desc",
                                                                                                new { creator = userId });
                return documents.ToList();
            }
        }

        public List<Document> AllDocument()
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                var documents = db.Query<Document>(@"select id as rowid, title, content, category, creat_at, update_at, creator
                                                    from documents a, documents_owner b 
                                                    where a.rowid = b.id and b.is_public=1
                                                    order by update_at desc");
                return documents.ToList();
            }
        }

        
    }
}

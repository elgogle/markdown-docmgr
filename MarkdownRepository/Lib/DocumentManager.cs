using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.SQLite;
using System.Data.Common;
using Dapper;
using MarkdownRepository.Models;

namespace MarkdownRepository.Lib
{
    public class DocumentManager
    {
        private string _dbPath = null;
        private IndexManager _indexMgr = null;

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
                var sql = @"create VIRTUAL table if not exists documents USING fts3(title, content, category);
                            create table if not exists documents_owner(id int primary key, creator nvarchar(100) not null, creat_at datetime default (datetime('now', 'localtime')), update_at datetime default (datetime('now', 'localtime')));
                            create table if not exists documents_category(id INTEGER primary key, category nvarchar(100) not null, doc_id int not null);
                            create table if not exists documents_file(id INTEGER primary key, file_path nvarchar(512) not null, doc_id int not null);
                                    ";
                db.Execute(sql);
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
        public Document Create(string content, string title, string category, string userId)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                var max = db.Query<int?>("select max(id) id from documents_owner").FirstOrDefault();
                var document = new Document { category = category, content = content, creator = userId, rowid = (max ?? 0) + 1, title = title };

                SaveCategory(document.rowid, category, db);

                db.Execute("insert into documents_owner(id, creator) values(@id, @creator)",
                    new { id = document.rowid, creator = document.creator });

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

            foreach (var c in ca)
            {
                db.Execute("delete from documents_category where doc_id=@id", new { id = id });
                db.Execute(@"insert or replace into documents_category(category, doc_id) values(@category, @id)",
                        new { category = c.Trim(), id = id });
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
                return db.Query<dynamic>("select category, count(*) hint from documents_category group by category order by count(*) desc").ToList();
            }
        }

        /// <summary>
        /// 创建或更新文档
        /// </summary>
        /// <param name="id"></param>
        /// <param name="content"></param>
        /// <param name="title"></param>
        /// <param name="category"></param>
        public void Update(long id, string content, string title, string category)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                SaveCategory(id, category, db);

                db.Execute("update documents_owner set update_at=datetime('now', 'localtime') where id=@id",
                    new { id = id });

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
                var document = db.Query<Document>(@"select id as rowid, title, content, category, creat_at, update_at, creator 
                                                    from documents a, documents_owner b 
                                                    where a.rowid = b.id and b.id=@id",
                                                                                      new { id = id }).FirstOrDefault();
                if (!_indexMgr.Exists(id.ToString()))
                    _indexMgr.AddOrUpdateDocIndex(new Doc { Title = document.title, Content = document.content, Category = document.category, Id = id.ToString(), Operate = Operate.AddOrUpdate });

                return document;
            }
        }

        /// <summary>
        /// 通过文档Category索引文档
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public List<Document> SearchByCategory(string category)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var documents = db.Query<Document>(@"select distinct b.id as rowid, title, 
                                                        content, 
                                                        a.category,
                                                        creat_at, update_at, creator
                                                    from documents a, documents_owner b, documents_category c 
                                                    WHERE a.rowid = b.id and c.doc_id = b.id and c.category = @category",
                                                                                               new { category = category });
                return documents.ToList();
            }
        }

        /// <summary>
        /// 搜索文档
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public List<Document> Search(string queryText)
        {
            var result = _indexMgr.Search(queryText);
            if (result != null && result.Count > 0)
            {
                using (var db = this.OpenDb())
                {
                    CreateTableIfNotExist();

                    var documents = db.Query<Document>(@"select b.id as rowid, title, 
                                                        content, 
                                                        category,
                                                        creat_at, update_at, creator
                                                    from documents a, documents_owner b 
                                                    WHERE a.rowid = b.id and b.id in @list",
                                                                                                   new { list = result.Select(t => t.Id).ToList() });
                    return documents.ToList();
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
                                                    where a.rowid = b.id and b.creator=@creator",
                                                                                                new { creator = userId });
                return documents.ToList();
            }
        }
    }
}
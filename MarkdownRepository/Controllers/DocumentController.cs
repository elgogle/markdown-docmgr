using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.SQLite;

namespace MarkdownRepository.Controllers
{
    using Lib;
    using System.IO;
    using System.Data.Common;
    using Models;
    using Dapper;

    public class DocumentController : Controller
    {
        const string SQLITE_PATH = "~/App_Data";
        DocumentManager docManager = new DocumentManager();

        /// <summary>
        /// 当前用户ID
        /// </summary>
        private string UserId
        {
            get
            {
                return string.IsNullOrWhiteSpace(User.Identity.Name) ?
                    "Anonymous" : User.Identity.Name.GetUserName();
            }
        }

        private string _DbFilePath
        {
            get
            {
                return Server.MapPath(Path.Combine(SQLITE_PATH, "Documents.db3"));
            }
        }

        private void CreateDBIfNotExist()
        {
            if (!System.IO.File.Exists(_DbFilePath))
            {
                SQLiteConnection.CreateFile(_DbFilePath);
            }
        }

        private DbConnection OpenDb()
        {
            this.CreateDBIfNotExist();           
            return new SQLiteConnection("data source=" + this._DbFilePath);
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

        private Document CreateDocument(string content, string title, string category)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                
                var max = db.Query<int?>("select max(id) id from documents_owner").FirstOrDefault();
                var document = new Document { category = category, content = content, creator = UserId, rowid = (max??0) + 1, title = title };

                SaveCategory(document.rowid, category, db);

                db.Execute("insert into documents_owner(id, creator) values(@id, @creator)", 
                    new { id = document.rowid, creator = document.creator });

                db.Execute("insert into documents(rowid, title, content, category) values(@rowid, @title, @content, @category)", 
                    new { rowid = document.rowid, title = title, content = content, category = category });

                docManager.EditDoc(new Doc { Id = document.rowid.ToString(), Category = category, Content = content, Title = title });
                return document;
            }
        }

        private void SaveCategory(long id, string category, DbConnection db)
        {
            var ca = category.Split(',');

            foreach (var c in ca)
            {
                db.Execute("delete from documents_category where doc_id=@id", new { id = id });
                db.Execute(@"insert or replace into documents_category(category, doc_id) values(@category, @id)",
                        new { category = c.Trim(), id = id });
            }
        }

        private List<dynamic> GetCategory()
        {
            using (var db = this.OpenDb())
            {
                return db.Query<dynamic>("select category, count(*) hint from documents_category group by category order by count(*) desc").ToList();
            }
        }

        private void EditDocument(long id, string content, string title, string category)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                SaveCategory(id, category, db);

                db.Execute("update documents_owner set update_at=datetime('now', 'localtime') where id=@id", 
                    new { id = id });  
              
                db.Execute("update documents set title=@title, content=@content, category=@category where rowid=@rowid", 
                    new { rowid = id, title = title, content = content, category = category });

                docManager.EditDoc(new Doc { Id = id.ToString(), Category = category, Content = content, Title = title });
                //ClearNotExistsAtachFile(id);
            }
        }

        private void DeleteDocument(long id)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                db.Execute("delete from documents_owner where id=@id;", new { id = id });
                db.Execute("delete from documents where rowid=@rowid;", new { rowid = id });
                db.Execute("delete from documents_category where doc_id=@id", new { id = id });

                docManager.DeleteDoc(id.ToString());
                //DeleteAtachFile(id);
            }
        }

        private void DeleteAtachFile(long id)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var filePath = db.Query<string>("select file_path from documents_file where doc_id=@id", 
                    new { id = id, session_id = this.Session.SessionID });
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
                            if(System.IO.File.Exists(f)) System.IO.File.Delete(f);
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

        private Document GetDocument(long id)
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();
                var document = db.Query<Document>(@"select id as rowid, title, content, category, creat_at, update_at, creator 
                                                    from documents a, documents_owner b 
                                                    where a.rowid = b.id and b.id=@id", 
                                                                                      new { id = id }).FirstOrDefault();
                if(!docManager.ExistsDoc(id.ToString()))
                    docManager.EditDoc(new Doc { Title = document.title, Content = document.content, Category = document.category, Id = id.ToString() });

                return document;
            }
        }

        private List<Document> SearchDocumentByCategory(string category)
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

        private List<Document> SearchDocument(string queryText)
        {            
            var result = docManager.Search(queryText);
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
                                                                                                   new { list = result.Select(t=>t.Id).ToList() });
                    return documents.ToList();
                }
            }

            return null;
        }

        private List<Document> MyDocument()
        {
            using (var db = this.OpenDb())
            {
                CreateTableIfNotExist();

                var documents = db.Query<Document>(@"select id as rowid, title, content, category, creat_at, update_at 
                                                    from documents a, documents_owner b 
                                                    where a.rowid = b.id and b.creator=@creator", 
                                                                                                new { creator = UserId });
                return documents.ToList();
            }
        }

        /// <summary>
        /// 首页，我的文档
        /// </summary>
        /// <param name="searchText"></param>
        /// <returns></returns>
        public ActionResult Index(string searchText, int? page)
        {
            var result = MyDocument();
            var category = GetCategory();
            ViewBag.Category = category;
            return View(result);
        }

        /// <summary>
        /// 搜索
        /// </summary>
        /// <param name="searchText"></param>
        /// <returns></returns>
        public ActionResult Search(string searchText)
        {
            ViewBag.CurrentFilter = searchText;

            if (!String.IsNullOrWhiteSpace(searchText))
            {
                var result = SearchDocument(searchText);
                if (result != null && result.Count > 0)
                {
                    foreach (var r in result)
                    {
                        r.title = SplitContent.HightLight(searchText, r.title);
                        r.content = SplitContent.HightLight(searchText, r.content);
                        r.category = SplitContent.HightLight(searchText, r.category);
                    }

                    return View(result);
                }
            }

            return View();
        }

        /// <summary>
        /// 根据category搜索
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public ActionResult SearchByCategory(string category)
        {
            ViewBag.CurrentFilter = category;

            if (!String.IsNullOrWhiteSpace(category))
            {
                var result = SearchDocumentByCategory(category);
                foreach (var r in result)
                {
                    r.title = SplitContent.HightLight(category, r.title);
                    r.content = SplitContent.HightLight(category, r.content);
                    r.category = SplitContent.HightLight(category, r.category);
                }

                return View("Search", result);
            }

            return View("Search");
        }

        /// <summary>
        /// 查看
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult Show(long id)
        {
            var doc = GetDocument(id);
            if (doc == null)
                return HttpNotFound();

            return View(doc);
        }

        /// <summary>
        /// 创建或修改
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult Create(long id = 0)
        {
            ViewBag.Action = "Create";
            if (id > 0)
            {
                var doc = GetDocument(id);
                if (doc == null)
                    return HttpNotFound();

                // TODO:无权访问
                if (!doc.creator.Equals(this.UserId, StringComparison.InvariantCultureIgnoreCase))
                {
                    TempData["RedirectReason"] = "Unauthorized";
                    return RedirectToAction("Show", new { id = id });
                }

                return View(doc);
            }

            return View();
        }

        /// <summary>
        /// 提交文档
        /// </summary>
        /// <param name="content"></param>
        /// <param name="title"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult Create(string category, string content, string title, long id)
        {
            Document document = null;
            // create
            if (id==0)
                document = CreateDocument(content, title, category);
            // edit
            else
            {
                document = GetDocument(id);
                if (document == null)
                {
                    return HttpNotFound();
                }

                if (!document.creator.Equals(this.UserId, StringComparison.InvariantCultureIgnoreCase))
                {                    
                    return HttpNotFound();
                }

                document.category = category;
                document.content = content;
                document.title = title;

                EditDocument(id, content, title, category);
            }

            return Json(document);
        }

        /// <summary>
        /// 删除文档
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult Delete(long id)
        {
            var existDoc = GetDocument(id);

            // TODO:无权访问
            if (existDoc != null && !existDoc.creator.Equals(this.UserId))
                return Json(new { success = false, message = "You don't have permission to delete" });

            DeleteDocument(id);
            return Json(new { success = true });
        }

        /// <summary>
        /// 上传图片
        /// </summary>
        /// <returns></returns>
        public ActionResult UploadImage()
        {
            var hfc = this.HttpContext.Request.Files;
            if (hfc.Count > 0)
            {
                var file = hfc[0];
                string reletiveSavePath = "/doc/images";
                string savePath = Server.MapPath(reletiveSavePath);
                if (!Directory.Exists(savePath))
                    Directory.CreateDirectory(savePath);

                string pic = System.IO.Path.GetExtension(file.FileName);
                var fileName = Guid.NewGuid().ToString() + pic;
                string path = System.IO.Path.Combine(savePath, fileName);

                // file is uploaded
                file.SaveAs(path);

                //AddAtachFile(Convert.ToInt64(Request["doc_id"]), path);

                var result = new { success = 1, message = "", url = reletiveSavePath + "/" + fileName };
                return Json(result);
            }

            return Json(new { success = 0, message = "", url = "" });
        }        
    }
}

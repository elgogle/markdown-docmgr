#region Imports (7)

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Web;
using System.Web.Mvc;

#endregion Imports (7)

namespace MarkdownRepository.Controllers
{
    using Dapper;
    using Lib;
    using Models;
    using Newtonsoft.Json;
    using PagedList;
    using System.Data.Common;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Web.Hosting;

    [Authorize]
    public class DocumentController : Controller
    {
        #region Members of DocumentController (3)
        const string SQLITE_PATH = "~/App_Data";
        const string INDEX_PATH = "~/App_Data/Index/";
        DocumentManager docMgr = null;

        #endregion Members of DocumentController (3)

        #region Properties of DocumentController (1)

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

        #endregion Properties of DocumentController (1)

        #region Constructors of DocumentController (1)

        public DocumentController()
        {
            var dbFilePath = Path.Combine(System.Web.HttpContext.Current.Server.MapPath(SQLITE_PATH), "Documents.db3");
            var indexFilePath = System.Web.HttpContext.Current.Server.MapPath(INDEX_PATH);

            docMgr = new DocumentManager(dbFilePath, indexFilePath);
        }

        #endregion Constructors of DocumentController (1)

        #region Methods of DocumentController (39)

        /// <summary>
        /// 所有书籍页面
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        public ActionResult AllBooks(int? page)
        {
            int pageSize = 50;
            int pageNumber = page ?? 1;
            var books = docMgr.GetBooks();
            var result = books.ToPagedList(pageNumber, pageSize);
            var owners = docMgr.GetBookOwner();
            foreach (var item in result)
            {
                var owner = owners.FirstOrDefault(t => t.book_id == item.id);
                if (owner != null)
                    item.creator = owner.user_id;
            }

            return View(result);
        }

        /// <summary>
        /// 所有文档
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        [AllowAnonymous]
        public ActionResult AllDocument(int? page, string orderBy)
        {
            int pageSize = 50;
            int pageNumber = page ?? 1;
            var result = docMgr.AllDocument(orderBy);
            var category = docMgr.GetCategory();
            //var creator = docMgr.GetCreator();
            var myFollowedDocs = docMgr.GetFollowDocuments(User.Identity.IsAuthenticated ? UserId : "");

            ViewBag.OrderBy = orderBy;
            ViewBag.Category = category;
            //ViewBag.Creator = creator;
            ViewBag.MyFollowedDocs = myFollowedDocs;
            ViewBag.Action = "ShowAll";
            ViewBag.Title = "所有文章";
            ViewBag.TitleWordCloud = JsonConvert.SerializeObject(
                result.Select(t => t.title).GetWordFreq().Select(t =>
                new
                {
                    text = t.Item1,
                    weight = t.Item2
                }));
            return View(result.ToPagedList(pageNumber, pageSize));
        }

        /// <summary>
        /// 书籍目录顺序更改，包括层级变化
        /// </summary>
        /// <param name="bookId"></param>
        /// <param name="dirId"></param>
        /// <param name="seq"></param>
        /// <param name="oldSeq"></param>
        /// <param name="parentId"></param>
        /// <param name="oldParentId"></param>
        /// <returns></returns>
        public ActionResult BookDirectoryMove(long bookId, long dirId, int seq, int oldSeq, long parentId, long oldParentId)
        {
            try
            {
                docMgr.BookDirectoryMove(this.UserId, bookId, dirId, seq, oldSeq, parentId, oldParentId);
                return Success(seq);
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        /// <summary>
        /// 取消关注该文章
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult CancelFollow(long id)
        {
            if (User.Identity.IsAuthenticated)
            {
                docMgr.CancelFollow(UserId, id);
                return Json(new { success = true });
            }
            return Json(new { success = false });
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
                var doc = docMgr.Get(id);
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

            ViewBag.Categories = docMgr.GetCategory().Select(t => t.category);

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
        public ActionResult Create(string category, string content, string title, long id, DocumentAccess access)
        {
            Document document = null;
            // create
            if (id == 0)
                document = docMgr.Create(content, title, category, UserId, access);
            // edit
            else
            {
                document = docMgr.Get(id);
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

                docMgr.Update(id, content, title, category, access, UserId);
                if (access == DocumentAccess.PRIVATE)
                {
                    docMgr.CancelFollow(UserId, id);
                }
            }

            return Json(document);
        }

        /// <summary>
        /// 显示创建书籍页面
        /// </summary>
        /// <returns></returns>
        public ActionResult CreateBook()
        {
            ViewBag.Action = "CreateBook";
            return View();
        }

        /// <summary>
        /// 创建书籍
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="category"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult CreateBook(string name, string description, string category)
        {
            ViewBag.Action = "CreateBook";
            try
            {
                DocumentAccess access = Request["is_public"] == "on" ? DocumentAccess.PUBLIC : DocumentAccess.PRIVATE;
                var id = docMgr.CreateBook(this.UserId, name, description, category, "", access);

                return RedirectToAction("EditBook", new { id = id });
            }
            catch (Exception ex)
            {
                TempData.Add("Error", ex.Message);
            }

            return View();
        }

        /// <summary>
        /// 创建书籍目录
        /// </summary>
        /// <param name="bookId"></param>
        /// <param name="title"></param>
        /// <param name="description"></param>
        /// <param name="parentId"></param>
        /// <param name="documentId"></param>
        /// <returns></returns>
        public ActionResult CreateBookDirectory(long bookId, string title, string description, long parentId, long documentId = 0)
        {
            try
            {
                var dirId = docMgr.CreateBookDirectory(bookId, title, description, parentId, documentId, 0, this.UserId);
                return Success(dirId);
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        /// <summary>
        /// 创建书籍文章
        /// </summary>
        /// <param name="directoryid"></param>
        /// <param name="content"></param>
        /// <param name="title"></param>
        /// <returns></returns>
        public ActionResult CreateOrUpdateBookArticle(long directoryid, string content, string title)
        {
            try
            {
                if (directoryid <= 0)
                {
                    throw new Exception("请选择目录后，再写文章和保存");
                }

                docMgr.CreateOrUpdateBookArticle(directoryid, content, title, this.UserId);
                return Success();
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        private void CreateProgramSpec(long bookid)
        {
            // 模板式创建 (Program spec)
            // 目录
            var dir1 = docMgr.CreateBookDirectory(bookid, "简介", "", 0, 0, 0, this.UserId);
            var dir2 = docMgr.CreateBookDirectory(bookid, "一、用户需求及参考资料", "", 0, 0, 0, this.UserId);
            var dir2_1 = docMgr.CreateBookDirectory(bookid, "1. 需求文件", "", dir2, 0, 0, this.UserId);
            var dir2_2 = docMgr.CreateBookDirectory(bookid, "2. 需求确认事项", "", dir2, 0, 0, this.UserId);
            var dir2_3 = docMgr.CreateBookDirectory(bookid, "3. R&D 事项", "", dir2, 0, 0, this.UserId);
            var dir2_4 = docMgr.CreateBookDirectory(bookid, "4. 部门开发标准及规范参考", "", dir2, 0, 0, this.UserId);

            var dir3 = docMgr.CreateBookDirectory(bookid, "二、分析及设计", "", 0, 0, 0, this.UserId);
            var dir3_1 = docMgr.CreateBookDirectory(bookid, "1. 系统框架设计及说明", "", dir3, 0, 0, this.UserId);
            var dir3_2 = docMgr.CreateBookDirectory(bookid, "2. 项目功能操作流程", "", dir3, 0, 0, this.UserId);
            var dir3_3 = docMgr.CreateBookDirectory(bookid, "3. 项目功能列表说明", "", dir3, 0, 0, this.UserId);
            var dir3_4 = docMgr.CreateBookDirectory(bookid, "4. 项目开发时间预计及计划", "", dir3, 0, 0, this.UserId);
            var dir3_5 = docMgr.CreateBookDirectory(bookid, "5. 数据库表设计", "", dir3, 0, 0, this.UserId);
            var dir3_6 = docMgr.CreateBookDirectory(bookid, "6. 数据库及项目位置", "", dir3, 0, 0, this.UserId);
            var dir3_7 = docMgr.CreateBookDirectory(bookid, "7. Web Service 的说明", "", dir3, 0, 0, this.UserId);

            var dir4 = docMgr.CreateBookDirectory(bookid, "三、测试与发布", "", 0, 0, 0, this.UserId);
            var dir4_1 = docMgr.CreateBookDirectory(bookid, "1. 测试计划", "", dir4, 0, 0, this.UserId);
            var dir4_2 = docMgr.CreateBookDirectory(bookid, "2. 安装及发布注意事项说明", "", dir4, 0, 0, this.UserId);
            var dir4_3 = docMgr.CreateBookDirectory(bookid, "3. 参数列表", "", dir4, 0, 0, this.UserId);

            var dir5 = docMgr.CreateBookDirectory(bookid, "四、使用及维护", "", 0, 0, 0, this.UserId);
            var dir5_1 = docMgr.CreateBookDirectory(bookid, "1. 用户使用指南", "", dir5, 0, 0, this.UserId);
            var dir5_2 = docMgr.CreateBookDirectory(bookid, "2. 程序文件说明", "", dir5, 0, 0, this.UserId);
            var dir5_3 = docMgr.CreateBookDirectory(bookid, "3. 维护及使用相关问题列表", "", dir5, 0, 0, this.UserId);
            var dir5_4 = docMgr.CreateBookDirectory(bookid, "4. 改进历史记录", "", dir5, 0, 0, this.UserId);
        }

        /// <summary>
        /// 删除文档
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult Delete(long id)
        {
            var existDoc = docMgr.Get(id);

            // TODO:无权访问
            if (existDoc != null && !existDoc.creator.Equals(this.UserId))
                return Json(new { success = false, message = "You don't have permission to delete" });

            docMgr.Delete(id);
            return Json(new { success = true });
        }

        /// <summary>
        /// 删除一本书
        /// </summary>
        /// <param name="bookid"></param>
        /// <returns></returns>
        public ActionResult DeleteBook(long bookid)
        {
            try
            {
                docMgr.DeleteBook(bookid, this.UserId);
                return RedirectToAction("MyBooks");
            }
            catch (Exception ex)
            {
                TempData.Add("Error", ex.Message);
            }

            return View();
        }

        /// <summary>
        /// 删除一本书中某一个目录
        /// </summary>
        /// <param name="bookDirecotryId"></param>
        /// <returns></returns>
        public ActionResult DeleteBookDirectory(long bookDirecotryId)
        {
            try
            {
                docMgr.DeleteBookDirectory(bookDirecotryId, this.UserId);
                return Success();
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        [AllowAnonymous]
        public ActionResult Download(string filename)
        {
            string path = System.IO.Path.Combine(this.Server.MapPath("~/doc/files"), System.Web.HttpUtility.UrlDecode(filename));
            if (System.IO.File.Exists(path))
            {
                return File(path, System.Net.Mime.MediaTypeNames.Application.Octet, Path.GetFileName(path));
            }

            return Content("文件不存在");
        }

        /// <summary>
        /// 显示编辑书籍页面
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult EditBook(long id, long dirId = 0)
        {
            var book = docMgr.GetBook(id, this.UserId);
            ViewBag.OpenDirId = dirId;

            return View(book);
        }

        public ActionResult Fail(string errorMessage)
        {
            return Json(new
            {
                isSuccess = false,
                message = errorMessage
            }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// 关注该文章
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult FollowDocument(long id)
        {
            if (User.Identity.IsAuthenticated)
            {
                docMgr.FollowDocument(UserId, id);
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        /// <summary>
        /// jsTree 需要的 Json 数据
        /// </summary>
        /// <param name="bookid"></param>
        /// <returns></returns>
        [AllowAnonymous]
        public ActionResult GetBookDirectory(long bookid, long dirId = 0)
        {
            try
            {
                var book = docMgr.GetBook(bookid, this.UserId);

                var directories = book.BookDirectory.Select(t => new
                {
                    id = t.id.ToString(),
                    parent = t.parent_id == 0 ? "#" : t.parent_id.ToString(),
                    text = t.title,
                    state = new
                    {
                        selected = t.id == dirId ? true : false
                    }
                }).ToList();

                return Success(directories);
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        /// <summary>
        /// 返回一个目录的顺序结构，用于前台导航
        /// </summary>
        /// <param name="bookid"></param>
        /// <returns></returns>
        private List<long> GetBookDirectoryNavigator(BookVm book)
        {
            var navigator = new List<long>();

            var bookDir = book.BookDirectory
                .OrderBy(t => t.parent_id)
                .ThenBy(t => t.seq)
                .ThenBy(t => t.id)
                .ToList();

            var first = bookDir.FirstOrDefault();
            if (first != null)
            {
                navigator.Add(first.id);
                WalkDirectory(bookDir, navigator, first);
            }

            return navigator;
        }

        /// <summary>
        /// 通过目录查找文章
        /// </summary>
        /// <param name="directoryid"></param>
        /// <returns></returns>
        [AllowAnonymous]
        public ActionResult GetDocument(long directoryid)
        {
            try
            {
                var doc = docMgr.GetDocumentByDirectory(directoryid);
                return Success(doc);
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        /// <summary>
        /// 首页，我的文档
        /// </summary>
        /// <param name="searchText"></param>
        /// <returns></returns>
        public ActionResult Index(string searchText, int? page)
        {
            int pageSize = 50;
            int pageNumber = page ?? 1;
            var result = docMgr.MyDocument(UserId);
            var category = docMgr.GetMyCategory(UserId);
            ViewBag.Category = category;
            ViewBag.Action = "MyDocs";
            ViewBag.Title = "我的文章";
            ViewBag.TitleWordCloud = JsonConvert.SerializeObject(
                result.Select(t => t.title).GetWordFreq().Select(t =>
                new
                {
                    text = t.Item1,
                    weight = t.Item2
                }));
            return View(result.ToPagedList(pageNumber, pageSize));
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult LastestDocuments()
        {
            var markdownTransfer = new MarkdownSharp.Markdown();
            var latestDocs = docMgr.GetLatestDocuments();
            var items = latestDocs.Select(t =>
            {
                var urlBuilder =
                    new System.UriBuilder(Request.Url.AbsoluteUri)
                    {
                        Path = Url.Action("Show", new { id = t.rowid }),
                        Query = null,
                    };

                Uri uri = urlBuilder.Uri;

                return new SyndicationItem(t.title, markdownTransfer.Transform(t.content), uri);
            }).ToList();
            SyndicationFeed feed = new SyndicationFeed("Markdown Documents", "CSC Markdown Documents RSS Feed", Request.Url, items);
            return new RssResult(feed);
        }

        public ActionResult MyBooks()
        {
            var books = docMgr.GetMyBooks(this.UserId);
            var transferUsers = docMgr.GetAllUserId()
                .Where(t => t.user_id.GetUserName() != this.UserId.GetUserName())
                .Select(t =>
                    new
                    {
                        value = t.user_id.Contains("\\") ? t.user_id.Split('\\')[1] : t.user_id,
                        text = t.user_name
                    }).Distinct();

            ViewBag.TransferUsers = new SelectList(transferUsers, "value", "text");
            return View(books);
        }

        /// <summary>
        /// 我关注的文章
        /// </summary>
        /// <returns></returns>
        public ActionResult MyFollowDocuments()
        {
            var result = docMgr.GetFollowDocuments(UserId);
            var category = docMgr.GetFollowCategory(UserId);
            ViewBag.Category = category;
            ViewBag.Action = "MyFollow";
            ViewBag.Title = "我关注的文章";
            return View(result);
        }

        /// <summary>
        /// 重建所有文档的索引
        /// </summary>
        /// <returns></returns>
        public ActionResult ReCreateIndex()
        {
            docMgr.ReCreateSearchIndex();
            return Content("OK");
        }

        /// <summary>
        /// 搜索
        /// </summary>
        /// <param name="searchText"></param>
        /// <returns></returns>
        [AllowAnonymous]
        public ActionResult Search(string searchText, int? page)
        {
            int pageSize = 50;
            int pageNumber = page ?? 1;
            ViewBag.CurrentFilter = searchText;

            if (!String.IsNullOrWhiteSpace(searchText))
            {
                var result = docMgr.Search(searchText, UserId);
                if (result != null && result.Count > 0)
                {
                    foreach (var r in result)
                    {
                        r.title = SplitContent.HightLight(searchText, r.title);
                        r.content = SplitContent.HightLight(searchText, r.content.StripHTML());
                        r.category = SplitContent.HightLight(searchText, r.category);
                    }

                    return View(result.ToPagedList(pageNumber, pageSize));
                }
            }

            return View();
        }

        /// <summary>
        /// 搜索书籍
        /// </summary>
        /// <param name="searchText"></param>
        /// <returns></returns>
        [AllowAnonymous]
        public ActionResult SearchBook(string searchText)
        {
            ViewBag.CurrentFilter = searchText;

            if (!String.IsNullOrWhiteSpace(searchText))
            {
                var result = docMgr.SearchBook(searchText, UserId);
                if (result != null && result.Count > 0)
                {
                    foreach (var r in result)
                    {
                        r.name = SplitContent.HightLight(searchText, r.name);

                        r.description = SplitContent.HightLight(searchText, r.description.StripHTML());

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
        [AllowAnonymous]
        public ActionResult SearchByCategory(string category, bool byOwner, int? page)
        {
            ViewBag.CurrentFilter = category;

            if (!String.IsNullOrWhiteSpace(category))
            {
                int pageSize = 50;
                int pageNumber = page ?? 1;
                var result = docMgr.SearchByCategory(category, byOwner ? UserId : "");
                foreach (var r in result)
                {
                    r.title = r.title;
                    r.content = r.content.StripHTML().GetShortDesc();
                    r.category = SplitContent.HightLight(category, r.category);
                }

                return View("Search", result.ToPagedList(pageNumber, pageSize));
            }

            return View("Search");
        }

        /// <summary>
        /// 设置书籍是否公开
        /// </summary>
        /// <param name="bookid"></param>
        /// <param name="access"></param>
        /// <returns></returns>
        public ActionResult SetBookState(long bookid, DocumentAccess access)
        {
            try
            {
                docMgr.SetBookState(bookid, access);
                return Success();
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        /// <summary>
        /// 查看
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [AllowAnonymous]
        public ActionResult Show(long id)
        {
            var doc = docMgr.Get(id);
            if (doc == null)
                return HttpNotFound();

            if (doc.is_public == DocumentAccess.PRIVATE && doc.creator != UserId)
            {
                return View("NoPermission");
            }

            ViewBag.IsFollowed = false;
            if (User.Identity.IsAuthenticated)
            {
                ViewBag.IsFollowed = docMgr.IsFollowed(UserId, id);
            }

            return View(doc);
        }

        /// <summary>
        /// 显示书籍详细
        /// </summary>
        /// <param name="bookid"></param>
        /// <param name="docId"></param>
        /// <returns></returns>
        [AllowAnonymous]
        public ActionResult ShowBook(long bookid, long docId = 0, long dirId = 0)
        {
            ViewBag.Action = "ShowBook";

            var book = docMgr.GetBook(bookid, this.UserId);
            var dirNav = GetBookDirectoryNavigator(book);
            var currentDirectoryId = dirNav[0];

            if (dirId != 0)
            {
                currentDirectoryId = dirId;
            }
            else if (docId != 0)
            {
                var jumpToDoc = book.BookDirectory.FirstOrDefault(t => t.document_id == docId);
                if (jumpToDoc != null)
                {
                    currentDirectoryId = jumpToDoc.id;
                }
            }

            ViewBag.CurrentDiretoryId = currentDirectoryId;
            var currentIndex = dirNav.IndexOf(currentDirectoryId);
            ViewBag.PreDirectoryId = currentIndex > 0 ? dirNav[currentIndex - 1] : -1;
            ViewBag.NextDirectoryId = currentIndex < (dirNav.Count - 1) ? dirNav[currentIndex + 1] : -1;
            ViewBag.Document = docMgr.GetDocumentByDirectory(currentDirectoryId);

            var directories = book.BookDirectory.Select(t => new
            {
                id = t.id.ToString(),
                parent = t.parent_id == 0 ? "#" : t.parent_id.ToString(),
                text = t.title,
                state = new
                {
                    selected = t.id == currentDirectoryId ? true : false
                }
            }).ToList();
            ViewBag.DirectoryJson = JsonConvert.SerializeObject(directories);

            return View(book);
        }

        /// <summary>
        /// slide presentatin
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [AllowAnonymous]
        public ActionResult Slide(long id)
        {
            var doc = docMgr.Get(id);
            if (doc == null)
                return HttpNotFound();

            if (doc.is_public == DocumentAccess.PRIVATE && doc.creator != UserId)
            {
                return View("NoPermission");
            }

            return View(doc);
        }

        public ActionResult Success(object data = null)
        {
            return Json(new
            {
                isSuccess = true,
                message = "",
                data = data
            }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// 转让书籍所有权
        /// </summary>
        /// <param name="bookid"></param>
        /// <param name="transferid"></param>
        /// <returns></returns>
        public ActionResult TransferBookOwner(long bookid, string transferid)
        {
            try
            {
                docMgr.TransferBookOwner(bookid, this.UserId, transferid);
                return Success();
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        /// <summary>
        /// 更新书籍名称等信息
        /// </summary>
        /// <param name="bookid"></param>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="category"></param>
        /// <param name="access"></param>
        /// <returns></returns>
        public ActionResult UpdateBook(long bookid, string name, string description, string category, DocumentAccess access)
        {
            try
            {
                docMgr.UpdateBook(this.UserId, bookid, name, description, category, "", access);
                return Success();
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        /// <summary>
        /// 更新书籍目录
        /// </summary>
        /// <param name="bookid"></param>
        /// <param name="directoryid"></param>
        /// <param name="title"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        public ActionResult UpdateBookDirectory(long bookid, long directoryid, string title, string description)
        {
            try
            {
                docMgr.UpdateBookDirectory(bookid, directoryid, title, description, 0, this.UserId);
                return Success();
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
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
                string savePath = HostingEnvironment.ApplicationPhysicalPath + reletiveSavePath;

                if (!Directory.Exists(savePath))
                    Directory.CreateDirectory(savePath);

                string pic = System.IO.Path.GetExtension(file.FileName);
                var fileName = Guid.NewGuid().ToString() + pic;
                var path = System.IO.Path.Combine(savePath, fileName);
                var relativePath = HostingEnvironment.ApplicationVirtualPath + path.Replace(Request.ServerVariables["APPL_PHYSICAL_PATH"], String.Empty);

                // file is uploaded
                file.SaveAs(path);

                //AddAtachFile(Convert.ToInt64(Request["doc_id"]), path);

                var result = new { success = 1, message = "", url = relativePath };
                return Json(result);
            }

            return Json(new { success = 0, message = "", url = "" });
        }

        /// <summary>
        /// 上传图片
        /// </summary>
        /// <param name="base64Content"></param>
        /// <returns></returns>
        public ActionResult UploadImageByBase64(string base64Content)
        {
            try
            {
                base64Content = base64Content.Substring(22); // Replace("data:image/png;base64,", "");
                byte[] fileContent = Convert.FromBase64String(base64Content);
                string reletiveSavePath = "/doc/images/";
                string savePath = HostingEnvironment.ApplicationPhysicalPath + reletiveSavePath;

                if (!Directory.Exists(savePath))
                    Directory.CreateDirectory(savePath);

                var fileName = Guid.NewGuid().ToString() + ".png";
                var path = System.IO.Path.Combine(savePath, fileName);
                var relativePath = HostingEnvironment.ApplicationVirtualPath + path.Replace(Request.ServerVariables["APPL_PHYSICAL_PATH"], String.Empty);

                System.IO.File.WriteAllBytes(path, fileContent);

                return Json(new { success = 1, message = "", url = relativePath });
            }
            catch (Exception ex)
            {
                return Json(new { success = 0, message = ex.Message, url = "" });
            }
        }

        /// <summary>
        /// 遍历整个目录
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="result"></param>
        /// <param name="current"></param>
        /// <param name="parent"></param>
        private void WalkDirectory(IList<BookDirectory> directory, IList<long> result, BookDirectory current)
        {
            if (directory.Any(t => t.parent_id == current.id))
            {
                // 有子目录
                var firstSub = directory.Where(t => t.parent_id == current.id)
                    .OrderBy(t=> t.seq)
                    .ThenBy(t => t.id)
                    .First();
                result.Add(firstSub.id);
                WalkDirectory(directory, result, firstSub);
            }

            var nextSibling = directory.Where(t => t.seq >= current.seq && t.parent_id == current.parent_id && !result.Any(i=> i == t.id))                
                .OrderBy(t => t.seq)
                .ThenBy(t => t.id)
                .FirstOrDefault();
            if (nextSibling != null)
            {
                result.Add(nextSibling.id);
                WalkDirectory(directory, result, nextSibling);
            }
        }

        #endregion Methods of DocumentController (39)
    }
}

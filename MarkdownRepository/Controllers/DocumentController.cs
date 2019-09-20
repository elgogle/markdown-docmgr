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
    using System.Text.RegularExpressions;
    using System.Web.Hosting;
    using Newtonsoft.Json;

    [Authorize]
    public class DocumentController : Controller
    {
        const string SQLITE_PATH = "~/App_Data";
        const string INDEX_PATH = "~/App_Data/Index/";
        DocumentManager docMgr = null;

        public DocumentController()
        {
            var dbFilePath = Path.Combine(System.Web.HttpContext.Current.Server.MapPath(SQLITE_PATH), "Documents.db3");
            var indexFilePath = System.Web.HttpContext.Current.Server.MapPath(INDEX_PATH);

            docMgr = new DocumentManager(dbFilePath, indexFilePath);
        }

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

        /// <summary>
        /// 首页，我的文档
        /// </summary>
        /// <param name="searchText"></param>
        /// <returns></returns>        
        public ActionResult Index(string searchText, int? page)
        {
            var result = docMgr.MyDocument(UserId);
            var category = docMgr.GetMyCategory(UserId);
            ViewBag.Category = category;
            ViewBag.Action = "MyDocs";
            ViewBag.Title = "我的文章";
            ViewBag.TitleWordCloud = JsonConvert.SerializeObject(
                result.Select(t => t.title).GetWordFreq().Select(t =>
                new {
                    text = t.Item1,
                    weight = t.Item2
                }));
            return View(result);
        }

        /// <summary>
        /// 所有文档
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        [AllowAnonymous]
        public ActionResult AllDocument(int?page)
        {            
            var result = docMgr.AllDocument();
            var category = docMgr.GetCategory();
            //var creator = docMgr.GetCreator();
            var myFollowedDocs = docMgr.GetFollowDocuments(User.Identity.IsAuthenticated?UserId:"");
            
            ViewBag.Category = category;
            //ViewBag.Creator = creator;
            ViewBag.MyFollowedDocs = myFollowedDocs;
            ViewBag.Action = "ShowAll";
            ViewBag.Title = "所有文章";
            ViewBag.TitleWordCloud = JsonConvert.SerializeObject(
                result.Select(t => t.title).GetWordFreq().Select(t =>
                new {
                    text = t.Item1,
                    weight = t.Item2
                }));
            return View(result);
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
        /// 搜索
        /// </summary>
        /// <param name="searchText"></param>
        /// <returns></returns>
        [AllowAnonymous]
        public ActionResult Search(string searchText)
        {
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
        public ActionResult SearchByCategory(string category, bool byOwner)
        {
            ViewBag.CurrentFilter = category;

            if (!String.IsNullOrWhiteSpace(category))
            {
                var result = docMgr.SearchByCategory(category, byOwner?UserId:"");
                foreach (var r in result)
                {
                    r.title = r.title;
                    r.content = r.content.StripHTML().GetShortDesc();
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
        [AllowAnonymous]
        public ActionResult Show(long id)
        {
            var doc = docMgr.Get(id);
            if (doc == null)
                return HttpNotFound();

            ViewBag.IsFollowed = false;
            if (User.Identity.IsAuthenticated)
            {
                ViewBag.IsFollowed = docMgr.IsFollowed(UserId, id);
            }

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
            if (id==0)
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

                docMgr.Update(id, content, title, category, access);
                if(access == DocumentAccess.PRIVATE)
                {
                    docMgr.CancelFollow(UserId, id);
                }
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
            var existDoc = docMgr.Get(id);

            // TODO:无权访问
            if (existDoc != null && !existDoc.creator.Equals(this.UserId))
                return Json(new { success = false, message = "You don't have permission to delete" });

            docMgr.Delete(id);
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
        /// 所有书籍页面
        /// </summary>
        /// <returns></returns>
        public ActionResult AllBooks()
        {
            var books = docMgr.GetBooks();
            return View(books);
        }

        /// <summary>
        /// 显示创建书籍页面
        /// </summary>
        /// <returns></returns>
        public ActionResult CreateBook()
        {
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
            try
            {
                var id = docMgr.CreateOrUpdateBook(this.UserId, name, description, category, "");
                return RedirectToAction("EditBook", new { id = id });
            }
            catch(Exception ex)
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
        public ActionResult CreateBookDirectory(long bookId, string title, string description, long parentId, long documentId)
        {
            docMgr.CreateOrUpdateBookDirectory(bookId, title, description, parentId, documentId);
            return Content("ok");
        }

        /// <summary>
        /// 创建书籍文章
        /// </summary>
        /// <param name="directoryid"></param>
        /// <param name="content"></param>
        /// <param name="title"></param>
        /// <returns></returns>
        public ActionResult CreateBookArticle(long directoryid, string content, string title)
        {
            docMgr.CreateOrUpdateBookArticle(directoryid, content, title, this.UserId);
            return Content("ok");
        }

        /// <summary>
        /// 显示编辑书籍页面
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult EditBook(long id)
        {
            var book = docMgr.GetBook(id);

            return View(book);
        }

        /// <summary>
        /// 显示书籍详细
        /// </summary>
        /// <param name="bookid"></param>
        /// <param name="docId"></param>
        /// <returns></returns>
        public ActionResult ShowBook(long bookid, long docId=0)
        {
            BookVm book = null;
            if(docId == 0)
            {
                book = docMgr.GetBook(bookid);
            }
            else
            {
                book = docMgr.GetBookByDoc(docId);
            }

            return View(book);
        }

        /// <summary>
        /// jsTree 需要的 Json 数据
        /// </summary>
        /// <param name="bookid"></param>
        /// <returns></returns>
        public ActionResult GetBookDirectory(long bookid)
        {
            var book = docMgr.GetBook(bookid);
            var directories = book.BookDirectory.Select(t => new
            {
                id = t.id,
                parent = t.parent_id,
                text = t.title
            }).ToList();

            return Json(directories);
        }

        /// <summary>
        /// 搜索书籍
        /// </summary>
        /// <param name="searchText"></param>
        /// <returns></returns>
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
                        // 文章名称，包含了目录
                        r.title = SplitContent.HightLight(searchText, r.title);
                        // 书籍目录
                        r.content = SplitContent.HightLight(searchText, r.content.StripHTML());
                        // 书籍名称
                        r.category = SplitContent.HightLight(searchText, r.category);
                    }

                    return View(result);
                }
            }

            return View();
        }
    }
}

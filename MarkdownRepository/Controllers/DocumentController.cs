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
            ViewBag.Action = "MyDocs";
            ViewBag.Category = category;
            return View(result);
        }

        /// <summary>
        /// 所有文档
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult AllDocument(int?page)
        {            
            var result = docMgr.AllDocument(UserId);
            var category = docMgr.GetCategory(UserId);
            var creator = docMgr.GetCreator();
            ViewBag.Action = "ShowAll";
            ViewBag.Category = category;
            ViewBag.Creator = creator;
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
                var result = docMgr.Search(searchText, UserId);
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
        public ActionResult SearchByCategory(string category, bool byOwner)
        {
            ViewBag.CurrentFilter = category;

            if (!String.IsNullOrWhiteSpace(category))
            {
                var result = docMgr.SearchByCategory(category, byOwner?UserId:"");
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
            var doc = docMgr.Get(id);
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

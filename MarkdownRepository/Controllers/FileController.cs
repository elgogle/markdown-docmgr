#region Imports (7)

using MarkdownRepository.Lib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

#endregion Imports (7)

namespace MarkdownRepository.Controllers
{
    [Authorize]
    public class FileController : Controller
    {
        #region Classes of FileController (1)

        class MyAjaxResponse
        {
            #region Properties of MyAjaxResponse (3)

            public object data { get; set; }

            public string message { get; set; }

            public bool success { get; set; }

            #endregion Properties of MyAjaxResponse (3)
        }

        #endregion Classes of FileController (1)

        #region Methods of FileController (19)

        public ActionResult Backpath()
        {
            return View();
        }

        public ActionResult Backup(string path, string file)
        {
            return MyResponse(null);
        }

        public ActionResult Copy(string copy, string finish, string move)
        {
            return MyResponse(null);
        }

        public ActionResult Delete(string currentPath, string deleteFile)
        {
            try
            {
                FileManager.DeleteFile(deleteFile);
            }
            catch (Exception ex)
            {
                ViewData["Error"] = ex.Message;
            }

            return RedirectToAction("Index", new { p = currentPath });
        }

        [AllowAnonymous]
        public ActionResult DirectLink(string encryptDLink)
        {
            var filePath = FileManager.DirectLinkToFullPath(encryptDLink);
            return File(filePath, "application/octet-stream", Path.GetFileName(filePath));
        }

        public ActionResult Download(string dl)
        {
            var filePath = FileManager.GetAbsolutePath(dl);

            return File(filePath, "application/octet-stream", Path.GetFileName(filePath));
        }

        public ActionResult Index(string p)
        {
            ViewBag.Action = "MyFiles";
            var currentPath = p ?? "";
            var files = FileManager.GetFiles(currentPath);
            ViewBag.CurrentPath = currentPath;

            if (p.IsNullOrEmpty() == false)
            {
                ViewBag.ParentPath = Path.GetDirectoryName(currentPath);
            }
            else
            {
                ViewBag.ParentPath = null;
            }

            return View(files);
        }

        public ActionResult MassDelete(string group, string delete)
        {
            return MyResponse(null);
        }

        public ActionResult MyResponse(Action act)
        {
            var res = new MyAjaxResponse { success = true };

            try
            {
                if (act != null)
                    act();
            }
            catch (Exception ex)
            {
                res.success = false;
                res.message = ex.Message;
            }

            return Json(res);
        }

        public ActionResult MyResponse<T>(Func<T> act)
        {
            var res = new MyAjaxResponse { success = true };

            try
            {
                if (act != null)
                    res.data = act();
            }
            catch (Exception ex)
            {
                res.success = false;
                res.message = ex.Message;
            }

            return Json(res);
        }

        public ActionResult NewFolder(string currentPath, string folderName)
        {
            try
            {
                FileManager.CreateFolder(Path.Combine(currentPath, folderName));
            }
            catch (Exception ex)
            {
                ViewData["Error"] = ex.Message;
            }

            return RedirectToAction("Index", new { p = currentPath });
        }

        public ActionResult PackFiles(string group, string file)
        {
            return File("", "application/octet-stream");
        }

        public ActionResult QuickView()
        {
            return View();
        }

        public ActionResult Rename(string currentPath, string from, string to)
        {
            try
            {
                FileManager.RenameFile(currentPath, from, to);
            }
            catch (Exception ex)
            {
                ViewData["Error"] = ex.Message;
            }

            return RedirectToAction("Index", new { p = currentPath });
        }

        public ActionResult Save(HttpPostedFileBase postFile)
        {
            postFile.SaveAs("");
            return MyResponse(null);
        }

        public ActionResult Settings(bool showHidden, string hideCols, string calcFolder)
        {
            return MyResponse(null);
        }

        public ActionResult Unpack(string unzip)
        {
            return MyResponse(null);
        }

        [HttpGet]
        public ActionResult Upload(string p)
        {
            ViewBag.CurrentPath = p;
            return View();
        }

        [HttpPost]
        public ActionResult Upload(string p, ICollection<HttpPostedFileBase> file)
        {
            return MyResponse(() =>
            {
                foreach (var f in file)
                {
                    var absPath = FileManager.GetAbsolutePath(Path.Combine(p, f.FileName));
                    f.SaveAs(absPath);
                }
            });
        }

        #endregion Methods of FileController (19)
    }
}

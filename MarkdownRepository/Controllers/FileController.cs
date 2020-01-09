using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.IO;
using MarkdownRepository.Lib;

namespace MarkdownRepository.Controllers
{
    [Authorize]
    public class FileController : Controller
    {
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

        public ActionResult Delete(string currentPath, string deleteFile)
        {
            try
            {
                FileManager.DeleteFile(deleteFile);
            }
            catch(Exception ex)
            {
                ViewData["Error"] = ex.Message;
            }

            return RedirectToAction("Index", new { p = currentPath });
        }

        public ActionResult NewFolder(string currentPath, string folderName)
        {
            try
            {
                FileManager.CreateFolder(Path.Combine(currentPath, folderName));
            }
            catch(Exception ex)
            {
                ViewData["Error"] = ex.Message;
            }

            return RedirectToAction("Index", new { p = currentPath });
        }

        public ActionResult Rename(string currentPath, string from, string to)
        {
            try
            {
                FileManager.RenameFile(currentPath, from, to);
            }
            catch(Exception ex)
            {
                ViewData["Error"] = ex.Message;
            }

            return RedirectToAction("Index", new { p = currentPath });
        }

        public ActionResult Download(string dl)
        {
            var filePath = FileManager.GetAbsolutePath(dl);

            return File(filePath, "application/octet-stream", Path.GetFileName(filePath));
        }

        [AllowAnonymous]
        public ActionResult DirectLink(string encryptDLink)
        {
            var filePath = FileManager.DirectLinkToFullPath(encryptDLink);
            return File(filePath, "application/octet-stream", Path.GetFileName(filePath));
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

        public ActionResult QuickView()
        {
            return View();
        }

        public ActionResult Backpath()
        {
            return View();
        }

        public ActionResult Save(HttpPostedFileBase postFile)
        {
            postFile.SaveAs("");
            return MyResponse(null);
        }

        public ActionResult Backup(string path, string file)
        {
            return MyResponse(null);
        }

        public ActionResult Settings(bool showHidden, string hideCols, string calcFolder)
        {
            return MyResponse(null);
        }

        public ActionResult Copy(string copy, string finish, string move)
        {
            return MyResponse(null);
        }

        public ActionResult MassDelete(string group, string delete)
        {
            return MyResponse(null);
        }

        public ActionResult PackFiles(string group, string file)
        {
            return File("", "application/octet-stream");
        }

        public ActionResult Unpack(string unzip)
        {
            return MyResponse(null);
        }

        public ActionResult MyResponse(Action act)
        {
            var res = new MyAjaxResponse { success = true };

            try
            {
                if(act != null)
                    act();
            }
            catch(Exception ex)
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
                if(act != null)
                    res.data = act();
            }
            catch (Exception ex)
            {
                res.success = false;
                res.message = ex.Message;
            }

            return Json(res);
        }

        class MyAjaxResponse
        {
            public object data { get; set; }
            public bool success { get; set; }
            public string message { get; set; }
        }
    }
}

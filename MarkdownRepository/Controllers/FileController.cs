using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.IO;

namespace MarkdownRepository.Controllers
{
    public class FileController : Controller
    {
        //
        const string FM_PATH = "";
        const string PHP_SELF = "";

        public ActionResult Index()
        {
            return View();
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

        public ActionResult Delete(string deleteFile)
        {
            return MyResponse(null);
        }

        public ActionResult New(string type, string newFile)
        {
            return MyResponse(null);
        }

        public ActionResult Copy(string copy, string finish, string move)
        {
            return MyResponse(null);
        }

        public ActionResult Rename(string ren, string to)
        {
            return MyResponse(null);
        }

        public ActionResult Download(string dl)
        {
            return File("", "application/octet-stream");
        }

        public ActionResult Upload(HttpPostedFileBase file)
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

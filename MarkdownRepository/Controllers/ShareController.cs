using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MarkdownRepository.Controllers
{
    public class ShareController : Controller
    {
        //
        // GET: /Share/

        // 查看全部分享的网址
        public ActionResult Index()
        {
            return View();
        }

        // 分享功能，记录网址、Token
        public ActionResult Add(string url, TimeSpan expireTime)
        {
            return Content("ok");
        }

        // 检验来自分享的网址，并跳转
        public ActionResult Goto(string url, string token)
        {
            return Content("ok");
        }

        // 删除分享的网址
        public ActionResult Delete(string url)
        {
            return Content("ok");
        }

        // 修改分享的过期时间
        public ActionResult Edit(string url, TimeSpan expireTime)
        {
            return Content("ok");
        }
    }
}

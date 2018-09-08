using MarkdownRepository.Lib;
using MarkdownRepository.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;

namespace MarkdownRepository.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                var a = model.UserName.Split('\\');
                var domain = a.Length > 1 ? a[0] : "";
                var userId = a.Length > 1 ? a[1] : model.UserName;

                if (AdAccount.IsAuthenticated(domain, userId, model.Password))
                {
                    FormsAuthenticationTicket ticket = new FormsAuthenticationTicket(
                        1,
                        model.UserName,
                        DateTime.Now,
                        DateTime.Now.Add(FormsAuthentication.Timeout),
                        true,
                        ""
                        );

                    HttpCookie cookie = new HttpCookie(
                        FormsAuthentication.FormsCookieName,
                        FormsAuthentication.Encrypt(ticket));
                    Response.Cookies.Add(cookie);

                    if (!String.IsNullOrEmpty(returnUrl)) return Redirect(returnUrl);
                    else return RedirectToAction("Index", "Document", new { Key = DateTime.Now });
                }
                else
                    ModelState.AddModelError("", "用户名或密码不正确");
            }

            return View();
        }

        public ActionResult LogOff()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("AllDocument", "Document", new { Key = DateTime.Now });
        }
    }
}

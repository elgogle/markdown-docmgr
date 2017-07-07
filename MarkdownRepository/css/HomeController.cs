using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.IO;
using System.Text;

namespace CrystalGroup.ISD.DocumentManage.Controllers
{
    public class HomeController : Controller
    {
        const string DOC_PATH = "/Doc";

        public ActionResult Index(string searchText, string path)
        {
            ViewBag.DocMenu = this.RenderDocMenu();     
            ViewBag.MD = this.RenderMarkdown(path);
            ViewBag.BasePath = DOC_PATH;


            // TODO: Search
            if (!string.IsNullOrEmpty(searchText))
            {
                ViewBag.SearchResult = "搜索功能正在开发中...";
            }

            return View();
        }

        private string RenderMarkdown(string path)
        {
            try
            {
                string mdPath = Server.MapPath("/" + path);
                if (System.IO.File.Exists(mdPath))
                {
                    var md = new MarkdownSharp.Markdown();
                    
                    string input = System.IO.File.ReadAllText(mdPath);
                    return md.Transform(input);
                }

                return "";
            }
            catch (Exception ex) { return ex.Message; }
        }

        private string RenderDocMenu()
        {
            StringBuilder sb = new StringBuilder();
            string basePath = Server.MapPath(DOC_PATH);
            this.ScanMarkdown(basePath, sb);
            return sb.ToString();
        }

        private void ScanMarkdown(string path, StringBuilder sb)
        {
            try
            {
                foreach (string d in Directory.GetDirectories(path))
                {
                    sb.AppendLine("<div class=\"sidebar-module\">");
                    sb.AppendFormat("<h4>{0}</h4>", new DirectoryInfo(d).Name);
                    sb.AppendLine();
                    sb.AppendLine("<ol class=\"list-unstyled\">");

                    foreach (string f in Directory.GetFiles(d, "*.md"))
                    {
                        System.Uri baseUri = new Uri(Server.MapPath(DOC_PATH));
                        System.Uri uri = new Uri(f);
                        var reletivePath = baseUri.MakeRelativeUri(uri).ToString();
                        sb.AppendFormat("<li><a href=\"/Home/Index?path={0}\">{1}</a></li>", reletivePath, Path.GetFileNameWithoutExtension(f));
                        sb.AppendLine();
                    }

                    sb.AppendLine("</ol>");
                    sb.AppendLine("</div>");                    
                }
            }
            catch { }
        }
    }
}

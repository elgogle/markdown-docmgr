﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.IO;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.IO.Compression;
using ICSharpCode.SharpZipLib.Zip;
using PagedList;

namespace MarkdownRepository.Controllers
{
    using Lib;
    using System.Threading.Tasks;

    [Authorize]
    public class CodeSearchController : Controller
    {
        const string CodeIndexPath = "~/App_Data/";

        private string UserId
        {
            get
            {
                return string.IsNullOrWhiteSpace(User?.Identity.Name) ?
                    "Anonymous" : User.Identity.Name.GetUserName();
            }
        }

        private CodeIndexManager GetIndexManager()
        {
            var absPath = this.Server.MapPath(CodeIndexPath);
            var codeIndexManager = CodeIndexManager.GetInstance(absPath);
            return codeIndexManager;
        }


        [HttpGet]
        public ActionResult Upload()
        {
            return View();
        }

        public ActionResult Delete(string id)
        {
            var result = new JsonResponse
            {
                success = true
            };

            var indexMgr = this.GetIndexManager();
            var doc = indexMgr.Get(id);
            if(doc != null)
            {
                if (this.UserId != doc.UserId)
                {
                    result.success = false;
                    result.message = "该文档你无权删除";
                }
                else
                {
                    doc.Operate = Operate.Delete;
                    indexMgr.Enqueue(doc);
                }
            }
            else
            {
                result.success = false;
                result.message = "文档不存在";
            }

            return Json(result);
        }

        [AllowAnonymous]
        public ActionResult Search(string searchText, string codeLanguage, int? page)
        {
            ViewBag.Title = "代码搜索";
            ViewBag.Action = "CodeSearch";
            ViewBag.CodeLanguage = codeLanguage.IsNullOrEmpty()?CodeLanguage.Csharp: codeLanguage;

            int pageSize = 15;
            int pageNumber = page ?? 1;
            ViewBag.SearchText = searchText;

            var codeIndexManager = GetIndexManager();
            var result = codeIndexManager.Search(searchText, codeLanguage);

            return View(result.ToPagedList(pageNumber, pageSize));
        }

        [AllowAnonymous]
        public ActionResult Show(string id)
        {
            ViewBag.Title = "代码搜索";
            var indexMgr = this.GetIndexManager();
            var doc = indexMgr.Get(id);
            return View(doc);
        }

        [AllowAnonymous]
        public ActionResult GetSearchAutoCompleteList(string searchText, string codeLanguage)
        {
            var codeIndexManager = GetIndexManager();

            var result = new JsonResponse
            {
                success = true,
                data = codeIndexManager.GetAutoCompleteList(searchText, codeLanguage)
            };

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [ValidateInput(false)]
        [HttpPost]
        public ActionResult PutCode(string codeLanguage, string codeSearchText, string codeSearchCodeBody)
        {
            var result = new JsonResponse
            {
                success = true
            };

            if(codeLanguage.IsNullOrEmpty())
            {
                result.success = false;
                result.message = "请选择编程语言";
            }
            else if(codeSearchText.IsNullOrEmpty())
            {
                result.success = false;
                result.message = "搜索词不能为空";
            }
            else if (codeSearchCodeBody.IsNullOrEmpty())
            {
                result.success = false;
                result.message = "代码不能为空";
            }
            else
            {
                var m = new CodeIndex
                {
                    CodeBody = codeSearchCodeBody,
                    Id = codeSearchCodeBody.ToHashText(),
                    Language = codeLanguage,
                    Operate = Operate.AddOrUpdate,
                    SearchText = codeSearchText,
                    UserId = UserId,
                    FileId = string.Empty
                };

                var indexMgr = this.GetIndexManager();
                indexMgr.Enqueue(m);
            }

            return Json(result);
        }
        

        [HttpPost]
        public async Task<ActionResult> Upload(ICollection<HttpPostedFileBase> file)
        {
            var result = new JsonResponse
            {
                success = true
            };

            try
            {
                var codeIndexManager = GetIndexManager();

                foreach (var f in file)
                {
                    if(f.FileName.EndsWith(".cs", StringComparison.InvariantCultureIgnoreCase))
                    {

                        var sr = new StreamReader(f.InputStream);
                        var text = sr.ReadToEnd();
                        var codemodels = await ParseCsharpFile(text);
                        foreach (var m in codemodels)
                        {
                            codeIndexManager.Enqueue(m);
                        }
                    }

                    if(f.FileName.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
                    {
                        try
                        {
                            var zip = new ZipFile(f.InputStream);

                            foreach (ZipEntry entry in zip)
                            {
                                if (!entry.IsFile) continue;

                                String entryFileName = entry.Name;

                                if (entryFileName.EndsWith(".cs", StringComparison.InvariantCulture) == false) continue;

                                var st = zip.GetInputStream(entry);
                                var sr = new StreamReader(st);
                                var text = sr.ReadToEnd();
                                var codemodels = await ParseCsharpFile(text);

                                foreach (var m in codemodels)
                                {
                                    codeIndexManager.Enqueue(m);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteError(this.GetType(), ex);
                        }
                    }

                    throw new Exception("不支持该类型文件，请上传 C# 文件或包含 C# 文件的 zip 包。");
                }
            }
            catch(Exception ex)
            {
                result.success = false;
                result.message = ex.Message;
            }

            return Json(result);
        }


        private async Task<List<CodeIndex>> ParseCsharpFile(string fileContent)
        {
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("([A-Z]+[a-z]+)");
            List<CodeIndex> result = new List<CodeIndex>();
            await Task.Run((Action)(() =>
            {
                var tree = CSharpSyntaxTree.ParseText(fileContent);
                var node = tree.GetRoot();
                foreach (var n in node.DescendantNodes())
                {
                    if ((n is ClassDeclarationSyntax)
                        || (n is MethodDeclarationSyntax)
                        && n.HasLeadingTrivia)
                    {
                        var codeBody = n.GetText().ToString();
                        string methodName = string.Empty;

                        if(n is MethodDeclarationSyntax)
                        {
                            methodName = (n as MethodDeclarationSyntax).Identifier.Text;
                        }

                        if (n is ClassDeclarationSyntax)
                        {
                            methodName = (n as ClassDeclarationSyntax).Identifier.Text;
                        }

                        foreach (var d in n.GetLeadingTrivia())
                        {
                            if (d.HasStructure)
                            {
                                var o = d.GetStructure()
                                    .DescendantNodes()
                                    .Where(p => p is XmlTextSyntax)
                                    .OfType<XmlTextSyntax>()
                                    .Select(p => p.TextTokens);
                                var comment = string.Empty;
                                foreach (var r in o)
                                {
                                    foreach (var w in r)
                                    {
                                        if (w.RawKind == (int)SyntaxKind.XmlTextLiteralToken
                                            && w.HasLeadingTrivia
                                            && w.Text.Trim().Length > 0)
                                        {
                                            comment = w.Text;
                                        }
                                    }
                                }

                                var m = new CodeIndex
                                {
                                    Id = codeBody.ToHashText(),
                                    CodeBody = codeBody,
                                    SearchText = comment + " "
                                                // 将 Pascal 名称拆分成空格连接的单个单词
                                                + regex.Replace(methodName, s => (s.Value.Length > 3 ? s.Value : s.Value.ToLower()) + " "),
                                    UserId = UserId,
                                    Language = CodeLanguage.Csharp,
                                    FileContent = fileContent,
                                    FileId = fileContent.ToHashText()
                                };

                                result.Add(m);
                            }
                        }
                    }
                }
            }));
            return result;
        }

        class JsonResponse
        {

            public object data { get; set; }

            public string message { get; set; }

            public bool success { get; set; }

        }
    }

    public struct CodeLanguage
    {
        public const string Csharp = "C#";
        public const string Js = "Js";
        public const string Html = "Html";
        public const string Css = "Css";
        public const string Sql = "Sql";
        public const string Abap = "Abap";
        public const string Vb = "Vb";
        public const string Java = "Java";
        public const string ObjectC = "Object-C";
    }
}

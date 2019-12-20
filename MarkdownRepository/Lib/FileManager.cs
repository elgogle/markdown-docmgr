using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;

namespace MarkdownRepository.Lib
{
    public class FileManager
    {
        static readonly string ROOT_PATH = HttpContext.Current.Server.MapPath("~/upload_files");

        private static string GetAbsolutePath(string relatePath)
        {
            var user = HttpContext.Current.User.Identity.Name.GetUserName();
            var path = Path.Combine(ROOT_PATH, user, relatePath);
            return path;
        }

        private static string GetRelatePath(string absPath)
        {
            var user = HttpContext.Current.User.Identity.Name.GetUserName();
            var path = Path.Combine(ROOT_PATH, user);

            return absPath.Replace(path, "");
        }


        public static string GetFileIconClass(string ext)
        {
            switch (ext)
            {
                case "ico":
                case "gif":
                case "jpg":
                case "jpeg":
                case "jpc":
                case "jp2":
                case "jpx":
                case "xbm":
                case "wbmp":
                case "png":
                case "bmp":
                case "tif":
                case "tiff":
                case "svg":
                    return "fa fa-picture-o";

                case "passwd":
                case "ftpquota":
                case "sql":
                case "js":
                case "json":
                case "sh":
                case "config":
                case "twig":
                case "tpl":
                case "md":
                case "gitignore":
                case "c":
                case "cpp":
                case "cs":
                case "py":
                case "map":
                case "lock":
                case "dtd":
                    return "fa fa-file-code-o";

                case "txt":
                case "ini":
                case "conf":
                case "log":
                case "htaccess":
                    return "fa fa-file-text-o";

                case "css":
                case "less":
                case "sass":
                case "scss":
                    return "fa fa-css3";

                case "zip":
                case "rar":
                case "gz":
                case "tar":
                case "7z":
                    return "fa fa-file-archive-o";

                case "php":
                case "php4":
                case "php5":
                case "phps":
                case "phtml":
                    return "fa fa-code";

                case "htm":
                case "html":
                case "shtml":
                case "xhtml":
                    return "fa fa-html5";

                case "xml":
                case "xsl":
                    return "fa fa-file-excel-o";

                case "wav":
                case "mp3":
                case "mp2":
                case "m4a":
                case "aac":
                case "ogg":
                case "oga":
                case "wma":
                case "mka":
                case "flac":
                case "ac3":
                case "tds":
                    return "fa fa-music";

                case "m3u":
                case "m3u8":
                case "pls":
                case "cue":
                    return "fa fa-headphones";

                case "avi":
                case "mpg":
                case "mpeg":
                case "mp4":
                case "m4v":
                case "flv":
                case "f4v":
                case "ogm":
                case "ogv":
                case "mov":
                case "mkv":
                case "3gp":
                case "asf":
                case "wmv":
                    return "fa fa-file-video-o";

                case "eml":
                case "msg":
                    return "fa fa-envelope-o";

                case "xls":
                case "xlsx":
                case "ods":
                    return "fa fa-file-excel-o";

                case "csv":
                    return "fa fa-file-text-o";

                case "bak":
                    return "fa fa-clipboard";

                case "doc":
                case "docx":
                case "odt":
                    return "fa fa-file-word-o";

                case "ppt":
                case "pptx":
                    return "fa fa-file-powerpoint-o";

                case "ttf":
                case "ttc":
                case "otf":
                case "woff":
                case "woff2":
                case "eot":
                case "fon":
                    return "fa fa-font";

                case "pdf":
                    return "fa fa-file-pdf-o";

                case "psd":
                case "ai":
                case "eps":
                case "fla":
                case "swf":
                    return "fa fa-file-image-o";

                case "exe":
                case "msi":
                    return "fa fa-file-o";

                case "bat":
                    return "fa fa-terminal";

                default:
                    return "fa fa-info-circle";
            }
        }

        public static List<string> GetImageExtentionName()
        {
            return new List<string> { "ico", "gif", "jpg", "jpeg", "jpc", "jp2", "jpx", "xbm", "wbmp", "png", "bmp", "tif", "tiff", "psd", "svg" };
        }

        public static List<string> GetVideoExtentionName()
        {
            return new List<string> { "webm", "mp4", "m4v", "ogm", "ogv", "mov", "mkv" };
        }

        public static List<string> GetAudioExtentionName()
        {
            return new List<string> { "wav", "mp3", "ogg", "m4a" };
        }

        public static List<string> GetTextExtentionName()
        {
            return new List<string>
            {
                "txt", "css", "ini", "conf", "log", "htaccess", "passwd", "ftpquota", "sql", "js", "json", "sh", "config",
                "php", "php4", "php5", "phps", "phtml", "htm", "html", "shtml", "xhtml", "xml", "xsl", "m3u", "m3u8", "pls", "cue",
                "eml", "msg", "csv", "bat", "twig", "tpl", "md", "gitignore", "less", "sass", "scss", "c", "cpp", "cs", "py",
                "map", "lock", "dtd", "svg", "scss", "asp", "aspx", "asx", "asmx", "ashx", "jsx", "jsp", "jspx", "cfm", "cgi"
            };
        }

        public static List<string> GetTextMimes()
        {
            return new List<string>
            {
                "application/xml",
                "application/javascript",
                "application/x-javascript",
                "image/svg+xml",
                "message/rfc822"
            };
        }

        public static List<string> GetOnlineViewerExtentionName()
        {
            return new List<string> { "doc", "docx", "xls", "xlsx", "pdf", "ppt", "pptx", "ai", "psd", "dxf", "xps", "rar", "odt", "ods" };
        }

        public static string DeleteFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)
                || path.Trim() == "."
                || path.Trim() == ".."
                ) return "Wrong file or folder name";

            var file = GetAbsolutePath(path);
            if (File.Exists(file))
            {
                try
                {
                    File.Delete(file);
                    return string.Format("File <b>{0}</b> deleted", path);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteError(typeof(FileManager), ex);
                    return string.Format("File <b>{0}</b> not deleted", path);
                }
            }
            else if (Directory.Exists(file))
            {
                try
                {
                    Directory.Delete(file);
                    return string.Format("Folder <b>{0}</b> deleted", path);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteError(typeof(FileManager), ex);
                    return string.Format("Folder <b>{0}</b> not deleted", path);
                }
            }
            else
            {
                return "Wrong file or folder name";
            }
        }

        public static string CreateFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path)
                || path.Trim() == "."
                || path.Trim() == ".."
                ) return "Wrong folder name";

            var file = GetAbsolutePath(path);

            if (Directory.Exists(file))
            {
                return string.Format("Folder <b>{0}</b> already exists", path);
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(file);
                    return string.Format("Folder <b>{0}</b> created", path);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteError(typeof(FileManager), ex);
                    return string.Format("Folder <b>{0}</b> not deleted", path);
                }
            }
        }

        public static Message CopyFile(string source, string destination)
        {
            if (string.IsNullOrWhiteSpace(source)
                || source.Trim() == "."
                || source.Trim() == ".."
                ) return new Message("Source path not defined", MessageType.Error);

            var from = GetAbsolutePath(source);
            var srcFileName = Path.GetFileName(from);

            var dest = Path.Combine(GetAbsolutePath(destination), srcFileName);
            if (from == dest)
            {
                return new Message("Paths must be not equal", MessageType.Error);
            }
            else
            {
                if (File.Exists(dest))
                {
                    return new Message("File or folder with this path already exists", MessageType.Alert);
                }

                try
                {
                    File.Copy(from, dest);
                    return new Message(string.Format("Copyied from <b>{0}</b> to <b>{1}</b>", from, dest));
                }
                catch (Exception ex)
                {
                    LogHelper.WriteError(typeof(FileManager), ex);
                    return new Message(string.Format("Error while moving from <b>{0}</b> to <b>{1}</b>", from, dest), MessageType.Error);
                }
            }
        }

        private struct MessageType
        {
            public static readonly string Error = "error";
            public static readonly string Alert = "alert";
        }

        public class Message
        {
            public string Content { get; set; }
            public string MsgType { get; set; }

            public Message(string msg)
            {
                this.Content = msg;
                this.MsgType = "success";
            }

            public Message(string msg, string msgType)
            {
                this.Content = msg;
                this.MsgType = msgType;
            }
        }
    }
}
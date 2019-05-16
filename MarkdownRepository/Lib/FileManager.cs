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

        public static string DeleteFile(string path)
        {
            if(string.IsNullOrWhiteSpace(path)
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
                catch(Exception ex)
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
                if(File.Exists(dest))
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
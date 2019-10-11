using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace MarkdownRepository.Lib
{
    public static class Extentions
    {
        public static string GetUserName(this string fullName)
        {
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                if (fullName.Any(t => t == '\\'))
                {
                    return fullName.Substring(fullName.LastIndexOf('\\') + 1);
                }

                return fullName;
            }

            return fullName;
        }

        const string HTML_TAG_PATTERN = "<.*?>";
        /// <summary>
        /// 去掉html 标签
        /// </summary>
        /// <param name="inputString"></param>
        /// <returns></returns>
        public static string StripHTML(this string inputString)
        {
            return Regex.Replace
              (inputString, HTML_TAG_PATTERN, string.Empty);
        }

        /// <summary>
        /// 获取摘要, 取256个长度
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public static string GetShortDesc(this string content, int length=256)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            if (content.Length > length)
                return content.Substring(0, length);

            return content;
        }


        /// <summary>
        /// 是否为数值
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsNum(this string str)
        {
            double num;
            return double.TryParse(str, out num);
        }

        /// <summary>
        /// 是否为空
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsNullOrEmpty(this string str)
        {
            return string.IsNullOrEmpty(str) || string.IsNullOrWhiteSpace(str);
        }

        /// <summary>
        /// 返回右边最多指定长度字符串
        /// </summary>
        /// <param name="str"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string Right(this string str, int length)
        {
            if (str.IsNullOrEmpty()) return "";
            str = str.Trim();
            return str.Length > length ? str.Substring(str.Length - length, length) : str;
        }

        /// <summary>
        /// 返回左边最多指定长度字符串
        /// </summary>
        /// <param name="str"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string Left(this string str, int length)
        {
            if (str.IsNullOrEmpty()) return "";

            str = str.Trim();
            return str.Length > length ? str.Substring(0, length) : str;
        }
    }
}
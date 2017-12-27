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
    }
}